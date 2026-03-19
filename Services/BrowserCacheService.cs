using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using WinOptimizerHub.Helpers;

namespace WinOptimizerHub.Services
{
    public class BrowserCacheService
    {
        private readonly string _local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private readonly string _roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        private List<BrowserInfo> _cachedResult;
        private DateTime _cacheTime = DateTime.MinValue;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(10);

        public List<BrowserInfo> ScanBrowsers(bool forceRefresh = false)
        {
            if (!forceRefresh
                && _cachedResult != null
                && (DateTime.Now - _cacheTime) < CacheTtl)
                return _cachedResult;

            _cachedResult = ScanBrowsersInternal();
            _cacheTime = DateTime.Now;
            return _cachedResult;
        }

        public void InvalidateCache() => _cacheTime = DateTime.MinValue;

        private List<BrowserInfo> ScanBrowsersInternal()
        {
            var results = new List<BrowserInfo>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (name, exePath) in GetBrowsersFromRegistry())
            {
                string engine = DetectEngineByDlls(exePath);
                if (engine == "Unknown") continue;

                string profileRoot = FindProfileRootFromExePath(exePath, engine);
                if (profileRoot == null) continue;

                if (engine == "Chromium")
                    ScanChromiumProfiles(results, name, profileRoot, seenPaths);
                else if (engine == "Gecko")
                    ScanGeckoProfiles(results, name, profileRoot, seenPaths);
            }
            ScanAppDataForBrowsers(results, seenPaths);

            return results;
        }

