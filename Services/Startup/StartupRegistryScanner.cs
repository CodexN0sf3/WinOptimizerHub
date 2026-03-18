using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;

namespace WinOptimizerHub.Services.Startup
{
    internal static class StartupRegistryScanner
    {

        internal static void ScanAllKeys(List<StartupItem> items)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // RUN
            ScanRunKey(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Run");
            ScanRunKey(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Run");
            ScanRunKey(items, seen, RegistryHive.CurrentUser,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Run");
            ScanRunKey(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer\Run", "Run");
            ScanRunKey(items, seen, RegistryHive.CurrentUser,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer\Run", "Run");
            ScanRunKey(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Terminal Server\Install\Software\Microsoft\Windows\CurrentVersion\Run", "Run");

            // RUNONCE
            ScanRunKey(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "RunOnce");
            ScanRunKey(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnceEx", "RunOnce");
            ScanRunKey(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce", "RunOnce");
            ScanRunKey(items, seen, RegistryHive.CurrentUser,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "RunOnce");
            ScanRunKey(items, seen, RegistryHive.CurrentUser,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnceEx", "RunOnce");

            // ALTERNATE SHELL
            ScanSingleValue(items, seen, RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\SafeBoot", "AlternateShell", "Alternate Shell");

            // INSTALLED COMPONENTS (Active Setup)
            ScanActiveSetup(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Active Setup\Installed Components", "Installed Components");
            ScanActiveSetup(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\WOW6432Node\Microsoft\Active Setup\Installed Components", "Installed Components");

            // WINLOGON
            ScanWinlogonValue(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", "Userinit", "Winlogon");
            ScanWinlogonValue(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", "Shell", "Winlogon");
            ScanWinlogonValue(items, seen, RegistryHive.CurrentUser,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", "Shell", "Winlogon");

            // EXPLORER
            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Classes\*\ShellEx\ContextMenuHandlers", "Explorer");
            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Classes\Drive\ShellEx\ContextMenuHandlers", "Explorer");
            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Classes\Directory\ShellEx\ContextMenuHandlers", "Explorer");
            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Classes\Directory\Background\ShellEx\ContextMenuHandlers", "Explorer");
            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Classes\Folder\ShellEx\ContextMenuHandlers", "Explorer");
            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Classes\Folder\ShellEx\DragDropHandlers", "Explorer");
            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\ShellIconOverlayIdentifiers", "Explorer");

            // INTERNET EXPLORER
            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects", "Internet Explorer");
            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects", "Internet Explorer");
            ScanRunKey(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Internet Explorer\Toolbar", "Internet Explorer");
            ScanRunKey(items, seen, RegistryHive.CurrentUser,
                @"SOFTWARE\Microsoft\Internet Explorer\Toolbar", "Internet Explorer");
            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Internet Explorer\Extensions", "Internet Explorer");

            // SERVICES & DRIVERS
            ScanServices(items, seen);
            ScanDrivers(items, seen);

            // IMAGE HIJACKS
            ScanDebuggerKeys(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options", "Image Hijacks");

            // APPINIT DLLs
            ScanSingleValue(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows", "AppInit_DLLs", "AppInit");
            ScanSingleValue(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion\Windows", "AppInit_DLLs", "AppInit");

            // WINLOGON NOTIFY
            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\Notify", "Winlogon Notify");

            // BOOT EXECUTE
            ScanSingleValue(items, seen, RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Session Manager", "BootExecute", "Boot Execute");

            // LSA PROVIDERS
            ScanSingleValue(items, seen, RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Lsa", "Authentication Packages", "LSA Providers");
            ScanSingleValue(items, seen, RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Lsa", "Security Packages", "LSA Providers");
            ScanSingleValue(items, seen, RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Lsa", "Notification Packages", "LSA Providers");

            // WINSOCK PROVIDERS
            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\WinSock2\Parameters\Protocol_Catalog9\Catalog_Entries", "Winsock Providers");
            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\WinSock2\Parameters\NameSpace_Catalog5\Catalog_Entries", "Winsock Providers");

            // PRINT MONITORS
            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Print\Monitors", "Print Monitors");

            // NETWORK PROVIDERS
            ScanSingleValue(items, seen, RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\NetworkProvider\Order", "ProviderOrder", "Network Providers");

            // KNOWN DLLs
            ScanRunKey(items, seen, RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Session Manager\KnownDLLs", "KnownDLLs");
        }

        internal static void ScanHive(RegistryHive hiveType, List<StartupItem> items, string hiveName)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ScanRunKey(items, seen, hiveType,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Logon");
        }

        private static void ScanRunKey(List<StartupItem> items, HashSet<string> seen,
            RegistryHive hive, string subKeyPath, string category)
        {
            string hivePrefix = HivePrefix(hive);
            string fullKeyPath = $"{hivePrefix}\\{subKeyPath}";
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = root.OpenSubKey(subKeyPath);
                if (key == null) return;

                foreach (var valueName in key.GetValueNames())
                {
                    try
                    {
                        string raw = key.GetValue(valueName)?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(raw)) continue;
                        string uid = $"{fullKeyPath}\\{valueName}";
                        if (!seen.Add(uid)) continue;

                        string exePath = ExtractExePath(raw);
                        bool isEnabled = IsEntryEnabled(hive, valueName, subKeyPath);
                        var (pub, desc) = GetFileInfo(exePath);

                        items.Add(new StartupItem
                        {
                            Name = valueName,
                            FileName = Path.GetFileName(exePath),
                            Command = exePath,
                            Description = string.IsNullOrEmpty(desc) ? valueName : desc,
                            Publisher = pub,
                            RegistryKey = uid,
                            RegistryKeyPath = fullKeyPath,
                            Category = category,
                            IsEnabled = isEnabled,
                            ImpactLevel = EstimateImpact(exePath),
                        });
                    }
                    catch (Exception ex) { AppLogger.Log(ex, $"ScanRunKey:{valueName}"); }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, $"ScanRunKey:{subKeyPath}"); }
        }

        private static void ScanSingleValue(List<StartupItem> items, HashSet<string> seen,
            RegistryHive hive, string subKeyPath, string valueName, string category)
        {
            string hivePrefix = HivePrefix(hive);
            string fullKeyPath = $"{hivePrefix}\\{subKeyPath}";
            string uid = $"{fullKeyPath}\\{valueName}";
            if (!seen.Add(uid)) return;
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = root.OpenSubKey(subKeyPath);
                if (key == null) return;

                var raw = key.GetValue(valueName);
                if (raw == null) return;
                string value = raw is string[] arr ? string.Join(", ", arr) : raw.ToString();
                if (string.IsNullOrWhiteSpace(value)) return;

                string exePath = ExtractExePath(value);
                var (pub, desc) = GetFileInfo(exePath);

                items.Add(new StartupItem
                {
                    Name = valueName,
                    FileName = string.IsNullOrEmpty(exePath) ? value : Path.GetFileName(exePath),
                    Command = string.IsNullOrEmpty(exePath) ? value : exePath,
                    Description = string.IsNullOrEmpty(desc) ? value : desc,
                    Publisher = pub,
                    RegistryKey = uid,
                    RegistryKeyPath = fullKeyPath,
                    Category = category,
                    IsEnabled = true,
                    ImpactLevel = "Unknown",
                });
            }
            catch (Exception ex) { AppLogger.Log(ex, $"ScanSingleValue:{subKeyPath}\\{valueName}"); }
        }

        private static void ScanWinlogonValue(List<StartupItem> items, HashSet<string> seen,
            RegistryHive hive, string subKeyPath, string valueName, string category)
            => ScanSingleValue(items, seen, hive, subKeyPath, valueName, category);

        private static void ScanActiveSetup(List<StartupItem> items, HashSet<string> seen,
            RegistryHive hive, string subKeyPath, string category)
        {
            string hivePrefix = HivePrefix(hive);
            string fullKeyPath = $"{hivePrefix}\\{subKeyPath}";
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = root.OpenSubKey(subKeyPath);
                if (key == null) return;

                foreach (var subName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var sub = key.OpenSubKey(subName);
                        if (sub == null) continue;

                        string stubPath = sub.GetValue("StubPath")?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(stubPath)) continue;

                        string displayName = sub.GetValue("")?.ToString()
                                          ?? sub.GetValue("DisplayName")?.ToString()
                                          ?? subName;
                        string uid = $"{fullKeyPath}\\{subName}";
                        if (!seen.Add(uid)) continue;

                        string exePath = ExtractExePath(stubPath);
                        var (pub, desc) = GetFileInfo(exePath);

                        items.Add(new StartupItem
                        {
                            Name = displayName,
                            FileName = Path.GetFileName(exePath),
                            Command = exePath,
                            Description = string.IsNullOrEmpty(desc) ? displayName : desc,
                            Publisher = pub,
                            RegistryKey = uid,
                            RegistryKeyPath = fullKeyPath,
                            Category = category,
                            IsEnabled = true,
                            ImpactLevel = EstimateImpact(exePath),
                        });
                    }
                    catch (Exception ex) { AppLogger.Log(ex, $"ScanActiveSetup:{subName}"); }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, $"ScanActiveSetup:{subKeyPath}"); }
        }

        private static void ScanClsidSubKeys(List<StartupItem> items, HashSet<string> seen,
            RegistryHive hive, string subKeyPath, string category)
        {
            string hivePrefix = HivePrefix(hive);
            string fullKeyPath = $"{hivePrefix}\\{subKeyPath}";
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = root.OpenSubKey(subKeyPath);
                if (key == null) return;

                using var lmRoot = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

                foreach (var subName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var sub = key.OpenSubKey(subName);
                        if (sub == null) continue;

                        string defaultVal = sub.GetValue("")?.ToString() ?? subName;
                        string clsid = defaultVal.StartsWith("{") ? defaultVal : subName;
                        string displayName = defaultVal.StartsWith("{") ? subName : defaultVal;

                        string uid = $"{fullKeyPath}\\{subName}";
                        if (!seen.Add(uid)) continue;

                        string exePath = ResolveCLSID(clsid, root)
                                      ?? ResolveCLSID(clsid, lmRoot);
                        if (string.IsNullOrEmpty(exePath)) continue;

                        exePath = Environment.ExpandEnvironmentVariables(exePath.Trim('"'));
                        var (pub, desc) = GetFileInfo(exePath);

                        if (string.IsNullOrEmpty(displayName) || displayName.StartsWith("{"))
                            displayName = string.IsNullOrEmpty(desc)
                                ? Path.GetFileNameWithoutExtension(exePath)
                                : desc;

                        items.Add(new StartupItem
                        {
                            Name = displayName,
                            FileName = Path.GetFileName(exePath),
                            Command = exePath,
                            Description = string.IsNullOrEmpty(desc) ? displayName : desc,
                            Publisher = pub,
                            RegistryKey = uid,
                            RegistryKeyPath = fullKeyPath,
                            Category = category,
                            IsEnabled = true,
                            ImpactLevel = EstimateImpact(exePath),
                        });
                    }
                    catch (Exception ex) { AppLogger.Log(ex, $"ScanClsidSubKeys:{subName}"); }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, $"ScanClsidSubKeys:{subKeyPath}"); }
        }

        private static void ScanDebuggerKeys(List<StartupItem> items, HashSet<string> seen,
            RegistryHive hive, string subKeyPath, string category)
        {
            string hivePrefix = HivePrefix(hive);
            string fullKeyPath = $"{hivePrefix}\\{subKeyPath}";
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = root.OpenSubKey(subKeyPath);
                if (key == null) return;

                foreach (var subName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var sub = key.OpenSubKey(subName);
                        string debugger = sub?.GetValue("Debugger")?.ToString();
                        if (string.IsNullOrWhiteSpace(debugger)) continue;

                        string uid = $"{fullKeyPath}\\{subName}\\Debugger";
                        if (!seen.Add(uid)) continue;

                        string exePath = ExtractExePath(debugger);
                        var (pub, desc) = GetFileInfo(exePath);

                        items.Add(new StartupItem
                        {
                            Name = subName,
                            FileName = Path.GetFileName(exePath),
                            Command = exePath,
                            Description = string.IsNullOrEmpty(desc) ? subName : desc,
                            Publisher = pub,
                            RegistryKey = uid,
                            RegistryKeyPath = fullKeyPath,
                            Category = category,
                            IsEnabled = true,
                            ImpactLevel = "High",
                        });
                    }
                    catch (Exception ex) { AppLogger.Log(ex, $"ScanDebugger:{subName}"); }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, $"ScanDebuggerKeys:{subKeyPath}"); }
        }

        private static void ScanServices(List<StartupItem> items, HashSet<string> seen)
        {
            const string subKeyPath = @"SYSTEM\CurrentControlSet\Services";
            string fullKeyPath = $"HKEY_LOCAL_MACHINE\\{subKeyPath}";
            try
            {
                using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var key = root.OpenSubKey(subKeyPath);
                if (key == null) return;

                foreach (var svcName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var sub = key.OpenSubKey(svcName);
                        if (sub == null) continue;
                        int type = Convert.ToInt32(sub.GetValue("Type") ?? -1);
                        int start = Convert.ToInt32(sub.GetValue("Start") ?? -1);
                        // Type 16/32 = Win32 service; Start 2 = Automatic
                        if ((type != 16 && type != 32) || start != 2) continue;

                        string imagePath = sub.GetValue("ImagePath")?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(imagePath)) continue;

                        string exePath = ExtractExePath(Environment.ExpandEnvironmentVariables(imagePath));
                        var (pub, desc) = GetFileInfo(exePath);
                        if (pub.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                        string uid = $"{fullKeyPath}\\{svcName}";
                        if (!seen.Add(uid)) continue;

                        string displayName = sub.GetValue("DisplayName")?.ToString() ?? svcName;

                        items.Add(new StartupItem
                        {
                            Name = displayName,
                            FileName = Path.GetFileName(exePath),
                            Command = exePath,
                            Description = string.IsNullOrEmpty(desc) ? displayName : desc,
                            Publisher = pub,
                            RegistryKey = uid,
                            RegistryKeyPath = fullKeyPath,
                            Category = "Services",
                            IsEnabled = true,
                            ImpactLevel = EstimateImpact(exePath),
                        });
                    }
                    catch (Exception ex) { AppLogger.Log(ex, $"ScanServices:{svcName}"); }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, "ScanServices"); }
        }

        private static void ScanDrivers(List<StartupItem> items, HashSet<string> seen)
        {
            const string subKeyPath = @"SYSTEM\CurrentControlSet\Services";
            string fullKeyPath = $"HKEY_LOCAL_MACHINE\\{subKeyPath}";
            try
            {
                using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var key = root.OpenSubKey(subKeyPath);
                if (key == null) return;

                foreach (var svcName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var sub = key.OpenSubKey(svcName);
                        if (sub == null) continue;
                        int type = Convert.ToInt32(sub.GetValue("Type") ?? -1);
                        int start = Convert.ToInt32(sub.GetValue("Start") ?? -1);
                        if ((type != 1 && type != 2) || (start != 0 && start != 1)) continue;

                        string imagePath = sub.GetValue("ImagePath")?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(imagePath)) continue;

                        string exePath = ExtractExePath(Environment.ExpandEnvironmentVariables(imagePath));
                        var (pub, desc) = GetFileInfo(exePath);

                        // For drivers, also check registry for manufacturer/provider info
                        if (pub == "Unknown" || string.IsNullOrEmpty(pub))
                        {
                            // Try reading from driver's enum key
                            pub = GetDriverPublisherFromRegistry(root, svcName)
                               ?? sub.GetValue("ProviderName")?.ToString()
                               ?? pub;
                        }

                        if (pub.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                        string uid = $"{fullKeyPath}\\{svcName}";
                        if (!seen.Add(uid)) continue;

                        string displayName = sub.GetValue("DisplayName")?.ToString() ?? svcName;
                        if (string.IsNullOrEmpty(desc))
                            desc = sub.GetValue("Description")?.ToString() ?? displayName;

                        items.Add(new StartupItem
                        {
                            Name = displayName,
                            FileName = Path.GetFileName(exePath),
                            Command = exePath,
                            Description = string.IsNullOrEmpty(desc) ? displayName : desc,
                            Publisher = pub,
                            RegistryKey = uid,
                            RegistryKeyPath = fullKeyPath,
                            Category = "Drivers",
                            IsEnabled = true,
                            ImpactLevel = "Unknown",
                        });
                    }
                    catch (Exception ex) { AppLogger.Log(ex, $"ScanDrivers:{svcName}"); }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, "ScanDrivers"); }
        }

        private static string GetDriverPublisherFromRegistry(RegistryKey root, string svcName)
        {
            try
            {
                using var enumKey = root.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{svcName}\Enum");
                if (enumKey == null) return null;

                string instanceId = enumKey.GetValue("0")?.ToString();
                if (string.IsNullOrEmpty(instanceId)) return null;

                using var devKey = root.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\{instanceId}");
                if (devKey == null) return null;

                return devKey.GetValue("Mfg")?.ToString()?.Trim('(', ')', ' ')
                    ?? devKey.GetValue("Provider")?.ToString();
            }
            catch { return null; }
        }

        private static string HivePrefix(RegistryHive hive) =>
            hive == RegistryHive.LocalMachine ? "HKEY_LOCAL_MACHINE" : "HKEY_CURRENT_USER";

        private static string ResolveCLSID(string clsid, RegistryKey hive)
        {
            if (string.IsNullOrEmpty(clsid) || hive == null) return null;
            try
            {
                using var k = hive.OpenSubKey($@"SOFTWARE\Classes\CLSID\{clsid}\InProcServer32")
                           ?? hive.OpenSubKey($@"SOFTWARE\Classes\CLSID\{clsid}\LocalServer32");
                return k?.GetValue("")?.ToString();
            }
            catch { return null; }
        }

        internal static string GetApprovedSubKey(string runKeyPath) =>
            runKeyPath.Contains("WOW6432")
                ? @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32"
                : @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

        internal static RegistryKey OpenHive64(RegistryHive hive) =>
            RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);

        internal static bool IsEntryEnabled(RegistryHive hiveType, string valueName, string runKeyPath)
        {
            try
            {
                using var hive = OpenHive64(hiveType);
                using var key = hive.OpenSubKey(GetApprovedSubKey(runKeyPath));
                if (key == null) return true;
                if (key.GetValue(valueName) is byte[] data && data.Length > 0 && data[0] == 3)
                    return false;
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(IsEntryEnabled)); }
            return true;
        }

        internal static string ExtractExePath(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return "";
            command = Environment.ExpandEnvironmentVariables(command.Trim());
            if (command.StartsWith("\""))
            {
                command = command.Substring(1);
                int end = command.IndexOf('"');
                command = end > 0 ? command.Substring(0, end) : command;
            }
            else
            {
                int space = command.IndexOf(' ');
                if (space > 0) command = command.Substring(0, space);
            }

            if (command.Contains('\\') || command.Contains('/'))
                return command;

            if (!string.IsNullOrEmpty(command))
            {
                string sys32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
                string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

                foreach (var dir in new[] { sys32, windir, Path.Combine(windir, "SysWOW64") })
                {
                    string candidate = Path.Combine(dir, command);
                    if (File.Exists(candidate)) return candidate;
                    
                    if (!command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        candidate = Path.Combine(dir, command + ".exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }

            return command;
        }

        internal static (string publisher, string description) GetFileInfo(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return ("Unknown", string.Empty);
            try
            {
                var info = FileVersionInfo.GetVersionInfo(filePath);
                string pub = !string.IsNullOrEmpty(info.CompanyName) ? info.CompanyName : "Unknown";
                string desc = !string.IsNullOrEmpty(info.FileDescription) ? info.FileDescription : string.Empty;
                return (pub, desc);
            }
            catch { return ("Unknown", string.Empty); }
        }

        internal static string GetPublisher(string filePath) => GetFileInfo(filePath).publisher;

        private static string EstimateImpact(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return "Unknown";
            try
            {
                long size = new FileInfo(filePath).Length;
                if (size > 50_000_000) return "High";
                if (size > 5_000_000) return "Medium";
                return "Low";
            }
            catch { return "Unknown"; }
        }
    }
}