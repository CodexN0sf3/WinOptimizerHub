using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;

namespace WinOptimizerHub.Services
{
    public class SSDTweakerService
    {
        public async Task<List<SSDTweak>> GetSSDStatusAsync()
        {
            return await Task.Run(() =>
            {
                var tweaks = new List<SSDTweak>();

                tweaks.Add(new SSDTweak
                {
                    Name = "Disable Superfetch (SysMain)",
                    Description = "SysMain pre-loads apps into RAM — useful on HDD, causes unnecessary writes on SSD",
                    Category = "Performance",
                    IsCurrentlyOptimal = IsServiceDisabledOrManual("SysMain")
                });

                tweaks.Add(new SSDTweak
                {
                    Name = "Optimize Prefetch for SSD",
                    Description = "Set Prefetch to boot-only mode (1) instead of fully disabled — keeps boot fast without excess writes",
                    Category = "Performance",
                    IsCurrentlyOptimal = GetRegDword(
                        @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters",
                        "EnablePrefetcher") == 1
                });

                tweaks.Add(new SSDTweak
                {
                    Name = "Disable 8.3 Filename Creation",
                    Description = "NTFS short filename (8.3) generation adds write overhead — safe to disable on modern systems",
                    Category = "Performance",
                    IsCurrentlyOptimal = GetRegDword(
                        @"SYSTEM\CurrentControlSet\Control\FileSystem",
                        "NtfsDisable8dot3NameCreation") == 1
                });

                tweaks.Add(new SSDTweak
                {
                    Name = "Disable Last Access Timestamp",
                    Description = "NTFS records last access time on every file read — disabling reduces write amplification",
                    Category = "Performance",
                    IsCurrentlyOptimal = GetRegDword(
                        @"SYSTEM\CurrentControlSet\Control\FileSystem",
                        "NtfsDisableLastAccessUpdate") >= 1
                });

                tweaks.Add(new SSDTweak
                {
                    Name = "Enable Write Caching",
                    Description = "Disk write caching buffers writes in RAM first — improves SSD throughput",
                    Category = "Performance",
                    IsCurrentlyOptimal = GetRegDword(
                        @"SYSTEM\CurrentControlSet\Services\disk",
                        "WriteCacheEnabled") != 0
                });

                tweaks.Add(new SSDTweak
                {
                    Name = "High Performance Power Plan",
                    Description = "Prevents CPU throttling and disk sleep — reduces latency for SSD random I/O",
                    Category = "Performance",
                    IsCurrentlyOptimal = IsHighPerformancePlanActive()
                });

                tweaks.Add(new SSDTweak
                {
                    Name = "Disable Search Indexing",
                    Description = "Windows Search indexer continuously writes to the SSD — disable if you rarely use Windows Search",
                    Category = "Performance",
                    IsCurrentlyOptimal = IsServiceDisabledOrManual("WSearch"),
                    IsSelected = false
                });

                tweaks.Add(new SSDTweak
                {
                    Name = "Enable TRIM",
                    Description = "TRIM tells the SSD which blocks are free — essential for maintaining SSD speed over time",
                    Category = "Storage",
                    IsCurrentlyOptimal = IsTrimEnabled()
                });

                tweaks.Add(new SSDTweak
                {
                    Name = "Disable Hibernation",
                    Description = "Hibernation reserves RAM size on SSD as hiberfil.sys — disabling frees gigabytes of space",
                    Category = "Storage",
                    IsCurrentlyOptimal = IsHibernationDisabled(),
                    IsSelected = false
                });

                tweaks.Add(new SSDTweak
                {
                    Name = "AHCI Link Power Management",
                    Description = "Disable aggressive link power management to prevent SSD stutter on wake",
                    Category = "Storage",
                    IsCurrentlyOptimal = GetRegDword(
                        @"SYSTEM\CurrentControlSet\Services\storahci\Parameters\Device",
                        "AlpmOverride") == 0
                });

                tweaks.Add(new SSDTweak
                {
                    Name = "Large System Cache",
                    Description = "Optimize memory for programs rather than file cache — better for desktop workloads",
                    Category = "System",
                    IsCurrentlyOptimal = GetRegDword(
                        @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management",
                        "LargeSystemCache") == 0
                });

                tweaks.Add(new SSDTweak
                {
                    Name = "Disable Windows Error Reporting",
                    Description = "WER creates dump files on SSD during crashes — disable to reduce unnecessary writes",
                    Category = "System",
                    IsCurrentlyOptimal = GetRegDword(
                        @"SOFTWARE\Microsoft\Windows\Windows Error Reporting",
                        "Disabled") == 1
                });

                return tweaks;
            });
        }

