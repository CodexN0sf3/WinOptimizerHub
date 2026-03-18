using System;
using System.ServiceProcess;
using System.Threading.Tasks;
using WinOptimizerHub.Helpers;

namespace WinOptimizerHub.Services
{
    public class FontCacheCleanerService
    {
        private static readonly string[] FontCachePaths = {
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Windows", "FontCache"),
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "ServiceProfiles", "LocalService", "AppData", "Local", "FontCache"),
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "ServiceProfiles", "LocalService", "AppData", "Local", "Microsoft", "Windows", "FontCache")
        };

        private static readonly string[] FontCacheServiceNames = new[]
        {
            "FontCache", "FontCache3.0.0.0"
        };

        public (long sizeBytes, int fileCount) GetFontCacheInfo()
        {
            long total = 0; int files = 0;
            foreach (var path in FontCachePaths)
            {
                if (!System.IO.Directory.Exists(path)) continue;
                foreach (var f in System.IO.Directory.EnumerateFiles(path, "*.dat", System.IO.SearchOption.AllDirectories))
                {
                    try { total += new System.IO.FileInfo(f).Length; files++; }
                    catch (Exception ex) { AppLogger.Log(ex, nameof(GetFontCacheInfo)); }
                }
            }
            return (total, files);
        }

        public async Task<bool> RebuildFontCacheAsync(IProgress<string> progress = null)
        {
            return await Task.Run(() =>
            {
                bool success = false;
                try
                {
                    progress?.Report("Stopping font cache services...");
                    foreach (var svcName in FontCacheServiceNames)
                    {
                        try
                        {
                            using var svc = new ServiceController(svcName);
                            if (svc.Status == ServiceControllerStatus.Running)
                            {
                                svc.Stop();
                                svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                            }
                        }
                        catch (Exception ex) { AppLogger.Log(ex, "FontCache.StopService"); }
                    }

                    progress?.Report("Deleting font cache files...");
                    foreach (var path in FontCachePaths)
                    {
                        if (!System.IO.Directory.Exists(path)) continue;
                        foreach (var f in System.IO.Directory.GetFiles(path, "*.dat"))
                        {
                            try { System.IO.File.Delete(f); }
                            catch (Exception ex) { AppLogger.Log(ex, "FontCache.DeleteFile"); }
                        }
                    }

                    progress?.Report("Restarting font cache services...");
                    foreach (var svcName in FontCacheServiceNames)
                    {
                        try
                        {
                            using var svc = new ServiceController(svcName);
                            svc.Start();
                            svc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                        }
                        catch (Exception ex) { AppLogger.Log(ex, "FontCache.StartService"); }
                    }

                    success = true;
                }
                catch (Exception ex) { progress?.Report($"Error: {ex.Message}"); }
                return success;
            });
        }
    }
}
