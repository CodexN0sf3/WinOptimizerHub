using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using WinOptimizerHub.Helpers;

namespace WinOptimizerHub.Services
{
    public class WindowsUpdateCleanupService
    {
        private const int DismAnalyzeTimeoutMs = 20 * 60 * 1000;
        private const int DismCleanupTimeoutMs = 60 * 60 * 1000;

        public async Task<(long sizeBytes, string info)> GetWinSxSSizeAsync(
            IProgress<string> progress = null,
            CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var sb = new System.Text.StringBuilder();

                    CommandHelper.RunWithProgress(
                        "dism.exe",
                        "/Online /Cleanup-Image /AnalyzeComponentStore /NoRestart",
                        line =>
                        {
                            if (string.IsNullOrWhiteSpace(line)) return;
                            sb.AppendLine(line);
                            progress?.Report(line);
                        },
                        timeoutMs: DismAnalyzeTimeoutMs);

                    long size = ParseDismSize(sb.ToString());

                    if (size == 0)
                    {
                        try
                        {
                            string winSxS = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "WinSxS");
                            if (Directory.Exists(winSxS))
                            {
                                size = new DirectoryInfo(winSxS)
                                    .GetFiles("*", SearchOption.TopDirectoryOnly)
                                    .Sum(f => { try { return f.Length; } catch { return 0L; } });
                            }
                        }
                        catch { }
                    }