        public async Task<(int applied, int failed)> ApplyTweaksAsync(
            IEnumerable<SSDTweak> tweaks,
            IProgress<string> progress = null,
            CancellationToken ct = default)
        {
            int applied = 0, failed = 0;
            await Task.Run(() =>
            {
                foreach (var tweak in tweaks)
                {
                    if (ct.IsCancellationRequested) break;
                    if (tweak.IsCurrentlyOptimal) continue;
                    progress?.Report($"Applying: {tweak.Name}...");
                    try { ApplyTweak(tweak); applied++; }
                    catch (Exception ex) { AppLogger.Log(ex, $"SSD.Apply:{tweak.Name}"); failed++; }
                }
            }, ct);
            return (applied, failed);
        }

        public async Task<(int reverted, int failed)> RevertTweaksAsync(
            IEnumerable<SSDTweak> tweaks,
            IProgress<string> progress = null,
            CancellationToken ct = default)
        {
            int reverted = 0, failed = 0;
            await Task.Run(() =>
            {
                foreach (var tweak in tweaks)
                {
                    if (ct.IsCancellationRequested) break;
                    progress?.Report($"Reverting: {tweak.Name}...");
                    try { RevertTweak(tweak); reverted++; }
                    catch (Exception ex) { AppLogger.Log(ex, $"SSD.Revert:{tweak.Name}"); failed++; }
                }
            }, ct);
            return (reverted, failed);
        }

