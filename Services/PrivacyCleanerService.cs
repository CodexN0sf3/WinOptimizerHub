using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;

namespace WinOptimizerHub.Services
{
    public class PrivacyCleanerService
    {
        public class PrivacyItem : ObservableObject
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public long EstimatedSize { get; set; }

            private bool _isSelected = true;
            public bool IsSelected
            {
                get => _isSelected;
                set => SetProperty(ref _isSelected, value);
            }
        }

        public async Task<List<PrivacyItem>> ScanAsync(CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var items = new List<PrivacyItem>();

                items.Add(new PrivacyItem
                {
                    Name = "Recent Documents",
                    Description = "Recently opened files list (Quick Access)",
                    Category = "MRU",
                    EstimatedSize = GetFolderSize(Environment.GetFolderPath(Environment.SpecialFolder.Recent))
                });
                items.Add(new PrivacyItem
                {
                    Name = "Recent Programs",
                    Description = "Recently run programs from registry",
                    Category = "MRU",
                    EstimatedSize = EstimateRegSize(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs", Registry.CurrentUser)
                });
                items.Add(new PrivacyItem
                {
                    Name = "Run Dialog History",
                    Description = "Commands typed in the Run dialog (Win+R)",
                    Category = "MRU",
                    EstimatedSize = EstimateRegSize(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\RunMRU", Registry.CurrentUser)
                });
                items.Add(new PrivacyItem
                {
                    Name = "Search History",
                    Description = "Windows search box history",
                    Category = "MRU",
                    EstimatedSize = EstimateRegSize(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\WordWheelQuery", Registry.CurrentUser)
                });
                items.Add(new PrivacyItem
                {
                    Name = "Open/Save Dialog History",
                    Description = "Files opened or saved via file dialogs",
                    Category = "MRU",
                    EstimatedSize = EstimateRegSize(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32\OpenSavePidlMRU", Registry.CurrentUser)
                });
                items.Add(new PrivacyItem
                {
                    Name = "Last Visited Folder MRU",
                    Description = "Last folders visited in file dialogs",
                    Category = "MRU",
                    EstimatedSize = EstimateRegSize(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32\LastVisitedPidlMRU", Registry.CurrentUser)
                });
                items.Add(new PrivacyItem
                {
                    Name = "Map Network Drive MRU",
                    Description = "Recently mapped network drives",
                    Category = "MRU",
                    EstimatedSize = EstimateRegSize(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Map Network Drive MRU", Registry.CurrentUser)
                });
                items.Add(new PrivacyItem
                {
                    Name = "Typed URLs",
                    Description = "URLs typed in Internet Explorer / Edge",
                    Category = "MRU",
                    EstimatedSize = EstimateRegSize(@"SOFTWARE\Microsoft\Internet Explorer\TypedURLs", Registry.CurrentUser)
                });

                items.Add(new PrivacyItem
                {
                    Name = "Jump Lists",
                    Description = "Recent files in taskbar jump lists",
                    Category = "System Traces",
                    EstimatedSize = GetFolderSize(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        @"Microsoft\Windows\Recent\AutomaticDestinations"))
                              + GetFolderSize(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        @"Microsoft\Windows\Recent\CustomDestinations"))
                });
                items.Add(new PrivacyItem
                {
                    Name = "Thumbnail Cache",
                    Description = "Cached thumbnail images for files and folders",
                    Category = "System Traces",
                    EstimatedSize = GetFolderSize(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        @"Microsoft\Windows\Explorer"))
                });
                items.Add(new PrivacyItem
                {
                    Name = "Icon Cache",
                    Description = "Cached application and file icons",
                    Category = "System Traces",
                    EstimatedSize = GetFileSize(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "IconCache.db"))
                });
                items.Add(new PrivacyItem
                {
                    Name = "Clipboard History",
                    Description = "Windows clipboard history (Win+V)",
                    Category = "System Traces",
                    EstimatedSize = 0
                });
                items.Add(new PrivacyItem
                {
                    Name = "Notification History",
                    Description = "Windows Action Center notification history",
                    Category = "System Traces",
                    EstimatedSize = GetFolderSize(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        @"Microsoft\Windows\Notifications"))
                });
                items.Add(new PrivacyItem
                {
                    Name = "Activity History Database",
                    Description = "Windows Timeline database (ActivitiesCache.db) — safe to delete",
                    Category = "System Traces",
                    EstimatedSize = GetFolderSize(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "ConnectedDevicesPlatform"))
                });
                items.Add(new PrivacyItem
                {
                    Name = "Prefetch Files",
                    Description = "Windows prefetch files — clearing may slow down app launches temporarily",
                    Category = "System Traces",
                    IsSelected = false,
                    EstimatedSize = GetFolderSize(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch"))
                });

                items.Add(new PrivacyItem
                {
                    Name = "Windows Error Reports",
                    Description = "Crash dumps and error reports sent to Microsoft",
                    Category = "Logs & Reports",
                    EstimatedSize = GetFolderSize(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        @"Microsoft\Windows\WER\ReportArchive"))
                              + GetFolderSize(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        @"Microsoft\Windows\WER\ReportQueue"))
                });
                items.Add(new PrivacyItem
                {
                    Name = "Memory Dumps",
                    Description = "System crash memory dump files",
                    Category = "Logs & Reports",
                    EstimatedSize = GetFolderSize(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Minidump"))
                              + GetFileSize(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "MEMORY.DMP"))
                });
                items.Add(new PrivacyItem
                {
                    Name = "Delivery Optimization Cache",
                    Description = "Cached Windows Update files for peer-to-peer delivery",
                    Category = "Logs & Reports",
                    EstimatedSize = GetFolderSize(@"C:\Windows\SoftwareDistribution\DeliveryOptimization")
                              + GetFolderSize(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        @"Microsoft\Windows\DeliveryOptimization\Cache"))
                });
                items.Add(new PrivacyItem
                {
                    Name = "Windows Update Logs",
                    Description = "Windows Update transaction logs",
                    Category = "Logs & Reports",
                    EstimatedSize = GetFileSize(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "WindowsUpdate.log"))
                              + GetFolderSize(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Logs\WindowsUpdate"))
                });

                items.Add(new PrivacyItem
                {
                    Name = "User Temp Files",
                    Description = "Temporary files in %TEMP% folder",
                    Category = "Temp Files",
                    EstimatedSize = GetFolderSize(Path.GetTempPath())
                });
                items.Add(new PrivacyItem
                {
                    Name = "System Temp Files",
                    Description = "Temporary files in C:\\Windows\\Temp",
                    Category = "Temp Files",
                    IsSelected = false,
                    EstimatedSize = GetFolderSize(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"))
                });

                items.Add(new PrivacyItem
                {
                    Name = "Network Credentials Cache",
                    Description = "Saved network passwords in Windows Credential Manager",
                    Category = "Credentials",
                    IsSelected = false,
                    EstimatedSize = 0
                });

                return items;
            }, ct);
        }

        public async Task<int> CleanAsync(IEnumerable<PrivacyItem> items,
            IProgress<string> progress = null, CancellationToken ct = default)
        {
            int cleaned = 0;
            await Task.Run(() =>
            {
                foreach (var item in items.Where(i => i.IsSelected))
                {
                    if (ct.IsCancellationRequested) break;
                    progress?.Report($"Cleaning: {item.Name}...");
                    try
                    {
                        CleanItem(item);
                        cleaned++;
                    }
                    catch (Exception ex) { AppLogger.Log(ex, $"PrivacyClean.{item.Name}"); }
                }
            }, ct);
            return cleaned;
        }

        private static void CleanItem(PrivacyItem item)
        {
            switch (item.Name)
            {
                case "Recent Documents":
                    ClearFolder(Environment.GetFolderPath(Environment.SpecialFolder.Recent));
                    break;
                case "Recent Programs":
                    ClearRegKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs", Registry.CurrentUser);
                    break;
                case "Run Dialog History":
                    ClearRegKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\RunMRU", Registry.CurrentUser);
                    break;
                case "Search History":
                    ClearRegKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\WordWheelQuery", Registry.CurrentUser);
                    break;
                case "Open/Save Dialog History":
                    ClearRegKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32\OpenSavePidlMRU", Registry.CurrentUser);
                    ClearRegSubKeys(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32\OpenSavePidlMRU", Registry.CurrentUser);
                    break;
                case "Last Visited Folder MRU":
                    ClearRegKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32\LastVisitedPidlMRU", Registry.CurrentUser);
                    break;
                case "Map Network Drive MRU":
                    ClearRegKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Map Network Drive MRU", Registry.CurrentUser);
                    break;
                case "Typed URLs":
                    ClearRegKey(@"SOFTWARE\Microsoft\Internet Explorer\TypedURLs", Registry.CurrentUser);
                    break;

                case "Jump Lists":
                    ClearFolder(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        @"Microsoft\Windows\Recent\AutomaticDestinations"));
                    ClearFolder(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        @"Microsoft\Windows\Recent\CustomDestinations"));
                    break;
                case "Thumbnail Cache":
                    DeleteFilesMatching(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        @"Microsoft\Windows\Explorer"), "thumbcache_*.db");
                    break;
                case "Icon Cache":
                    DeleteFileSafe(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "IconCache.db"));
                    break;
                case "Clipboard History":
                    try { System.Windows.Clipboard.Clear(); } catch { }
                    SetRegDword(@"SOFTWARE\Microsoft\Clipboard", "EnableClipboardHistory", 0, Registry.CurrentUser);
                    SetRegDword(@"SOFTWARE\Microsoft\Clipboard", "EnableClipboardHistory", 1, Registry.CurrentUser);
                    break;
                case "Notification History":
                    ClearFolder(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        @"Microsoft\Windows\Notifications"));
                    break;
                case "Activity History Database":
                    ClearFolder(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "ConnectedDevicesPlatform"));
                    break;
                case "Prefetch Files":
                    ClearFolder(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch"));
                    break;

                case "Windows Error Reports":
                    ClearFolder(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        @"Microsoft\Windows\WER\ReportArchive"));
                    ClearFolder(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        @"Microsoft\Windows\WER\ReportQueue"));
                    ClearFolder(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        @"Microsoft\Windows\WER\ReportArchive"));
                    break;
                case "Memory Dumps":
                    ClearFolder(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Minidump"));
                    DeleteFileSafe(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "MEMORY.DMP"));
                    break;
                case "Delivery Optimization Cache":
                    ClearFolder(@"C:\Windows\SoftwareDistribution\DeliveryOptimization");
                    ClearFolder(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        @"Microsoft\Windows\DeliveryOptimization\Cache"));
                    break;
                case "Windows Update Logs":
                    DeleteFileSafe(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "WindowsUpdate.log"));
                    ClearFolder(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Logs\WindowsUpdate"));
                    break;

                case "User Temp Files":
                    ClearFolderSafe(Path.GetTempPath());
                    break;
                case "System Temp Files":
                    ClearFolderSafe(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));
                    break;

                case "Network Credentials Cache":
                    string credList = CommandHelper.RunSync("cmdkey", "/list");
                    foreach (var line in credList.Split('\n'))
                    {
                        string t = line.Trim();
                        if (t.StartsWith("Target:"))
                            CommandHelper.RunSync("cmdkey", $"/delete:{t.Replace("Target:", "").Trim()}");
                    }
                    break;
            }
        }

        private static string GetFirstActivityHistoryProfile()
        {
            try
            {
                string base1 = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ConnectedDevicesPlatform");
                if (!Directory.Exists(base1)) return "";
                return Directory.GetDirectories(base1).Select(Path.GetFileName).FirstOrDefault() ?? "";
            }
            catch { return ""; }
        }

        private static long GetFolderSize(string path)
        {
            if (string.IsNullOrEmpty(path)) return 0;
            path = Environment.ExpandEnvironmentVariables(path);
            if (!Directory.Exists(path)) return 0;
            long size = 0;
            var stack = new Stack<DirectoryInfo>();
            stack.Push(new DirectoryInfo(path));
            while (stack.Count > 0)
            {
                var dir = stack.Pop();                
                try { foreach (var f in dir.GetFiles()) try { size += f.Length; } catch { } }
                catch { }
                try { foreach (var d in dir.GetDirectories()) stack.Push(d); }
                catch { }
            }
            return size;
        }

        private static long GetFileSize(string path)
        {
            if (string.IsNullOrEmpty(path)) return 0;
            path = Environment.ExpandEnvironmentVariables(path);
            try { return File.Exists(path) ? new FileInfo(path).Length : 0; }
            catch { return 0; }
        }

        private static long EstimateRegSize(string subKey, RegistryKey hive)
        {
            try
            {
                using var key = hive.OpenSubKey(subKey);
                return (key?.ValueCount ?? 0) * 200L;
            }
            catch { return 0; }
        }

        private static void ClearFolder(string path)
        {
            if (!Directory.Exists(path)) return;
            foreach (var f in Directory.GetFiles(path))
                try { File.Delete(f); } catch (Exception ex) { AppLogger.Log(ex, nameof(ClearFolder)); }
        }

        private static void ClearFolderSafe(string path)
        {
            if (!Directory.Exists(path)) return;

            var stack = new Stack<string>();
            stack.Push(path);

            while (stack.Count > 0)
            {
                string current = stack.Pop();

                try
                {
                    foreach (var f in Directory.GetFiles(current))
                        try { File.Delete(f); } catch { }
                }
                catch { }

                try
                {
                    foreach (var d in Directory.GetDirectories(current))
                    {
                        if (!d.Equals(path, StringComparison.OrdinalIgnoreCase))
                            stack.Push(d);
                    }
                }
                catch { }
            }

            try
            {
                foreach (var d in Directory.GetDirectories(path))
                    try { Directory.Delete(d, true); } catch { }
            }
            catch { }
        }

        private static void DeleteFileSafe(string path)
        {
            if (File.Exists(path))
                try { File.Delete(path); } catch (Exception ex) { AppLogger.Log(ex, nameof(DeleteFileSafe)); }
        }

        private static void DeleteFilesMatching(string folder, string pattern)
        {
            if (!Directory.Exists(folder)) return;
            foreach (var f in Directory.GetFiles(folder, pattern))
                try { File.Delete(f); } catch (Exception ex) { AppLogger.Log(ex, nameof(DeleteFilesMatching)); }
        }

        private static void ClearRegKey(string subKey, RegistryKey hive)
        {
            try
            {
                using var key = hive.OpenSubKey(subKey, writable: true);
                if (key == null) return;
                foreach (var v in key.GetValueNames())
                    try { key.DeleteValue(v); } catch { }
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(ClearRegKey)); }
        }

        private static void ClearRegSubKeys(string subKey, RegistryKey hive)
        {
            try
            {
                using var key = hive.OpenSubKey(subKey, writable: true);
                if (key == null) return;
                foreach (var sub in key.GetSubKeyNames())
                    try { key.DeleteSubKeyTree(sub); } catch { }
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(ClearRegSubKeys)); }
        }

        private static void SetRegDword(string subKey, string valueName, int value, RegistryKey hive)
        {
            using var key = hive.CreateSubKey(subKey);
            key?.SetValue(valueName, value, RegistryValueKind.DWord);
        }
    }
}