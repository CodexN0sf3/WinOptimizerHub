using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using WinOptimizerHub.Helpers;

namespace WinOptimizerHub.Services
{
    public class SystemToolsService
    {
        public async Task<string> RunSfcAsync(
            IProgress<string> progress = null, CancellationToken ct = default)
        {
            progress?.Report("Starting TrustedInstaller service...");
            await Task.Run(() =>
            {
                try
                {
                    using var sc = new ServiceController("TrustedInstaller");
                    if (sc.Status != ServiceControllerStatus.Running)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
                    }
                }
                catch (Exception ex) { AppLogger.Log(ex); }
            }, ct);

            progress?.Report("Running System File Checker (this may take several minutes)...");

            return await Task.Run(() => CommandHelper.RunWithProgressRaw(
                "sfc.exe", "/scannow",
                line => progress?.Report(line),
                timeoutMs: 15 * 60 * 1000), ct);
        }

        public async Task<string> RunDismHealthCheckAsync(
            IProgress<string> progress = null, CancellationToken ct = default)
        {
            progress?.Report("Running DISM /CheckHealth...");
            return await Task.Run(() => CommandHelper.RunWithProgress(
                "dism.exe", "/Online /Cleanup-Image /CheckHealth /NoRestart",
                line => { if (!string.IsNullOrWhiteSpace(line)) progress?.Report(line); },
                timeoutMs: 10 * 60 * 1000), ct);
        }

        public async Task<string> RunDismRestoreHealthAsync(
            IProgress<string> progress = null, CancellationToken ct = default)
        {
            progress?.Report("Running DISM /RestoreHealth (requires internet, may take 15-30 min)...");
            return await Task.Run(() => CommandHelper.RunWithProgress(
                "dism.exe", "/Online /Cleanup-Image /RestoreHealth /NoRestart",
                line => { if (!string.IsNullOrWhiteSpace(line)) progress?.Report(line); },
                timeoutMs: 60 * 60 * 1000), ct);
        }

        public async Task<string> RunDefragAsync(
            string drive,
            IProgress<string> progress = null, CancellationToken ct = default)
        {
            progress?.Report($"Analyzing {drive} for fragmentation...");
            return await Task.Run(() => CommandHelper.RunWithProgress(
                "defrag.exe", $"{drive} /A /V",
                line => { if (!string.IsNullOrWhiteSpace(line)) progress?.Report(line); },
                timeoutMs: 30 * 60 * 1000), ct);
        }

        public void OpenSystemProperties() =>
            Process.Start(new ProcessStartInfo("sysdm.cpl") { UseShellExecute = true });
        public void OpenDeviceManager() =>
            Process.Start(new ProcessStartInfo("devmgmt.msc") { UseShellExecute = true });
        public void OpenDiskManagement() =>
            Process.Start(new ProcessStartInfo("diskmgmt.msc") { UseShellExecute = true });
        public void OpenTaskManager() =>
            Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
        public void OpenResourceMonitor() =>
            Process.Start(new ProcessStartInfo("perfmon.exe")
            { Arguments = "/res", UseShellExecute = true });
        public void OpenEventViewer() =>
            Process.Start(new ProcessStartInfo("eventvwr.msc") { UseShellExecute = true });
        public void OpenGroupPolicyEditor() =>
            Process.Start(new ProcessStartInfo("gpedit.msc") { UseShellExecute = true });
        public void OpenRegedit() =>
            Process.Start(new ProcessStartInfo("regedit.exe") { UseShellExecute = true });
    }
}