        private void ApplyTweak(SSDTweak tweak)
        {
            switch (tweak.Name)
            {
                case "Disable Superfetch (SysMain)":
                    SetRegDword(@"SYSTEM\CurrentControlSet\Services\SysMain", "Start", 4);
                    TryStopService("SysMain");
                    break;

                case "Optimize Prefetch for SSD":
                    SetRegDword(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters",
                        "EnablePrefetcher", 1);
                    SetRegDword(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters",
                        "EnableBootTrace", 0);
                    break;

                case "Disable 8.3 Filename Creation":
                    SetRegDword(@"SYSTEM\CurrentControlSet\Control\FileSystem", "NtfsDisable8dot3NameCreation", 1);
                    break;

                case "Disable Last Access Timestamp":
                    SetRegDword(@"SYSTEM\CurrentControlSet\Control\FileSystem", "NtfsDisableLastAccessUpdate", 1);
                    CommandHelper.RunSync("fsutil", "behavior set disablelastaccess 1");
                    break;

                case "Enable Write Caching":
                    SetRegDword(@"SYSTEM\CurrentControlSet\Services\disk", "WriteCacheEnabled", 1);
                    break;

                case "High Performance Power Plan":
                    CommandHelper.RunSync("powercfg.exe", "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
                    break;

                case "Disable Search Indexing":
                    SetRegDword(@"SYSTEM\CurrentControlSet\Services\WSearch", "Start", 4);
                    TryStopService("WSearch");
                    break;

                case "Enable TRIM":
                    CommandHelper.RunSync("fsutil", "behavior set DisableDeleteNotify 0");
                    break;

                case "Disable Hibernation":
                    CommandHelper.RunSync("powercfg.exe", "/h off");
                    break;

                case "AHCI Link Power Management":
                    SetRegDword(@"SYSTEM\CurrentControlSet\Services\storahci\Parameters\Device", "AlpmOverride", 0);
                    break;

                case "Large System Cache":
                    SetRegDword(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management",
                        "LargeSystemCache", 0);
                    break;

                case "Disable Windows Error Reporting":
                    SetRegDword(@"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1);
                    SetRegDword(@"SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting", "Disabled", 1);
                    break;
            }
        }

        private void RevertTweak(SSDTweak tweak)
        {
            switch (tweak.Name)
            {
                case "Disable Superfetch (SysMain)":
                    SetRegDword(@"SYSTEM\CurrentControlSet\Services\SysMain", "Start", 2);
                    break;

                case "Optimize Prefetch for SSD":
                    SetRegDword(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters",
                        "EnablePrefetcher", 3);
                    break;

                case "Disable 8.3 Filename Creation":
                    SetRegDword(@"SYSTEM\CurrentControlSet\Control\FileSystem", "NtfsDisable8dot3NameCreation", 0);
                    break;

                case "Disable Last Access Timestamp":
                    SetRegDword(@"SYSTEM\CurrentControlSet\Control\FileSystem", "NtfsDisableLastAccessUpdate", 0);
                    CommandHelper.RunSync("fsutil", "behavior set disablelastaccess 0");
                    break;

                case "Enable Write Caching":
                    SetRegDword(@"SYSTEM\CurrentControlSet\Services\disk", "WriteCacheEnabled", 0);
                    break;

                case "High Performance Power Plan":
                    CommandHelper.RunSync("powercfg.exe", "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e");
                    break;

                case "Disable Search Indexing":
                    SetRegDword(@"SYSTEM\CurrentControlSet\Services\WSearch", "Start", 2);
                    break;

                case "Enable TRIM":
                    CommandHelper.RunSync("fsutil", "behavior set DisableDeleteNotify 1");
                    break;

                case "Disable Hibernation":
                    CommandHelper.RunSync("powercfg.exe", "/h on");
                    break;

                case "AHCI Link Power Management":
                    SetRegDword(@"SYSTEM\CurrentControlSet\Services\storahci\Parameters\Device", "AlpmOverride", 1);
                    break;

                case "Large System Cache":
                    SetRegDword(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management",
                        "LargeSystemCache", 1);
                    break;

                case "Disable Windows Error Reporting":
                    SetRegDword(@"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 0);
                    SetRegDword(@"SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting", "Disabled", 0);
                    break;
            }
        }

        private static bool IsTrimEnabled()
        {
            try
            {
                string result = CommandHelper.RunSync("fsutil", "behavior query DisableDeleteNotify");
                return result.IndexOf("= 0", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        private static bool IsHibernationDisabled()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\Power");
                int hiberBoot = key?.GetValue("HiberbootEnabled") is int h ? h : 1;
                int hiberFileSz = key?.GetValue("HiberFileSizePercent") is int s ? s : 75;

                string hibPath = Path.Combine(
                    Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? "C:\\",
                    "hiberfil.sys");
                return hiberBoot == 0 || !File.Exists(hibPath);
            }
            catch { return false; }
        }

        private static bool IsHighPerformancePlanActive()
        {
            try
            {
                string result = CommandHelper.RunSync("powercfg.exe", "/getactivescheme");
                return result.IndexOf("8c5e7fda", StringComparison.OrdinalIgnoreCase) >= 0
                    || result.IndexOf("e9a42b02", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        private static bool IsServiceDisabledOrManual(string name)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Services\{name}");
                int start = key?.GetValue("Start") is int s ? s : 2;
                return start >= 3;
            }
            catch { return false; }
        }

        private static void TryStopService(string name)
        {
            try
            {
                using var sc = new ServiceController(name);
                if (sc.Status == ServiceControllerStatus.Running)
                    sc.Stop();
            }
            catch  { AppLogger.Log(new Exception("Unhandled"), nameof(AppLogger)); }
        }

        private static int GetRegDword(string subKey, string valueName)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(subKey);
                return key?.GetValue(valueName) is int v ? v : 0;
            }
            catch { return 0; }
        }

        private static void SetRegDword(string subKey, string valueName, int value)
        {
            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(subKey);
                key?.SetValue(valueName, value, RegistryValueKind.DWord);
            }
            catch (Exception ex) { AppLogger.Log(ex, $"SSD.SetReg:{valueName}"); }
        }
    }
}