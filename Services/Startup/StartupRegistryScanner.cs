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

            ScanSingleValue(items, seen, RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\SafeBoot", "AlternateShell", "Alternate Shell");

            ScanActiveSetup(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Active Setup\Installed Components", "Installed Components");
            ScanActiveSetup(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\WOW6432Node\Microsoft\Active Setup\Installed Components", "Installed Components");

            ScanWinlogonValue(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", "Userinit", "Winlogon");
            ScanWinlogonValue(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", "Shell", "Winlogon");
            ScanWinlogonValue(items, seen, RegistryHive.CurrentUser,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", "Shell", "Winlogon");

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
            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\ShellIconOverlayIdentifiers", "Explorer");

            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects", "Internet Explorer");
            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects", "Internet Explorer");
            ScanRunKey(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Internet Explorer\Toolbar", "Internet Explorer");
            ScanRunKey(items, seen, RegistryHive.CurrentUser,
                @"SOFTWARE\Microsoft\Internet Explorer\Toolbar", "Internet Explorer");
            ScanIEExtensions(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Internet Explorer\Extensions", "Internet Explorer");
            ScanIEExtensions(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\WOW6432Node\Microsoft\Internet Explorer\Extensions", "Internet Explorer");

            ScanProtocolHandlers(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Classes\PROTOCOLS\Filter", "Protocol Filters");
            ScanProtocolHandlers(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Classes\PROTOCOLS\Handler", "Protocol Handlers");

            ScanOfficeAddins(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Office", "Office Addins");
            ScanOfficeAddins(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\WOW6432Node\Microsoft\Office", "Office Addins");
            ScanOfficeAddins(items, seen, RegistryHive.CurrentUser,
                @"SOFTWARE\Microsoft\Office", "Office Addins");

            ScanRunKey(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\UserinitmprLogonScript",
                "Winlogon");

            ScanRunKey(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Terminal Server\Install\Software\Microsoft\Windows\CurrentVersion\RunOnce",
                "RunOnce");

            ScanServices(items, seen);
            ScanDrivers(items, seen);

            ScanDebuggerKeys(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options", "Image Hijacks");

            ScanSingleValue(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows", "AppInit_DLLs", "AppInit");
            ScanSingleValue(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion\Windows", "AppInit_DLLs", "AppInit");

            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\Notify", "Winlogon Notify");

            ScanSingleValue(items, seen, RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Session Manager", "BootExecute", "Boot Execute");

            ScanSingleValue(items, seen, RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Lsa", "Authentication Packages", "LSA Providers");
            ScanSingleValue(items, seen, RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Lsa", "Security Packages", "LSA Providers");
            ScanSingleValue(items, seen, RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Lsa", "Notification Packages", "LSA Providers");

            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\WinSock2\Parameters\Protocol_Catalog9\Catalog_Entries", "Winsock Providers");
            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\WinSock2\Parameters\NameSpace_Catalog5\Catalog_Entries", "Winsock Providers");

            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Print\Monitors", "Print Monitors");

            ScanSingleValue(items, seen, RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\NetworkProvider\Order", "ProviderOrder", "Network Providers");

            ScanRunKey(items, seen, RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Session Manager\KnownDLLs", "KnownDLLs");

            ScanRunKey(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Font Drivers", "Font Drivers");
        }

        internal static void ScanHive(RegistryHive hiveType, List<StartupItem> items, string hiveName)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ScanRunKey(items, seen, hiveType,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Run");
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
                        bool isEnabled = IsRunKeyEntryEnabled(hive, valueName, subKeyPath);
                        var (pub, desc) = GetFileInfo(exePath);
                        string friendlyDesc = GetFriendlyName(exePath, desc, valueName);

                        items.Add(new StartupItem
                        {
                            Name = valueName,
                            FileName = Path.GetFileName(exePath),
                            Command = exePath,
                            Description = friendlyDesc,
                            Publisher = pub,
                            Source = $"Registry:{hivePrefix}",
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
                    Source = $"Registry:{hivePrefix}",
                    RegistryKey = uid,
                    RegistryKeyPath = fullKeyPath,
                    Category = category,
                    IsEnabled = IsValueEnabled(hive, subKeyPath, valueName, category),
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

                foreach (var rawSubName in key.GetSubKeyNames())
                {
                    try
                    {
                        bool renamedDisabled = rawSubName.EndsWith("_disabled", StringComparison.OrdinalIgnoreCase);
                        string subName = renamedDisabled
                            ? rawSubName.Substring(0, rawSubName.Length - 9)
                            : rawSubName;

                        string uid = $"{fullKeyPath}\\{subName}";
                        if (!seen.Add(uid)) continue;

                        using var sub = key.OpenSubKey(rawSubName);
                        if (sub == null) continue;

                        string stubPath = sub.GetValue("StubPath")?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(stubPath)) continue;

                        string displayName = sub.GetValue("")?.ToString()
                                          ?? sub.GetValue("DisplayName")?.ToString()
                                          ?? subName;

                        string exePath = ExtractExePath(stubPath);
                        var (pub, desc) = GetFileInfo(exePath);
                        string friendlyDisplayName = GetFriendlyName(exePath, displayName, displayName);

                        var isInstalledRaw = sub.GetValue("IsInstalled");
                        bool isEnabled;
                        if (renamedDisabled)
                            isEnabled = false;
                        else if (isInstalledRaw != null)
                            isEnabled = Convert.ToInt32(isInstalledRaw) != 0;
                        else
                            isEnabled = true;

                        items.Add(new StartupItem
                        {
                            Name = friendlyDisplayName,
                            FileName = Path.GetFileName(exePath),
                            Command = exePath,
                            Description = string.IsNullOrEmpty(desc) ? friendlyDisplayName : desc,
                            Publisher = pub,
                            Source = $"Registry:{hivePrefix}",
                            RegistryKey = uid,
                            RegistryKeyPath = fullKeyPath,
                            Category = category,
                            IsEnabled = isEnabled,
                            ImpactLevel = EstimateImpact(exePath),
                        });
                    }
                    catch (Exception ex) { AppLogger.Log(ex, $"ScanActiveSetup:{rawSubName}"); }
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
                using var lmRoot32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

                foreach (var rawSubName in key.GetSubKeyNames())
                {
                    try
                    {
                        bool isDisabled = rawSubName.EndsWith("_disabled", StringComparison.OrdinalIgnoreCase);
                        string subName = isDisabled
                            ? rawSubName.Substring(0, rawSubName.Length - 9)
                            : rawSubName;

                        string uid = $"{fullKeyPath}\\{subName}";
                        if (!seen.Add(uid)) continue;

                        using var sub = key.OpenSubKey(rawSubName);
                        if (sub == null) continue;

                        string defaultVal = sub.GetValue("")?.ToString() ?? subName;
                        string clsid = defaultVal.StartsWith("{") ? defaultVal : subName;
                        string displayName = defaultVal.StartsWith("{") ? subName : defaultVal;

                        string exePath = ResolveCLSID(clsid, root)
                                      ?? ResolveCLSID(clsid, lmRoot)
                                      ?? ResolveCLSID(clsid, lmRoot32);
                        if (string.IsNullOrEmpty(exePath)) continue;

                        exePath = Environment.ExpandEnvironmentVariables(exePath.Trim('"'));
                        var (pub, desc) = GetFileInfo(exePath);

                        displayName = GetFriendlyName(exePath, displayName, Path.GetFileNameWithoutExtension(exePath), clsid);

                        items.Add(new StartupItem
                        {
                            Name = displayName,
                            FileName = Path.GetFileName(exePath),
                            Command = exePath,
                            Description = string.IsNullOrEmpty(desc) ? displayName : desc,
                            Publisher = pub,
                            Source = $"Registry:{hivePrefix}",
                            RegistryKey = uid,
                            RegistryKeyPath = fullKeyPath,
                            Category = category,
                            IsEnabled = !isDisabled,
                            ImpactLevel = EstimateImpact(exePath),
                        });
                    }
                    catch (Exception ex) { AppLogger.Log(ex, $"ScanClsidSubKeys:{rawSubName}"); }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, $"ScanClsidSubKeys:{subKeyPath}"); }
        }

        private static void ScanIEExtensions(List<StartupItem> items, HashSet<string> seen,
            RegistryHive hive, string subKeyPath, string category)
        {
            string hivePrefix = HivePrefix(hive);
            string fullKeyPath = $"{hivePrefix}\\{subKeyPath}";
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var root32 = RegistryKey.OpenBaseKey(hive, RegistryView.Registry32);
                using var lm64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var lm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                using var key = root.OpenSubKey(subKeyPath) ?? root32.OpenSubKey(subKeyPath);
                if (key == null) return;

                foreach (var rawSubName in key.GetSubKeyNames())
                {
                    try
                    {
                        bool isDisabled = rawSubName.EndsWith("_disabled", StringComparison.OrdinalIgnoreCase);
                        string subName = isDisabled
                            ? rawSubName.Substring(0, rawSubName.Length - 9)
                            : rawSubName;

                        string uid = $"{fullKeyPath}\\{subName}";
                        if (!seen.Add(uid)) continue;

                        using var sub = key.OpenSubKey(rawSubName);
                        if (sub == null) continue;

                        string displayName = sub.GetValue("ButtonText")?.ToString()
                                          ?? sub.GetValue("MenuText")?.ToString()
                                          ?? subName.Trim();

                        string exePath = sub.GetValue("Exec")?.ToString() ?? "";

                        if (string.IsNullOrEmpty(exePath))
                        {
                            string clsid = sub.GetValue("ClsidExtension")?.ToString()
                                        ?? sub.GetValue("CLSID")?.ToString()
                                        ?? (subName.StartsWith("{") ? subName : "");

                            if (!string.IsNullOrEmpty(clsid) && clsid.StartsWith("{"))
                            {
                                exePath = ResolveCLSID(clsid, root)
                                       ?? ResolveCLSID(clsid, lm64)
                                       ?? ResolveCLSID(clsid, root32)
                                       ?? ResolveCLSID(clsid, lm32)
                                       ?? "";
                            }
                        }

                        if (!string.IsNullOrEmpty(exePath))
                            exePath = Environment.ExpandEnvironmentVariables(exePath.Trim('"'));

                        var (pub, desc) = GetFileInfo(exePath);
                        if (string.IsNullOrEmpty(displayName) || displayName.StartsWith("{"))
                            displayName = string.IsNullOrEmpty(desc)
                                ? Path.GetFileNameWithoutExtension(exePath)
                                : desc;

                        items.Add(new StartupItem
                        {
                            Name = displayName,
                            FileName = string.IsNullOrEmpty(exePath) ? subName : Path.GetFileName(exePath),
                            Command = exePath,
                            Description = string.IsNullOrEmpty(desc) ? displayName : desc,
                            Publisher = pub,
                            Source = $"Registry:{hivePrefix}",
                            RegistryKey = uid,
                            RegistryKeyPath = fullKeyPath,
                            Category = category,
                            IsEnabled = !isDisabled,
                            ImpactLevel = "Unknown",
                        });
                    }
                    catch (Exception ex) { AppLogger.Log(ex, $"ScanIEExtensions:{rawSubName}"); }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, $"ScanIEExtensions:{subKeyPath}"); }
        }

        private static void ScanProtocolHandlers(List<StartupItem> items, HashSet<string> seen,
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
                using var lmRoot32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

                foreach (var rawSubName in key.GetSubKeyNames())
                {
                    try
                    {
                        bool isDisabled = rawSubName.EndsWith("_disabled", StringComparison.OrdinalIgnoreCase);
                        string subName = isDisabled
                            ? rawSubName.Substring(0, rawSubName.Length - 9)
                            : rawSubName;

                        string uid = $"{fullKeyPath}\\{subName}";
                        if (!seen.Add(uid)) continue;

                        using var sub = key.OpenSubKey(rawSubName);
                        if (sub == null) continue;

                        string clsid = sub.GetValue("CLSID")?.ToString() ?? "";
                        if (string.IsNullOrEmpty(clsid) || !clsid.StartsWith("{"))
                            clsid = sub.GetValue("")?.ToString() ?? "";
                        if (string.IsNullOrEmpty(clsid) || !clsid.StartsWith("{"))
                        {
                            using var clsidSub = sub.OpenSubKey("CLSID");
                            clsid = clsidSub?.GetValue("")?.ToString() ?? clsid;
                        }

                        string exePath = "";
                        if (!string.IsNullOrEmpty(clsid) && clsid.StartsWith("{"))
                        {
                            exePath = ResolveCLSID(clsid, root)
                                   ?? ResolveCLSID(clsid, lmRoot)
                                   ?? "";
                            if (!string.IsNullOrEmpty(exePath))
                                exePath = Environment.ExpandEnvironmentVariables(exePath.Trim('"'));
                        }

                        var (pub, desc) = GetFileInfo(exePath);
                        string displayName = string.IsNullOrEmpty(desc) ? subName : desc;

                        items.Add(new StartupItem
                        {
                            Name = subName,
                            FileName = string.IsNullOrEmpty(exePath) ? clsid : Path.GetFileName(exePath),
                            Command = string.IsNullOrEmpty(exePath) ? clsid : exePath,
                            Description = displayName,
                            Publisher = pub,
                            Source = $"Registry:{hivePrefix}",
                            RegistryKey = uid,
                            RegistryKeyPath = fullKeyPath,
                            Category = category,
                            IsEnabled = !isDisabled,
                            ImpactLevel = "Unknown",
                        });
                    }
                    catch (Exception ex) { AppLogger.Log(ex, $"ScanProtocolHandlers:{rawSubName}"); }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, $"ScanProtocolHandlers:{subKeyPath}"); }
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
                            Source = $"Registry:{hivePrefix}",
                            RegistryKey = uid,
                            RegistryKeyPath = fullKeyPath,
                            Category = category,
                            IsEnabled = IsValueEnabled(hive, subKeyPath + "\\" + subName, "Debugger", category),
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

                        if (type != 16 && type != 32) continue;
                        if (start < 0 || start > 4) continue;

                        string imagePath = sub.GetValue("ImagePath")?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(imagePath)) continue;

                        string exePath = ExtractExePath(Environment.ExpandEnvironmentVariables(imagePath));
                        var (pub, desc) = GetFileInfo(exePath);
                        if (pub.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                        string uid = $"{fullKeyPath}\\{svcName}";
                        if (!seen.Add(uid)) continue;

                        string displayName = GetFriendlyName(exePath,
                            sub.GetValue("DisplayName")?.ToString() ?? svcName, svcName);
                        bool isEnabled = start != 4;

                        bool isDelayed = start == 2 &&
                            Convert.ToInt32(sub.GetValue("DelayedAutostart") ?? 0) == 1;
                        string startLabel = start switch
                        {
                            0 => "Boot",
                            1 => "System",
                            2 => isDelayed ? "Auto (Delayed)" : "Automatic",
                            3 => "Manual",
                            4 => "Disabled",
                            _ => "Unknown"
                        };

                        items.Add(new StartupItem
                        {
                            Name = displayName,
                            FileName = Path.GetFileName(exePath),
                            Command = exePath,
                            Description = string.IsNullOrEmpty(desc)
                                                ? $"[{startLabel}] {displayName}" : $"[{startLabel}] {desc}",
                            Publisher = pub,
                            Source = "Services",
                            RegistryKey = uid,
                            RegistryKeyPath = fullKeyPath,
                            Category = "Services",
                            IsEnabled = isEnabled,
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

                        if (pub == "Unknown" || string.IsNullOrEmpty(pub))
                            pub = GetDriverPublisherFromRegistry(root, svcName)
                               ?? sub.GetValue("ProviderName")?.ToString()
                               ?? pub;

                        if (pub.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                        string uid = $"{fullKeyPath}\\{svcName}";
                        if (!seen.Add(uid)) continue;

                        string displayName = GetFriendlyName(exePath,
                            sub.GetValue("DisplayName")?.ToString() ?? svcName,
                            svcName);
                        if (string.IsNullOrEmpty(desc))
                            desc = sub.GetValue("Description")?.ToString() ?? displayName;

                        bool drvEnabled = start != 4 && sub.GetValue("_OriginalStart") == null;
                        items.Add(new StartupItem
                        {
                            Name = displayName,
                            FileName = Path.GetFileName(exePath),
                            Command = exePath,
                            Description = string.IsNullOrEmpty(desc) ? displayName : desc,
                            Publisher = pub,
                            Source = "Drivers",
                            RegistryKey = uid,
                            RegistryKeyPath = fullKeyPath,
                            Category = "Drivers",
                            IsEnabled = drvEnabled,
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
                return devKey?.GetValue("Mfg")?.ToString()?.Trim('(', ')', ' ')
                    ?? devKey?.GetValue("Provider")?.ToString();
            }
            catch { return null; }
        }

        private static bool IsValueEnabled(RegistryHive hive, string subKeyPath, string valueName, string category)
        {
            string fullKeyPath = $"{HivePrefix(hive)}\\{subKeyPath}";

            string approvedSubKey = GetApprovedSubKey(fullKeyPath);
            if (approvedSubKey != null)
                return IsRunKeyEntryEnabled(hive, valueName, subKeyPath);

            if (category == "Boot Execute" || category == "LSA Providers")
            {
                try
                {
                    using var root = OpenHive64(hive);
                    using var key = root.OpenSubKey(subKeyPath);
                    if (key == null) return true;

                    string backupPrefix = $"_disabled_{valueName}_";
                    return !Array.Exists(key.GetValueNames(),
                        v => v.StartsWith(backupPrefix, StringComparison.OrdinalIgnoreCase));
                }
                catch { return true; }
            }

            try
            {
                using var root = OpenHive64(hive);
                using var key = root.OpenSubKey(subKeyPath);
                if (key == null) return true;

                if (key.GetValue(valueName) != null) return true;

                if (key.GetValue(valueName + "_disabled") != null) return false;
                return true;
            }
            catch { return true; }
        }

        private static bool IsSubKeyEnabled(string subKeyName)
            => !subKeyName.EndsWith("_disabled", StringComparison.OrdinalIgnoreCase);

        private static void ScanOfficeAddins(List<StartupItem> items, HashSet<string> seen,
            RegistryHive hive, string officeRootPath, string category)
        {
            string hivePrefix = HivePrefix(hive);
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var officeRoot = root.OpenSubKey(officeRootPath);
                if (officeRoot == null) return;

                foreach (var versionName in officeRoot.GetSubKeyNames())
                {
                    if (!char.IsDigit(versionName[0])) continue;

                    using var versionKey = officeRoot.OpenSubKey(versionName);
                    if (versionKey == null) continue;

                    foreach (var appName in versionKey.GetSubKeyNames())
                    {
                        using var appKey = versionKey.OpenSubKey(appName);
                        using var addinsKey = appKey?.OpenSubKey("Addins");
                        if (addinsKey == null) continue;

                        foreach (var addinName in addinsKey.GetSubKeyNames())
                        {
                            try
                            {
                                using var addinKey = addinsKey.OpenSubKey(addinName);
                                if (addinKey == null) continue;

                                int loadBehavior = Convert.ToInt32(addinKey.GetValue("LoadBehavior") ?? -1);

                                if (loadBehavior < 0) continue;

                                string friendlyName = addinKey.GetValue("FriendlyName")?.ToString()
                                                   ?? addinKey.GetValue("Description")?.ToString()
                                                   ?? addinName;
                                string description = addinKey.GetValue("Description")?.ToString() ?? "";

                                string exePath = ResolveCOMServer(addinName, root)
                                             ?? ResolveCOMServer(addinName,
                                                    RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                                             ?? "";
                                if (!string.IsNullOrEmpty(exePath))
                                    exePath = Environment.ExpandEnvironmentVariables(exePath.Trim('"'));

                                var (pub, fileDesc) = GetFileInfo(exePath);
                                if (!string.IsNullOrEmpty(fileDesc) && string.IsNullOrEmpty(description))
                                    description = fileDesc;

                                string uid = $"{hivePrefix}\\{officeRootPath}\\{versionName}\\{appName}\\Addins\\{addinName}";
                                if (!seen.Add(uid)) continue;

                                bool isEnabled = loadBehavior != 0 && loadBehavior != 2 && (loadBehavior & 1) != 0;

                                items.Add(new StartupItem
                                {
                                    Name = friendlyName,
                                    FileName = string.IsNullOrEmpty(exePath) ? addinName : Path.GetFileName(exePath),
                                    Command = string.IsNullOrEmpty(exePath) ? addinName : exePath,
                                    Description = string.IsNullOrEmpty(description) ? $"Office {appName} addin" : description,
                                    Publisher = pub,
                                    Source = $"Registry:{hivePrefix}",
                                    RegistryKey = uid,
                                    RegistryKeyPath = $"{hivePrefix}\\{officeRootPath}\\{versionName}\\{appName}\\Addins",
                                    Category = category,
                                    IsEnabled = isEnabled,
                                    ImpactLevel = "Medium",
                                });
                            }
                            catch (Exception ex) { AppLogger.Log(ex, $"ScanOfficeAddins:{addinName}"); }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, $"ScanOfficeAddins:{officeRootPath}"); }
        }

        private static string ResolveCOMServer(string progId, RegistryKey hive)
        {
            if (string.IsNullOrEmpty(progId) || hive == null) return null;
            try
            {

                if (progId.StartsWith("{"))
                {
                    using var k = hive.OpenSubKey($@"SOFTWARE\Classes\CLSID\{progId}\InProcServer32")
                               ?? hive.OpenSubKey($@"SOFTWARE\Classes\CLSID\{progId}\LocalServer32");
                    return k?.GetValue("")?.ToString();
                }

                using var progKey = hive.OpenSubKey($@"SOFTWARE\Classes\{progId}\CLSID");
                string clsid = progKey?.GetValue("")?.ToString();
                if (string.IsNullOrEmpty(clsid)) return null;
                using var clsidKey = hive.OpenSubKey($@"SOFTWARE\Classes\CLSID\{clsid}\InProcServer32")
                                  ?? hive.OpenSubKey($@"SOFTWARE\Classes\CLSID\{clsid}\LocalServer32");
                return clsidKey?.GetValue("")?.ToString();
            }
            catch { return null; }
        }

        internal static string GetApprovedSubKey(string fullKeyPath)
        {
            bool isRunKey =
                fullKeyPath.EndsWith("\\Run", StringComparison.OrdinalIgnoreCase) ||
                fullKeyPath.EndsWith("\\RunOnce", StringComparison.OrdinalIgnoreCase) ||
                fullKeyPath.EndsWith("\\RunOnceEx", StringComparison.OrdinalIgnoreCase);

            if (!isRunKey) return null;

            return fullKeyPath.EndsWith("WOW6432", StringComparison.OrdinalIgnoreCase)
                ? @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32"
                : @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        }

        internal static RegistryKey OpenHive64(RegistryHive hive) =>
            RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);

        internal static bool IsRunKeyEntryEnabled(RegistryHive hiveType, string valueName, string subKeyPath)
        {
            string fullKeyPath = $"{HivePrefix(hiveType)}\\{subKeyPath}";
            string approvedSubKey = GetApprovedSubKey(fullKeyPath);
            if (approvedSubKey == null) return true;

            try
            {
                using var hive = OpenHive64(hiveType);
                using var key = hive.OpenSubKey(approvedSubKey);
                if (key == null) return true;
                if (key.GetValue(valueName) is byte[] data && data.Length > 0 && data[0] == 3)
                    return false;
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(IsRunKeyEntryEnabled)); }
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

                string desc = info.FileDescription;
                if (string.IsNullOrWhiteSpace(desc)) desc = info.ProductName;
                if (string.IsNullOrWhiteSpace(desc)) desc = info.InternalName;
                if (string.IsNullOrWhiteSpace(desc)) desc = Path.GetFileNameWithoutExtension(filePath);

                return (pub, desc ?? string.Empty);
            }
            catch { return ("Unknown", string.Empty); }
        }

        internal static string GetPublisher(string filePath) => GetFileInfo(filePath).publisher;

        internal static string GetFriendlyName(string filePath, string registryDisplayName, string fallback,
            string clsid = null)
        {
            if (!string.IsNullOrWhiteSpace(registryDisplayName))
            {
                string trimmed = registryDisplayName.Trim();
                if (trimmed.Length > 0
                    && !trimmed.StartsWith("{")
                    && !trimmed.Contains("\\"))
                    return trimmed;
            }

            if (!string.IsNullOrEmpty(clsid) && clsid.StartsWith("{"))
            {
                string clsidName = GetClsidDisplayName(clsid);
                if (!string.IsNullOrWhiteSpace(clsidName)) return clsidName;
            }

            if (!string.IsNullOrWhiteSpace(registryDisplayName) && registryDisplayName.StartsWith("{"))
            {
                string clsidName = GetClsidDisplayName(registryDisplayName);
                if (!string.IsNullOrWhiteSpace(clsidName)) return clsidName;
            }

            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                try
                {
                    var info = FileVersionInfo.GetVersionInfo(filePath);
                    string desc = info.FileDescription;
                    if (string.IsNullOrWhiteSpace(desc)) desc = info.ProductName;
                    if (string.IsNullOrWhiteSpace(desc)) desc = info.InternalName;
                    if (!string.IsNullOrWhiteSpace(desc)) return desc.Trim();
                }
                catch  { AppLogger.Log(new Exception("Unhandled"), nameof(AppLogger)); }
                return Path.GetFileNameWithoutExtension(filePath);
            }
            return string.IsNullOrWhiteSpace(fallback) ? (filePath ?? "") : fallback;
        }

        private static string GetClsidDisplayName(string clsid)
        {
            if (string.IsNullOrEmpty(clsid)) return null;

            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
                {
                    try
                    {
                        using var root = RegistryKey.OpenBaseKey(hive, view);
                        using var clsidKey = root.OpenSubKey(@"SOFTWARE\Classes\CLSID\" + clsid);
                        if (clsidKey == null) continue;

                        string name = clsidKey.GetValue("")?.ToString();
                        if (IsGoodDisplayName(name)) return name;

                        using var vipKey = clsidKey.OpenSubKey("VersionIndependentProgID");
                        string vip = vipKey?.GetValue("")?.ToString();
                        if (!string.IsNullOrEmpty(vip))
                        {
                            using var progKey = root.OpenSubKey(@"SOFTWARE\Classes\" + vip);
                            name = progKey?.GetValue("")?.ToString();
                            if (IsGoodDisplayName(name)) return name;
                        }

                        using var pidKey = clsidKey.OpenSubKey("ProgID");
                        string pid = pidKey?.GetValue("")?.ToString();
                        if (!string.IsNullOrEmpty(pid))
                        {
                            using var progKey = root.OpenSubKey(@"SOFTWARE\Classes\" + pid);
                            name = progKey?.GetValue("")?.ToString();
                            if (IsGoodDisplayName(name)) return name;
                        }
                    }
                    catch  { AppLogger.Log(new Exception("Unhandled"), nameof(AppLogger)); }
                }
            }
            return null;
        }

        private static bool IsGoodDisplayName(string name)
            => !string.IsNullOrWhiteSpace(name)
            && !name.StartsWith("{")
            && !name.StartsWith("@")
            && name.Length > 2;

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

        private static string HivePrefix(RegistryHive hive) =>
            hive == RegistryHive.LocalMachine ? "HKEY_LOCAL_MACHINE" : "HKEY_CURRENT_USER";
    }
}