                    return (size, sb.ToString());
                }
                catch (Exception ex) { return (0, ex.Message); }
            }, ct);
        }

        public async Task<string> CleanComponentStoreAsync(
            IProgress<string> progress = null, CancellationToken ct = default,
            int[] stepsToRun = null)
        {
            var log = new System.Text.StringBuilder();
            void Report(string msg) { progress?.Report(msg); log.AppendLine(msg); }
            bool ShouldRun(int step) => stepsToRun == null || Array.IndexOf(stepsToRun ?? new int[0], step) >= 0;

            long freed2 = 0, freed3 = 0, freed4 = 0, freed5 = 0, freed6 = 0, freed7 = 0;

            if (ShouldRun(1))
            {
                Report("► [1/7] DISM Component Store Cleanup (/StartComponentCleanup /ResetBase)...");
                Report("  This may take several minutes, please wait.");
                string dismResult = await Task.Run(() =>
                    CommandHelper.RunSync("dism.exe", "/Online /Cleanup-Image /StartComponentCleanup /ResetBase /NoRestart",
                        timeoutMs: DismCleanupTimeoutMs), ct);
                Report(dismResult);
                Report("");
            }

            if (ct.IsCancellationRequested) return log.ToString();

            if (ShouldRun(2))
            {
                Report("► [2/7] Cleaning Windows Update download cache...");
                freed2 = await Task.Run(() => CleanUpdateCache(Report), ct);
                Report($"  Freed: {FormatHelper.FormatSize(freed2)}");
                Report("");
            }

            if (ct.IsCancellationRequested) return log.ToString();

            if (ShouldRun(3))
            {
                Report("► [3/7] Cleaning Delivery Optimization cache...");
                freed3 = await Task.Run(() =>
                {
                    long total = 0;
                    string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                    StopService("DoSvc");
                    try
                    {
                        total += DeleteFiles(Path.Combine(win, "ServiceProfiles", "NetworkService",
                            "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache"),
                            "*", SearchOption.AllDirectories);
                        total += DeleteFiles(Path.Combine(win, "SoftwareDistribution", "DeliveryOptimization"),
                            "*", SearchOption.AllDirectories);
                        total += DeleteFiles(Path.Combine(win, "ServiceProfiles", "NetworkService",
                            "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Logs"),
                            "*", SearchOption.AllDirectories);
                    }
                    finally { StartService("DoSvc"); }
                    return total;
                }, ct);
                Report($"  Freed: {FormatHelper.FormatSize(freed3)}");
                Report("");
            }

            if (ct.IsCancellationRequested) return log.ToString();

            if (ShouldRun(4))
            {
                Report("► [4/7] Removing Windows upgrade logs...");
                freed4 = await Task.Run(() =>
                {
                    long total = 0;
                    string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                    total += DeleteFiles(Path.Combine(win, "Logs", "CBS"), "*.log", SearchOption.TopDirectoryOnly);
                    total += DeleteFiles(Path.Combine(win, "Panther"), "*", SearchOption.AllDirectories);
                    foreach (var drive in DriveInfo.GetDrives())
                    {
                        if (drive.DriveType != DriveType.Fixed) continue;
                        total += DeleteFiles(Path.Combine(drive.Name, "$Windows.~BT"), "*", SearchOption.AllDirectories);
                        total += DeleteFiles(Path.Combine(drive.Name, "$Windows.~WS"), "*", SearchOption.AllDirectories);
                        total += DeleteFiles(Path.Combine(drive.Name, "Windows.old"), "*", SearchOption.AllDirectories);
                    }
                    return total;
                }, ct);
                Report($"  Freed: {FormatHelper.FormatSize(freed4)}");
                Report("");
            }

            if (ct.IsCancellationRequested) return log.ToString();

            if (ShouldRun(5))
            {
                Report("► [5/7] Cleaning Windows Error Reports...");
                freed5 = await Task.Run(() =>
                {
                    long total = 0;
                    string sysWer = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "Microsoft", "Windows", "WER");
                    total += DeleteFiles(Path.Combine(sysWer, "ReportQueue"), "*", SearchOption.AllDirectories);
                    total += DeleteFiles(Path.Combine(sysWer, "ReportArchive"), "*", SearchOption.AllDirectories);
                    foreach (var userDir in GetUserDirectories())
                        total += DeleteFiles(Path.Combine(userDir, "AppData", "Local", "Microsoft", "Windows", "WER"),
                            "*", SearchOption.AllDirectories);
                    return total;
                }, ct);
                Report($"  Freed: {FormatHelper.FormatSize(freed5)}");
                Report("");
            }

            if (ct.IsCancellationRequested) return log.ToString();

            if (ShouldRun(6))
            {
                Report("► [6/7] Cleaning DirectX Shader Cache...");
                freed6 = await Task.Run(() =>
                {
                    long total = 0;
                    foreach (var userDir in GetUserDirectories())
                        total += DeleteFiles(Path.Combine(userDir, "AppData", "Local", "D3DSCache"),
                            "*", SearchOption.AllDirectories);
                    return total;
                }, ct);
                Report($"  Freed: {FormatHelper.FormatSize(freed6)}");
                Report("");
            }

            if (ct.IsCancellationRequested) return log.ToString();

            if (ShouldRun(7))
            {
                Report("► [7/7] Cleaning thumbnail cache...");
                freed7 = await Task.Run(() =>
                {
                    long total = 0;
                    foreach (var userDir in GetUserDirectories())
                        total += DeleteFiles(
                            Path.Combine(userDir, "AppData", "Local", "Microsoft", "Windows", "Explorer"),
                            "thumbcache_*.db", SearchOption.TopDirectoryOnly);
                    return total;
                }, ct);
                Report($"  Freed: {FormatHelper.FormatSize(freed7)}");
                Report("");
            }

            long totalFreed = freed2 + freed3 + freed4 + freed5 + freed6 + freed7;
            if (totalFreed > 0 || ShouldRun(1))
            {
                Report("─────────────────────────────────────");
                Report($"✔ Cleanup complete. Additional space freed (excl. DISM): {FormatHelper.FormatSize(totalFreed)}");
            }

            return log.ToString();
        }


        public async Task<(long freed, string info)> CleanUpdateCacheAsync(
            IProgress<string> progress = null, CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    progress?.Report("Stopping Windows Update services...");
                    StopService("wuauserv");
                    StopService("bits");

                    string datastore = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                        "SoftwareDistribution", "Download");
                    long freed = GetFolderSize(datastore);

                    progress?.Report("Deleting update cache files...");
                    DeleteFiles(datastore, "*", SearchOption.AllDirectories);

                    progress?.Report("Restarting Windows Update services...");
                    StartService("bits");
                    StartService("wuauserv");

                    return (freed, "Windows Update cache cleaned successfully.");
                }
                catch (Exception ex)
                {
                    StartService("bits");
                    StartService("wuauserv");
                    return (0L, $"Error: {ex.Message}");
                }
            }, ct);
        }

        private long CleanUpdateCache(Action<string> log)
        {
            try
            {
                StopService("wuauserv");
                StopService("bits");
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "SoftwareDistribution", "Download");
                long freed = GetFolderSize(path);
                DeleteFiles(path, "*", SearchOption.AllDirectories);
                StartService("bits");
                StartService("wuauserv");
                return freed;
            }
            catch (Exception ex)
            {
                log?.Invoke($"  Warning: {ex.Message}");
                StartService("bits");
                StartService("wuauserv");
                return 0;
            }
        }

        private static long DeleteFiles(string folder, string pattern, SearchOption option)
        {
            if (!Directory.Exists(folder)) return 0;
            long freed = 0;
            try
            {
                foreach (var f in Directory.GetFiles(folder, pattern, option))
                {
                    try { freed += new FileInfo(f).Length; File.Delete(f); }
                    catch (Exception ex) { AppLogger.Log(ex, nameof(DeleteFiles)); }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(DeleteFiles)); }
            return freed;
        }

        private static IEnumerable<string> GetUserDirectories()
        {
            string usersRoot = Path.GetPathRoot(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows))
                + "Users";
            if (!Directory.Exists(usersRoot)) yield break;
            foreach (var dir in Directory.GetDirectories(usersRoot))
            {
                string name = Path.GetFileName(dir);
                if (name.Equals("Public", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Default.", StringComparison.OrdinalIgnoreCase)) continue;
                yield return dir;
            }
        }

        private static long GetFolderSize(string path)
        {
            if (!Directory.Exists(path)) return 0;
            long size = 0;
            foreach (var f in new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories))
                try { size += f.Length; }
                catch (Exception ex) { AppLogger.Log(ex, nameof(GetFolderSize)); }
            return size;
        }

        private static void StopService(string name)
        {
            try
            {
                using var sc = new ServiceController(name);
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, $"WinUpdate.StopService:{name}"); }
        }

        private static void StartService(string name)
        {
            try
            {
                using var sc = new ServiceController(name);
                if (sc.Status != ServiceControllerStatus.Running) sc.Start();
            }
            catch (Exception ex) { AppLogger.Log(ex, $"WinUpdate.StartService:{name}"); }
        }

        private static long ParseDismSize(string dismOutput)
        {
            foreach (var line in dismOutput.Split('\n'))
            {
                if (line.IndexOf("Component Store Size", StringComparison.OrdinalIgnoreCase) < 0) continue;
                int colon = line.LastIndexOf(':');
                if (colon < 0) continue;
                string val = line.Substring(colon + 1).Trim();

                var parts = val.Split(' ');
                if (parts.Length < 2) continue;
                string numStr = parts[0].Replace(",", ".");
                if (!double.TryParse(numStr,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double num)) continue;
                string unit = parts[1].ToUpperInvariant();
                return unit switch
                {
                    "GB" => (long)(num * 1024 * 1024 * 1024),
                    "MB" => (long)(num * 1024 * 1024),
                    "KB" => (long)(num * 1024),
                    _ => (long)num
                };
            }
            return 0;
        }

        public async Task<Dictionary<string, long>> GetCleanupSizesAsync(CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var sizes = new Dictionary<string, long>();
                string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

                sizes["Windows Update Cache"] = GetFolderSize(
                    Path.Combine(win, "SoftwareDistribution", "Download"));

                sizes["Delivery Optimization"] =
                    GetFolderSize(Path.Combine(win, "SoftwareDistribution", "DeliveryOptimization")) +
                    GetFolderSize(Path.Combine(win, "ServiceProfiles", "NetworkService",
                        "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache"));

                sizes["Windows Upgrade Logs"] =
                    GetFolderSize(Path.Combine(win, "Panther")) +
                    GetFolderSize(Path.Combine(win, "Logs", "CBS"));

                sizes["Windows Error Reports"] =
                    GetFolderSize(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "Microsoft", "Windows", "WER", "ReportArchive")) +
                    GetFolderSize(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "Microsoft", "Windows", "WER", "ReportQueue"));

                sizes["DirectX Shader Cache"] = GetUserDirectories()
                    .Sum(u => GetFolderSize(Path.Combine(u, "AppData", "Local", "D3DSCache")));

                sizes["Thumbnail Cache"] = GetUserDirectories()
                    .Sum(u => GetFolderSize(Path.Combine(u, "AppData", "Local",
                        "Microsoft", "Windows", "Explorer")));

                return sizes;
            }, ct);
        }

        public async Task<string> RunWindowsUpdateDismCleanupAsync(
            bool resetBase = false,
            IProgress<string> progress = null,
            CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var log = new System.Text.StringBuilder();
                void Report(string msg) { progress?.Report(msg); log.AppendLine(msg); }

                Report("► Stopping Windows Update services...");
                StopService("wuauserv");
                StopService("bits");
                StopService("cryptsvc");
                Report("  Services stopped.");
                Report("");

                Report("► Cleaning Windows Update download cache (SoftwareDistribution\\Download)...");
                long freed = 0;
                try
                {
                    string sdPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                        "SoftwareDistribution", "Download");
                    freed = GetFolderSize(sdPath);
                    DeleteFiles(sdPath, "*", SearchOption.AllDirectories);
                    Report($"  Freed: {FormatHelper.FormatSize(freed)}");
                }
                catch (Exception ex) { Report($"  Warning: {ex.Message}"); }
                Report("");

                Report("► Restarting Windows Update services...");
                StartService("cryptsvc");
                StartService("bits");
                StartService("wuauserv");
                Report("  Services restarted.");
                Report("");

                Report("► DISM /Cleanup-Image /SPSuperseded (removes superseded Service Pack backups)...");
                Report("  This may take several minutes...");
                string spResult = CommandHelper.RunSync(
                    "dism.exe",
                    "/Online /Cleanup-Image /SPSuperseded /NoRestart",
                    timeoutMs: DismCleanupTimeoutMs);
                Report(spResult);
                Report("");

                if (ct.IsCancellationRequested) return log.ToString();

                string dismArgs = resetBase
                    ? "/Online /Cleanup-Image /StartComponentCleanup /ResetBase /NoRestart"
                    : "/Online /Cleanup-Image /StartComponentCleanup /NoRestart";

                Report(resetBase
                    ? "► DISM /StartComponentCleanup /ResetBase (irreversible — removes rollback data)..."
                    : "► DISM /StartComponentCleanup (removes superseded components)...");
                Report("  This may take 5-15 minutes...");

                string cleanResult = CommandHelper.RunSync(
                    "dism.exe", dismArgs, timeoutMs: DismCleanupTimeoutMs);
                Report(cleanResult);
                Report("");

                Report("─────────────────────────────────────");
                Report($"✔ Windows Update cleanup complete. Download cache freed: {FormatHelper.FormatSize(freed)}");

                return log.ToString();
            }, ct);
        }

    }
}