        public long CleanBrowserCache(List<BrowserInfo> browsers)
        {
            long total = 0;
            foreach (var b in browsers)
            {
                try
                {
                    var di = new DirectoryInfo(b.Path);
                    if (!di.Exists) continue;

                    foreach (var f in di.EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            long fileSize = f.Length;
                            f.Delete();
                            total += fileSize;
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Log(ex,$"Error deleting file {f.FullName}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Log(ex,$"Error accessing directory {b.Path}: {ex.Message}");
                }
            }
            InvalidateCache();
            return total;
        }

        private void ScanAppDataForBrowsers(List<BrowserInfo> results, HashSet<string> seenPaths)
        {
            foreach (var root in new[] { _local, _roaming })
            {
                foreach (var lvl1 in SafeGetDirectories(root))
                {
                    CheckAndAddIfBrowser(lvl1, results, seenPaths);

                    foreach (var lvl2 in SafeGetDirectories(lvl1))
                    {
                        CheckAndAddIfBrowser(lvl2, results, seenPaths);

                        foreach (var lvl3 in SafeGetDirectories(lvl2))
                        {
                            CheckAndAddIfBrowser(lvl3, results, seenPaths);
                        }
                    }
                }
            }
        }

        private void CheckAndAddIfBrowser(string dir, List<BrowserInfo> results, HashSet<string> seenPaths)
        {
            if (IsValidChromiumRoot(dir))
            {

            }
            else if (IsValidGeckoRoot(dir))
            {
                string name = GuessNameFromPath(dir);
                BrowserInfo.BrowserCategory category = BrowserInfo.BrowserCategory.Browser;
                ScanGeckoProfiles(results, name, dir, seenPaths, category);
            }
            else
            {
                string userDataSub = Path.Combine(dir, "User Data");
                if (IsValidChromiumRoot(userDataSub))
                {
                    string name = GuessNameFromPath(dir);
                    ScanChromiumProfiles(results, name, userDataSub, seenPaths, BrowserInfo.BrowserCategory.Browser);
                }
            }
        }

        private string GuessNameFromPath(string path)
        {
            string normalized = path.TrimEnd(Path.DirectorySeparatorChar);
            if (normalized.EndsWith("User Data", StringComparison.OrdinalIgnoreCase))
                normalized = Path.GetDirectoryName(normalized) ?? normalized;

            return Path.GetFileName(normalized) ?? "Unknown Browser";
        }

        private List<(string Name, string ExePath)> GetBrowsersFromRegistry()
        {
            var browsers = new List<(string, string)>();
            var seenExe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string[] regKeys = {
                @"SOFTWARE\Clients\StartMenuInternet",
                @"SOFTWARE\WOW6432Node\Clients\StartMenuInternet"
            };

            foreach (var key in regKeys)
            {
                using var root = Registry.LocalMachine.OpenSubKey(key);
                if (root == null) continue;

                foreach (var browserKey in root.GetSubKeyNames())
                {
                    using var shell = root.OpenSubKey($@"{browserKey}\shell\open\command");
                    var raw = shell?.GetValue("")?.ToString();
                    if (string.IsNullOrWhiteSpace(raw)) continue;

                    string exePath = raw.Trim().TrimStart('"');
                    int quoteEnd = exePath.IndexOf('"');
                    if (quoteEnd >= 0) exePath = exePath.Substring(0, quoteEnd);
                    exePath = exePath.Trim();

                    if (!File.Exists(exePath) || !seenExe.Add(exePath)) continue;

                    browsers.Add((GetFriendlyName(browserKey, exePath), exePath));
                }
            }

            return browsers;
        }

        private string GetFriendlyName(string registryKey, string exePath)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrWhiteSpace(info.ProductName))
                    return info.ProductName;
            }
            catch (Exception ex){ AppLogger.Log(ex, $"Error retrieving friendly name for {exePath}: {ex.Message}"); }
            return registryKey;
        }

        private string DetectEngineByDlls(string exePath)
        {
            string dir = Path.GetDirectoryName(exePath) ?? "";

            if (File.Exists(Path.Combine(dir, "chrome.dll")) ||
                File.Exists(Path.Combine(dir, "libcef.dll")) ||
                File.Exists(Path.Combine(dir, "electron.exe")) ||
                SafeGetFiles(dir, "chrome_*.pak").Any())
                return "Chromium";

            if (File.Exists(Path.Combine(dir, "xul.dll")) ||
                File.Exists(Path.Combine(dir, "mozglue.dll")) ||
                File.Exists(Path.Combine(dir, "nss3.dll")))
                return "Gecko";

            return "Unknown";
        }

        private string FindProfileRootFromExePath(string exePath, string engine)
        {
            string exeDir = Path.GetDirectoryName(exePath) ?? "";

            if (engine == "Chromium")
            {
                string portable = Path.Combine(exeDir, "User Data");
                if (IsValidChromiumRoot(portable)) return portable;
            }

            var parts = exePath.Split(Path.DirectorySeparatorChar)
                .Where(p => !string.IsNullOrWhiteSpace(p) &&
                            !p.Equals("Program Files", StringComparison.OrdinalIgnoreCase) &&
                            !p.Equals("Program Files (x86)", StringComparison.OrdinalIgnoreCase) &&
                            !p.Equals("Application", StringComparison.OrdinalIgnoreCase) &&
                            !p.Equals("Windows", StringComparison.OrdinalIgnoreCase) &&
                            !p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var appDataRoot in new[] { _local, _roaming })
            {
                for (int take = 1; take <= Math.Min(parts.Count, 3); take++)
                {
                    string subPath = Path.Combine(parts.Skip(parts.Count - take - 1)
                                                       .Take(take).ToArray());
                    string basePath = Path.Combine(appDataRoot, subPath);

                    if (engine == "Chromium")
                    {
                        if (IsValidChromiumRoot(basePath)) return basePath;
                        string ud = Path.Combine(basePath, "User Data");
                        if (IsValidChromiumRoot(ud)) return ud;
                    }
                    else if (engine == "Gecko")
                    {
                        if (IsValidGeckoRoot(basePath)) return basePath;
                    }
                }
            }

            return null;
        }

        private bool IsValidChromiumRoot(string path)
        {
            if (!Directory.Exists(path)) return false;
            return File.Exists(Path.Combine(path, "Local State")) &&
                   Directory.Exists(Path.Combine(path, "Default"));
        }

        private bool IsValidGeckoRoot(string path)
        {
            if (!Directory.Exists(path)) return false;
            string profilesPath = Path.Combine(path, "Profiles");
            if (!Directory.Exists(profilesPath)) return false;
            return SafeGetDirectories(profilesPath)
                .Any(p => Path.GetFileName(p).Contains(".default") ||
                          Path.GetFileName(p).Contains(".release"));
        }

        private void ScanChromiumProfiles(List<BrowserInfo> list, string name,
            string userDataPath, HashSet<string> seen,
            BrowserInfo.BrowserCategory category = BrowserInfo.BrowserCategory.Browser)
        {
            var profiles = SafeGetDirectories(userDataPath, "Default")
                .Concat(SafeGetDirectories(userDataPath, "Profile *"));

            if (!profiles.Any() && Directory.Exists(Path.Combine(userDataPath, "Cache")))
                profiles = new[] { userDataPath };

            string[] cacheSubFolders = { "Cache", @"Network\Cache", @"Code Cache\js" };

            foreach (var profile in profiles)
                foreach (var sub in cacheSubFolders)
                    AddIfValid(list, $"{name} (Chromium)", Path.Combine(profile, sub), seen, category);
        }

        private void ScanGeckoProfiles(List<BrowserInfo> list, string name,
            string basePath, HashSet<string> seen,
            BrowserInfo.BrowserCategory category = BrowserInfo.BrowserCategory.Browser)
        {
            string profilesPath = Path.Combine(basePath, "Profiles");
            if (!Directory.Exists(profilesPath)) return;

            foreach (var profilePath in SafeGetDirectories(profilesPath))
            {
                AddIfValid(list, $"{name} (Gecko)", profilePath, seen, category);

                string cache2 = Path.Combine(profilePath, "cache2");
                AddIfValid(list, $"{name} (Gecko)", cache2, seen, category);
            }
        }

        private void AddIfValid(List<BrowserInfo> list, string name, string path,
            HashSet<string> seen, BrowserInfo.BrowserCategory category = BrowserInfo.BrowserCategory.Browser)
        {
            if (!Directory.Exists(path)) return;
            if (!seen.Add(path)) return;

            long size = GetSize(path);
            if (size > 1024 * 1024)
                list.Add(new BrowserInfo
                {
                    Name = name,
                    Path = path,
                    Size = size,
                    IsSelected = true,
                    Category = category
                });
        }

        private long GetSize(string path)
        {
            try
            {
                return new DirectoryInfo(path)
                    .EnumerateFiles("*", SearchOption.AllDirectories)
                    .Sum(f => f.Length);
            }
            catch { return 0; }
        }

        private IEnumerable<string> SafeGetDirectories(string path, string pattern = "*")
        {
            try { return Directory.GetDirectories(path, pattern); }
            catch { return Enumerable.Empty<string>(); }
        }

        private IEnumerable<string> SafeGetFiles(string path, string pattern = "*")
        {
            try { return Directory.GetFiles(path, pattern); }
            catch { return Enumerable.Empty<string>(); }
        }
    }
}