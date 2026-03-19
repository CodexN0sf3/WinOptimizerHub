using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WinOptimizerHub.Helpers;

namespace WinOptimizerHub.Services
{
    public class RAMOptimizerService
    {

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetSystemFileCacheSize(
            UIntPtr MinimumFileCacheSize, UIntPtr MaximumFileCacheSize, uint Flags);

        public (double usedGb, double totalGb, double freeGb, double percent) GetRamInfo()
        {
            var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
            if (!GlobalMemoryStatusEx(ref ms)) return (0, 0, 0, 0);

            double total = ms.ullTotalPhys / 1073741824.0;
            double free = ms.ullAvailPhys / 1073741824.0;
            double used = total - free;
            double pct = ms.dwMemoryLoad;
            return (used, total, free, pct);
        }

        public async Task<long> OptimizeAsync(
            IEnumerable<ProcessMemoryInfo> selectedProcesses = null,
            IProgress<string> progress = null,
            CancellationToken ct = default)
        {
            var (beforeUsed, _, _, _) = GetRamInfo();

            progress?.Report("Clearing working sets...");
            await Task.Run(() => ClearWorkingSets(selectedProcesses, progress), ct).ConfigureAwait(false);

            if (ct.IsCancellationRequested) return 0L;

            progress?.Report("Flushing file system cache...");
            await Task.Run(() => FlushFileSystemCache(), ct).ConfigureAwait(false);

            await Task.Delay(800, ct).ConfigureAwait(false);

            var (afterUsed, _, _, _) = GetRamInfo();
            long freedMB = (long)((beforeUsed - afterUsed) * 1024);
            return Math.Max(0, freedMB);
        }

        private void ClearWorkingSets(
            IEnumerable<ProcessMemoryInfo> selected,
            IProgress<string> progress)
        {
            HashSet<int> targetPids = null;
            if (selected != null)
            {
                targetPids = new HashSet<int>(selected.Select(p => p.Pid));
            }

            var systemNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "System", "smss", "csrss", "wininit", "winlogon", "lsass",
                "services", "svchost", "dwm", "fontdrvhost", "audiodg",
                "MsMpEng", "SecurityHealthService", "spoolsv", "WinOptimizerHub"
            };

            int myPid = Process.GetCurrentProcess().Id;
            int cleared = 0;

            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (proc.Id == myPid) continue;

                    if (targetPids != null)
                    {
                        if (!targetPids.Contains(proc.Id)) continue;
                    }
                    else
                    {
                        if (systemNames.Contains(proc.ProcessName)) continue;
                    }

                    EmptyWorkingSet(proc.Handle);
                    cleared++;

                    if (cleared % 10 == 0)
                        progress?.Report($"Cleared {cleared} processes...");
                }
                catch  { AppLogger.Log(new Exception("Unhandled"), nameof(AppLogger)); }
                finally { proc.Dispose(); }
            }

            progress?.Report($"Working sets cleared: {cleared} processes");
        }

        private void FlushFileSystemCache()
        {
            try { SetSystemFileCacheSize(UIntPtr.Zero, UIntPtr.Zero, 0); }
            catch (Exception ex) { AppLogger.Log(ex, nameof(FlushFileSystemCache)); }
        }

        public List<ProcessMemoryInfo> GetTopMemoryProcesses(int top = 15)
        {
            var (_, totalGb, _, _) = GetRamInfo();
            double totalMB = totalGb * 1024;

            var systemNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "System", "smss", "csrss", "wininit", "winlogon", "lsass",
                "services", "fontdrvhost", "audiodg", "MsMpEng",
                "SecurityHealthService", "Registry", "Idle"
            };

            return Process.GetProcesses()
                .Select(p =>
                {
                    try
                    {
                        double memMB = p.WorkingSet64 / 1048576.0;
                        bool isSystem = systemNames.Contains(p.ProcessName)
                                      || p.SessionId == 0;
                        return new ProcessMemoryInfo(p.ProcessName, p.Id, memMB, totalMB, isSystem);
                    }
                    catch { return null; }
                    finally { p.Dispose(); }
                })
                .Where(x => x != null && x.MemMB > 1)
                .OrderByDescending(x => x.MemMB)
                .Take(top)
                .ToList();
        }
    }
}