using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;

namespace WinOptimizerHub.Services
{
    public enum CleaningMode { Safe, Normal, Aggressive }

    public enum CleanStrategy { Folder, Pattern, FullTree }

    public class JunkCleanerService
    {
        private static readonly string Win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        private static readonly string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private static readonly string Roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        private static readonly string Temp = System.IO.Path.GetTempPath();
        private static readonly string SysDrv = System.IO.Path.GetPathRoot(
            Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? @"C:\";
        private static readonly string Profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        public async Task<List<CleanableFolder>> ScanAsync(
            CleaningMode mode, IProgress<string> progress = null, CancellationToken ct = default)
        {
            var definitions = BuildDefinitions(mode);
            var results = new List<CleanableFolder>();
            var sem = new SemaphoreSlim(4);

            var tasks = definitions.Select(async folder =>
            {
                await sem.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    if (ct.IsCancellationRequested) return;
                    progress?.Report($"Scanning: {folder.Name}...");

                    var (size, count) = await Task.Run(
                        () => GetStats(folder.Path, folder.Strategy, folder.Extensions), ct)
                        .ConfigureAwait(false);

                    if (size > 0 || count > 0)
                    {
                        folder.Size = size;
                        folder.FileCount = count;
                        lock (results) { results.Add(folder); }
                    }
                }
                catch (Exception ex) { AppLogger.Log(ex, $"Scan:{folder.Name}"); }
                finally { sem.Release(); }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return results.OrderByDescending(r => r.Size).ToList();
        }

        public async Task<(long bytesFreed, int filesDeleted)> CleanAsync(
            IEnumerable<CleanableFolder> folders,
            IProgress<string> progress = null,
            CancellationToken ct = default)
        {
            long freed = 0;
            int deleted = 0;

            foreach (var f in folders.Where(x => x.IsSelected))
            {
                if (ct.IsCancellationRequested) break;
                progress?.Report($"Cleaning: {f.Name}...");

                bool isWuCache = f.Path.IndexOf("SoftwareDistribution",
                    StringComparison.OrdinalIgnoreCase) >= 0;

                if (isWuCache) StopService("wuauserv");

                try
                {
                    var (fb, fd) = await Task.Run(
                        () => Clean(f.Path, f.Strategy, f.Extensions), ct)
                        .ConfigureAwait(false);
                    freed += fb;
                    deleted += fd;
                }
                finally
                {
                    if (isWuCache) StartService("wuauserv");
                }
            }
            return (freed, deleted);
        }

        private List<CleanableFolder> BuildDefinitions(CleaningMode mode)
        {
            var all = new List<CleanableFolder>();

            // SAFE — zero risk, Windows/apps rebuild everything automatically            
            Add(all, "User Temp Files", Temp, "Temp Files", CleaningMode.Safe);
            Add(all, "Windows Temp", P(Win, "Temp"), "Temp Files", CleaningMode.Safe);
            Add(all, "LocalAppData Temp", P(Local, "Temp"), "Temp Files", CleaningMode.Safe);

            AddPattern(all, "Thumbnail Cache", P(Local, "Microsoft", "Windows", "Explorer"),
                        new[] { "thumbcache_*.db", "iconcache_*.db" }, "Cache", CleaningMode.Safe);

            Add(all, "WER User Report Archive", P(Local, "Microsoft", "Windows", "WER", "ReportArchive"), "Crash Reports", CleaningMode.Safe);
            Add(all, "WER User Report Queue", P(Local, "Microsoft", "Windows", "WER", "ReportQueue"), "Crash Reports", CleaningMode.Safe);
            AddPattern(all, "Memory Dump Files", SysDrv, new[] { "*.dmp", "*.mdmp" }, "Crash Reports", CleaningMode.Safe);
            Add(all, "User Crash Dumps", P(Local, "CrashDumps"), "Crash Reports", CleaningMode.Safe);

            Add(all, "Windows Update Downloads", P(Win, "SoftwareDistribution", "Download"), "Windows Update", CleaningMode.Safe);
            Add(all, "Delivery Optimization Cache", P(Win, "SoftwareDistribution", "DeliveryOptimization"), "Windows Update", CleaningMode.Safe);

            Add(all, "DirectX Shader Cache", P(Local, "D3DSCache"), "GPU Cache", CleaningMode.Safe);
            Add(all, "NVIDIA DX Cache", P(Local, "NVIDIA", "DXCache"), "GPU Cache", CleaningMode.Safe);
            Add(all, "NVIDIA GL Cache", P(Local, "NVIDIA", "GLCache"), "GPU Cache", CleaningMode.Safe);
            Add(all, "AMD DX Cache", P(Local, "AMD", "DxCache"), "GPU Cache", CleaningMode.Safe);
            Add(all, "Intel Shader Cache", P(Local, "Intel", "ShaderCache"), "GPU Cache", CleaningMode.Safe);

            AddPattern(all, "Font Cache Files",
                P(Local, "Microsoft", "Windows", "FontCache"),
                new[] { "*.dat" }, "Cache", CleaningMode.Safe);
            AddPattern(all, "Font Cache (System)",
                P(Win, "ServiceProfiles", "LocalService", "AppData", "Local", "FontCache"),
                new[] { "*.dat" }, "Cache", CleaningMode.Safe);

            AddRecycleBinEntries(all);

            foreach (var (name, path) in GetChromiumCaches())
                Add(all, name, path, "Browser Cache", CleaningMode.Safe);
            foreach (var (name, path) in GetGeckoCaches())
                Add(all, name, path, "Browser Cache", CleaningMode.Safe);

            AddPattern(all, "CBS Log", P(Win, "Logs", "CBS"), new[] { "*.log" }, "Logs", CleaningMode.Safe);

            // NORMAL — safe but may slightly slow first app launch after clean
            Add(all, "Prefetch Files", P(Win, "Prefetch"), "System", CleaningMode.Normal);

            Add(all, "Windows Update Logs", P(Win, "Logs", "WindowsUpdate"), "Logs", CleaningMode.Normal);
            Add(all, "Windows DISM Logs", P(Win, "Logs", "DISM"), "Logs", CleaningMode.Normal);
            Add(all, "Windows Setup Logs", P(Win, "Panther"), "Logs", CleaningMode.Normal);
            AddPattern(all, "Setup Root Logs", Win, new[] { "setupact.log", "setuperr.log" }, "Logs", CleaningMode.Normal);

            Add(all, "Recent Documents MRU",
                Environment.GetFolderPath(Environment.SpecialFolder.Recent), "Privacy", CleaningMode.Normal);

            Add(all, "Visual Studio Cache", P(Local, "Microsoft", "VisualStudio"), "Dev Tools", CleaningMode.Normal, selected: false);
            Add(all, "VS Code Cache", P(Roaming, "Code", "Cache"), "Dev Tools", CleaningMode.Normal);
            Add(all, "VS Code CachedData", P(Roaming, "Code", "CachedData"), "Dev Tools", CleaningMode.Normal);
            Add(all, "VS Code CachedExtensions", P(Roaming, "Code", "CachedExtensions"), "Dev Tools", CleaningMode.Normal);
            Add(all, "JetBrains Caches", P(Local, "JetBrains"), "Dev Tools", CleaningMode.Normal, selected: false);
            Add(all, "npm Cache", P(Roaming, "npm-cache"), "Dev Tools", CleaningMode.Normal);
            Add(all, "pip Cache", P(Local, "pip", "cache"), "Dev Tools", CleaningMode.Normal);
            Add(all, "NuGet HTTP Cache", P(Local, "NuGet", "v3-cache"), "Dev Tools", CleaningMode.Normal);
            Add(all, "NuGet Package Cache", P(Local, "NuGet", "Cache"), "Dev Tools", CleaningMode.Normal);
            Add(all, "Gradle Caches", P(Profile, ".gradle", "caches"), "Dev Tools", CleaningMode.Normal, selected: false);

            Add(all, "Microsoft Teams Cache", P(Roaming, "Microsoft", "Teams", "Cache"), "App Cache", CleaningMode.Normal);
            Add(all, "Microsoft Teams Media", P(Roaming, "Microsoft", "Teams", "media-stack"), "App Cache", CleaningMode.Normal);
            Add(all, "Slack Cache", P(Roaming, "Slack", "Cache"), "App Cache", CleaningMode.Normal);
            Add(all, "Slack Code Cache", P(Roaming, "Slack", "Code Cache"), "App Cache", CleaningMode.Normal);
            Add(all, "Discord Cache", P(Roaming, "discord", "Cache"), "App Cache", CleaningMode.Normal);
            Add(all, "Discord Code Cache", P(Roaming, "discord", "Code Cache"), "App Cache", CleaningMode.Normal);
            Add(all, "Spotify Data Cache", P(Local, "Spotify", "Data"), "App Cache", CleaningMode.Normal);
            Add(all, "WhatsApp Cache", P(Roaming, "WhatsApp", "Cache"), "App Cache", CleaningMode.Normal);
            Add(all, "Zoom Cache", P(Roaming, "Zoom", "data", "Zoom"), "App Cache", CleaningMode.Normal);
            Add(all, "OneDrive Logs", P(Local, "Microsoft", "OneDrive", "logs"), "App Cache", CleaningMode.Normal);
            Add(all, "Office File Cache", P(Local, "Microsoft", "Office", "16.0", "OfficeFileCache"), "App Cache", CleaningMode.Normal);
            Add(all, "Office Telemetry", P(Local, "Microsoft", "Office", "16.0", "Telemetry"), "App Cache", CleaningMode.Normal);

            foreach (var path in GetSteamShaderCaches())
                Add(all, "Steam Shader Cache", path, "Gaming", CleaningMode.Normal);

            Add(all, "Windows Installer Patch Cache",
                P(Win, "Installer", "$PatchCache$"), "Windows", CleaningMode.Normal, selected: false);

            // AGGRESSIVE — large space savings, minor caveats noted
            AddTree(all, "Windows.old", P(SysDrv, "Windows.old"), "Old Windows", CleaningMode.Aggressive);
            AddTree(all, "Windows Upgrade Temp (~BT)", P(SysDrv, "$WINDOWS.~BT"), "Old Windows", CleaningMode.Aggressive);
            AddTree(all, "Windows Upgrade Temp (~WS)", P(SysDrv, "$WINDOWS.~WS"), "Old Windows", CleaningMode.Aggressive);

            Add(all, "Downloaded Program Files",
                P(Win, "Downloaded Program Files"), "Windows", CleaningMode.Aggressive);

            Add(all, "Windows Upgrade Logs", P(Win, "Panther"), "Old Windows", CleaningMode.Aggressive);

            return all.Where(f => f.MinMode <= mode).ToList();
        }

        private static void Add(List<CleanableFolder> list, string name, string path,
            string category, CleaningMode mode, bool selected = true, string desc = "")
        {
            list.Add(new CleanableFolder
            {
                Name = name,
                Path = path,
                Category = category,
                MinMode = mode,
                Strategy = CleanStrategy.Folder,
                IsSelected = selected,
                Description = string.IsNullOrEmpty(desc) ? Descriptions.Get(name) : desc
            });
        }

        private static void AddPattern(List<CleanableFolder> list, string name, string path,
            string[] extensions, string category, CleaningMode mode, bool selected = true, string desc = "")
        {
            list.Add(new CleanableFolder
            {
                Name = name,
                Path = path,
                Category = category,
                MinMode = mode,
                Strategy = CleanStrategy.Pattern,
                Extensions = extensions,
                IsSelected = selected,
                Description = string.IsNullOrEmpty(desc) ? Descriptions.Get(name) : desc
            });
        }

        private static void AddTree(List<CleanableFolder> list, string name, string path,
            string category, CleaningMode mode, bool selected = true, string desc = "")
        {
            list.Add(new CleanableFolder
            {
                Name = name,
                Path = path,
                Category = category,
                MinMode = mode,
                Strategy = CleanStrategy.FullTree,
                IsSelected = selected,
                Description = string.IsNullOrEmpty(desc) ? Descriptions.Get(name) : desc
            });
        }

        private static void AddRecycleBinEntries(List<CleanableFolder> all)
        {
            string currentSid = "";
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                currentSid = identity.User?.ToString() ?? "";
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, "AddRecycleBinEntries.GetIdentity");
            }

            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                try
                {
                    string rbRoot = P(drive.RootDirectory.FullName, "$Recycle.Bin");
                    if (!Directory.Exists(rbRoot)) continue;

                    if (!string.IsNullOrEmpty(currentSid))
                    {
                        string userRb = P(rbRoot, currentSid);
                        if (Directory.Exists(userRb))
                        {
                            Add(all, $"Recycle Bin ({drive.Name.TrimEnd('\\')})",
                                userRb, "Recycle Bin", CleaningMode.Safe);
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Log(ex, $"AddRecycleBinEntries.ProcessDrive_{drive.Name.Replace(":\\", "")}");
                }
            }
        }

        private static IEnumerable<(string name, string path)> GetChromiumCaches()
        {
            var defs = new[]
            {
                ("Chrome",       P(Local, "Google", "Chrome",           "User Data")),
                ("Edge",         P(Local, "Microsoft", "Edge",          "User Data")),
                ("Brave",        P(Local, "BraveSoftware", "Brave-Browser", "User Data")),
                ("Vivaldi",      P(Local, "Vivaldi",                    "User Data")),
                ("Opera",        P(Roaming, "Opera Software", "Opera Stable")),
                ("Opera GX",     P(Roaming, "Opera Software", "Opera GX Stable")),
                ("Yandex",       P(Local, "Yandex", "YandexBrowser",    "User Data")),
                ("Chromium",     P(Local, "Chromium",                   "User Data"))
            };

            foreach (var (name, root) in defs)
            {
                if (!Directory.Exists(root)) continue;

                var profiles = new List<string> { "Default" };

                try
                {
                    profiles.AddRange(Directory.GetDirectories(root, "Profile *")
                        .Select(System.IO.Path.GetFileName));
                }
                catch (Exception ex)
                {
                    AppLogger.Log(ex, $"GetChromiumCaches.GetProfiles_{name}");
                }

                foreach (var profile in profiles)
                {
                    foreach (var sub in new[] { "Cache", "Code Cache", "GPUCache" })
                    {
                        string p = P(root, profile, sub);

                        if (Directory.Exists(p))
                        {
                            yield return ($"{name} {sub}", p);
                        }
                    }
                }
            }
        }

        private static IEnumerable<(string name, string path)> GetGeckoCaches()
        {
            var defs = new[]
            {
                ("Firefox",     P(Local,   "Mozilla",    "Firefox",    "Profiles")),
                ("Thunderbird", P(Roaming, "Thunderbird",              "Profiles")),
                ("Waterfox",    P(Roaming, "Waterfox",                 "Profiles")),
                ("LibreWolf",   P(Roaming, "LibreWolf",                "Profiles")),
            };

            foreach (var (name, profilesRoot) in defs)
            {
                if (!Directory.Exists(profilesRoot)) continue;

                IEnumerable<string> profileDirs;
                try
                {
                    profileDirs = Directory.GetDirectories(profilesRoot);
                }
                catch (Exception ex)
                {
                    AppLogger.Log(ex, $"GetGeckoCaches.GetDirectories_{name}");
                    continue;
                }

                foreach (var dir in profileDirs)
                {
                    string cache = P(dir, "cache2");

                    if (Directory.Exists(cache))
                    {
                        yield return ($"{name} Cache", cache);
                    }
                }
            }
        }

        private static IEnumerable<string> GetSteamShaderCaches()
        {
            var candidates = new List<string>
            {P(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam", "steamapps", "shadercache"),};

            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\WOW6432Node\Valve\Steam");

                string path = key?.GetValue("InstallPath")?.ToString();

                if (!string.IsNullOrEmpty(path))
                {
                    candidates.Add(P(path, "steamapps", "shadercache"));
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, "GetSteamShaderCaches.Registry");
            }

            return candidates.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
        }
        private static (long size, int count) GetStats(string path, CleanStrategy strategy, string[] patterns)
        {
            try
            {
                var dir = new DirectoryInfo(path);
                if (!dir.Exists) return (0, 0);

                long size = 0; int count = 0;
                IEnumerable<FileInfo> files;

                if (strategy == CleanStrategy.Pattern && patterns?.Length > 0)
                {
                    files = patterns.SelectMany(p =>
                    {
                        try
                        {
                            return (IEnumerable<FileInfo>)dir.EnumerateFiles(p, SearchOption.AllDirectories);
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Log(ex, "GetStats.EnumeratePattern");
                            return Enumerable.Empty<FileInfo>();
                        }
                    });
                }
                else
                {
                    try
                    {
                        files = dir.EnumerateFiles("*", SearchOption.AllDirectories);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Log(ex, "GetStats.EnumerateAll");
                        return (0, 0);
                    }
                }

                foreach (var f in files)
                {
                    try
                    {
                        size += f.Length;
                        count++;
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Log(ex, "GetStats.FileLength");
                    }
                }

                return (size, count);
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, "GetStats.General");
                return (0, 0);
            }
        }

        private static (long freed, int deleted) Clean(
            string path, CleanStrategy strategy, string[] patterns)
        {
            long freed = 0; int deleted = 0;
            try
            {
                var dir = new DirectoryInfo(path);
                if (!dir.Exists) return (0, 0);

                switch (strategy)
                {
                    case CleanStrategy.FullTree:
                        freed = GetStats(path, CleanStrategy.Folder, null).size;
                        deleted = DeleteTreeContents(dir, deleteRoot: true);
                        break;

                    case CleanStrategy.Pattern:
                        if (patterns?.Length > 0)
                        {
                            foreach (var pattern in patterns)
                            {
                                try
                                {
                                    foreach (var f in dir.EnumerateFiles(pattern, SearchOption.AllDirectories))
                                    {
                                        TryDeleteFile(f, ref freed, ref deleted);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    AppLogger.Log(ex, "Clean.PatternDelete");
                                }
                            }
                        }
                        break;

                    default:
                        foreach (var f in SafeFiles(dir))
                        {
                            TryDeleteFile(f, ref freed, ref deleted);
                        }
                        foreach (var sub in SafeDirs(dir))
                        {
                            TryDeleteDir(sub, ref freed, ref deleted);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, nameof(Clean));
            }
            return (freed, deleted);
        }

        private static int DeleteTreeContents(DirectoryInfo dir, bool deleteRoot)
        {
            int deleted = 0;
            foreach (var f in SafeFiles(dir))
            {
                try
                {
                    f.Attributes = FileAttributes.Normal;
                    f.Delete();
                    deleted++;
                }
                catch (Exception ex)
                {
                    AppLogger.Log(ex, "DeleteTreeContents.FileDelete");
                }
            }
            foreach (var sub in SafeDirs(dir))
            {
                deleted += DeleteTreeContents(sub, deleteRoot: true);
                try
                {
                    sub.Delete(false);
                    deleted++;
                }
                catch (Exception ex)
                {
                    AppLogger.Log(ex, "DeleteTreeContents.SubDirDelete");
                }
            }
            if (deleteRoot)
            {
                try
                {
                    dir.Delete(false);
                }
                catch (Exception ex)
                {
                    AppLogger.Log(ex, "DeleteTreeContents.DirDelete");
                }
            }
            return deleted;
        }

        private static void TryDeleteFile(FileInfo f, ref long freed, ref int deleted)
        {
            try
            {
                long len = f.Length;
                f.Attributes = FileAttributes.Normal;
                f.Delete();
                freed += len;
                deleted++;
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, "TryDeleteFile");
            }
        }

        private static void TryDeleteDir(DirectoryInfo d, ref long freed, ref int deleted)
        {
            try
            {
                foreach (var f in d.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        freed += f.Length;
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Log(ex, "TryDeleteDir.FileLength");
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, "TryDeleteDir.CalculateSize");
            }

            try
            {
                foreach (var f in d.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        f.Attributes = FileAttributes.Normal;
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Log(ex, "TryDeleteDir.SetAttributes");
                    }
                }
                d.Delete(recursive: true);
                deleted++;
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, "TryDeleteDir.DeleteRecursiveFallback");

                foreach (var f in SafeFiles(d))
                {
                    TryDeleteFile(f, ref freed, ref deleted);
                }
                foreach (var sub in SafeDirs(d))
                {
                    TryDeleteDir(sub, ref freed, ref deleted);
                }
            }
        }

        private static IEnumerable<FileInfo> SafeFiles(DirectoryInfo dir)
        {
            try
            {
                return dir.EnumerateFiles("*", SearchOption.TopDirectoryOnly).ToList();
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, "SafeFiles");
                return Enumerable.Empty<FileInfo>();
            }
        }

        private static IEnumerable<DirectoryInfo> SafeDirs(DirectoryInfo dir)
        {
            try
            {
                return dir.EnumerateDirectories().ToList();
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, "SafeDirs");
                return Enumerable.Empty<DirectoryInfo>();
            }
        }

        private static void StopService(string name)
        {
            try
            {
                using var sc = new ServiceController(name);
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, "StopService");
            }
        }

        private static void StartService(string name)
        {
            try
            {
                using var sc = new ServiceController(name);
                if (sc.Status == ServiceControllerStatus.Stopped)
                {
                    sc.Start();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, "StartService");
            }
        }

        private static string P(params string[] parts) => System.IO.Path.Combine(parts);
        internal static class Descriptions
        {
            private static readonly Dictionary<string, string> _map =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    // Temp Files
                    ["User Temp Files"] = "Temporary files created by applications in your user profile. Apps recreate them as needed. Safe to delete anytime.",
                    ["Windows Temp"] = "Temporary files created by Windows and installers in the system Temp folder. Can accumulate GB over time.",
                    ["LocalAppData Temp"] = "Per-user temporary files stored in AppData\\Local\\Temp. Same as %TEMP% for most apps.",

                    // Cache
                    ["Thumbnail Cache"] = "thumbcache_*.db files used by Windows Explorer to display image/video previews. Deleted files leave orphan entries. Explorer rebuilds this automatically.",
                    ["Font Cache Files"] = "Binary cache of loaded fonts stored by the FontCache service. Rebuilt on next Windows startup. Speeds up font rendering.",
                    ["Font Cache (System)"] = "System-level font cache used by the LocalService account. Same as user font cache but for system processes.",
                    ["DirectX Shader Cache"] = "Compiled DirectX shaders stored by games and apps to avoid recompiling on every launch. GPU driver rebuilds on first use.",
                    ["NVIDIA DX Cache"] = "DirectX shader cache maintained by NVIDIA drivers. Rebuilt automatically by the driver on next game/app launch.",
                    ["NVIDIA GL Cache"] = "OpenGL shader cache maintained by NVIDIA drivers. Rebuilt automatically. Deleting may cause a one-time stutter on first launch.",
                    ["AMD DX Cache"] = "DirectX shader cache maintained by AMD drivers. Rebuilt automatically on next game/app launch.",
                    ["Intel Shader Cache"] = "Shader cache maintained by Intel integrated graphics drivers. Rebuilt automatically.",

                    // Crash Reports
                    ["WER User Report Archive"] = "Windows Error Reporting — crash reports that were already sent to Microsoft. Contains minidumps and diagnostic data. No functional value after submission.",
                    ["WER User Report Queue"] = "Windows Error Reporting — crash reports queued for upload but not yet sent. Deleting prevents upload; no impact on system stability.",
                    ["Memory Dump Files"] = "*.dmp files created when Windows or an app crashes. Useful for advanced debugging only. Can be several GB. Safe to delete.",
                    ["User Crash Dumps"] = "Application crash dump files stored in AppData\\Local\\CrashDumps. Created when apps crash and a debugger is attached.",

                    // Windows Update
                    ["Windows Update Downloads"] = "Downloaded Windows Update packages waiting to be installed, or already installed. Windows re-downloads if needed. Can free several GB.",
                    ["Delivery Optimization Cache"] = "Files downloaded by Delivery Optimization (Windows Update peer-to-peer). Used to share updates with other PCs on your network. Rebuilt automatically.",

                    // Logs
                    ["CBS Log"] = "Component-Based Servicing log — records Windows component install/update activity. Used for diagnosing update failures. Safe to delete.",
                    ["Windows Update Logs"] = "Logs from Windows Update service. Used to diagnose update problems. No functional impact when deleted.",
                    ["Windows DISM Logs"] = "Deployment Image Servicing and Management logs. Useful only when troubleshooting Windows image corruption.",
                    ["Windows Setup Logs"] = "Logs from Windows installation or major feature updates (stored in Windows\\Panther). Safe to delete after OS is stable.",
                    ["Setup Root Logs"] = "setupact.log and setuperr.log in the Windows root — created during Windows setup and feature updates. Safe to delete.",

                    // Privacy
                    ["Recent Documents MRU"] = "Most Recently Used list — shortcuts to recently opened files shown in File Explorer and Office apps. Deleting clears your recent file history.",

                    // Recycle Bin
                    ["Recycle Bin (C:)"] = "Files you deleted and moved to the Recycle Bin on drive C:. Only your user account's bin is cleared — other users are not affected.",
                    ["Recycle Bin (D:)"] = "Files you deleted and moved to the Recycle Bin on drive D:. Only your user account's bin is cleared.",
                    ["Recycle Bin (E:)"] = "Files you deleted and moved to the Recycle Bin on drive E:. Only your user account's bin is cleared.",

                    // System
                    ["Prefetch Files"] = "Windows preloads frequently used programs at startup to speed up launch times. Deleting causes a one-time slowdown; Windows rebuilds within ~10 minutes of normal use.",

                    // Dev Tools
                    ["Visual Studio Cache"] = "IntelliSense databases, component model caches and project caches created by Visual Studio. Rebuilt on next VS startup (may take a few minutes).",
                    ["VS Code Cache"] = "UI and extension render cache used by Visual Studio Code (Electron-based). Rebuilt on next VS Code launch.",
                    ["VS Code CachedData"] = "Compiled JavaScript V8 bytecode cache for VS Code's core scripts. Speeds up startup; rebuilt automatically.",
                    ["VS Code CachedExtensions"] = "Cached metadata for installed VS Code extensions. Rebuilt on next launch.",
                    ["JetBrains Caches"] = "Index, system and plugin caches for JetBrains IDEs (IntelliJ, Rider, PyCharm etc.). Rebuilt on next IDE startup — may take a few minutes.",
                    ["npm Cache"] = "Node.js package manager cache. Stores downloaded packages to avoid re-downloading. Safe to delete; npm re-downloads as needed.",
                    ["pip Cache"] = "Python package manager cache. Stores downloaded wheels/packages. pip re-downloads on next install.",
                    ["NuGet HTTP Cache"] = "NuGet v3 HTTP response cache (package metadata and index). Rebuilt on next package restore.",
                    ["NuGet Package Cache"] = "Downloaded NuGet packages cached locally. Visual Studio/dotnet CLI re-downloads from NuGet.org if missing.",
                    ["Gradle Caches"] = "Downloaded dependencies and compiled scripts cached by Gradle (Android/Java build tool). Can be very large. Gradle re-downloads on next build.",

                    // App Cache
                    ["Microsoft Teams Cache"] = "UI cache, images and assets cached by Microsoft Teams. Teams rebuilds on next launch. Does not affect messages, files or settings.",
                    ["Microsoft Teams Media"] = "Media stack files used by Teams for calls and meetings. Rebuilt automatically. Does not affect contacts or call history.",
                    ["Slack Cache"] = "Images, avatars and UI assets cached by the Slack desktop app. Rebuilt on next Slack launch.",
                    ["Slack Code Cache"] = "Compiled JavaScript V8 bytecode cache for Slack (Electron-based). Rebuilt on next Slack launch.",
                    ["Discord Cache"] = "Images, GIFs and media cached by the Discord desktop app. Rebuilt on next Discord launch. Does not affect messages or servers.",
                    ["Discord Code Cache"] = "Compiled JavaScript V8 bytecode cache for Discord (Electron-based). Rebuilt on next Discord launch.",
                    ["Spotify Data Cache"] = "Album art, UI assets and song metadata cached by Spotify. Rebuilt as you browse. Does not affect your playlists or library.",
                    ["WhatsApp Cache"] = "Images and media cached by the WhatsApp desktop app. Rebuilt on next launch. Does not affect your messages.",
                    ["Zoom Cache"] = "UI assets and meeting data cached by the Zoom desktop app. Rebuilt on next Zoom launch.",
                    ["OneDrive Logs"] = "Diagnostic and sync logs generated by OneDrive. No functional impact when deleted. OneDrive recreates them.",
                    ["Office File Cache"] = "Locally cached copies of cloud Office files (SharePoint/OneDrive). Rebuilt when you re-open the files.",
                    ["Office Telemetry"] = "Usage and performance telemetry data collected by Microsoft Office. Sent to Microsoft periodically. Deleting has no effect on Office functionality.",
                    ["Steam Shader Cache"] = "Pre-compiled Vulkan/DirectX shader cache for Steam games. Rebuilt by Steam on first game launch after deletion — may cause a one-time stutter.",

                    // Windows
                    ["Windows Installer Patch Cache"] = "$PatchCache$ stores copies of patched files for rollback. Deleting prevents patch rollback but saves 100MB-2GB. Windows re-downloads if needed.",

                    // Old Windows
                    ["Windows.old"] = "Previous Windows installation kept by the upgrade process. Allows rolling back to the previous OS version. Windows auto-deletes it after 30 days. Can free 10-30 GB.",
                    ["Windows Upgrade Temp (~BT)"] = "$WINDOWS.~BT contains temporary files used during the Windows upgrade process. Safe to delete after upgrade is complete.",
                    ["Windows Upgrade Temp (~WS)"] = "$WINDOWS.~WS is another temporary folder created during Windows upgrade. Safe to delete after upgrade completes.",
                    ["Downloaded Program Files"] = "Legacy folder for ActiveX controls and Java applets downloaded by Internet Explorer. Empty on virtually all modern systems.",
                    ["Windows Upgrade Logs"] = "Windows\\Panther contains setup and upgrade log files from the last Windows installation or feature update.",

                    // Browser Cache (dynamic names)
                    ["Chrome Cache"] = "Web content cached by Google Chrome (HTML, CSS, images, scripts). Deleting clears browsing cache — pages may load slightly slower on first visit.",
                    ["Edge Cache"] = "Web content cached by Microsoft Edge. Rebuilt as you browse. Does not affect bookmarks, passwords or extensions.",
                    ["Brave Cache"] = "Web content cached by Brave Browser. Does not affect bookmarks, passwords or extensions.",
                    ["Firefox Cache"] = "Web content cached by Mozilla Firefox. Does not affect bookmarks, passwords or extensions.",
                    ["Opera Cache"] = "Web content cached by Opera Browser. Does not affect bookmarks or settings.",
                    ["Opera GX Cache"] = "Web content cached by Opera GX. Does not affect bookmarks or gaming features.",
                    ["Vivaldi Cache"] = "Web content cached by Vivaldi Browser. Does not affect bookmarks or settings.",
                    ["Thunderbird Cache"] = "Email content and attachment cache stored by Mozilla Thunderbird. Does not affect emails or account settings.",
                };

            public static string Get(string name)
            {
                if (_map.TryGetValue(name, out string desc)) return desc;

                foreach (var kvp in _map)
                    if (name.StartsWith(kvp.Key.Split('(')[0].Trim(),
                        StringComparison.OrdinalIgnoreCase))
                        return kvp.Value;

                return string.Empty;
            }
        }
    }
}