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
            ScanClsidSubKeys(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\ShellIconOverlayIdentifiers", "Explorer");

            // INTERNET EXPLORER
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

            // PROTOCOL FILTERS & HANDLERS
            ScanProtocolHandlers(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Classes\PROTOCOLS\Filter", "Protocol Filters");
            ScanProtocolHandlers(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Classes\PROTOCOLS\Handler", "Protocol Handlers");

            // OFFICE COM ADDINS (Skype Click to Call, Lync, etc.)
            // Each sub-key has LoadBehavior DWORD: 3=enabled, 2=disabled, 0=disabled
            ScanOfficeAddins(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Office", "Office Addins");
            ScanOfficeAddins(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\WOW6432Node\Microsoft\Office", "Office Addins");
            ScanOfficeAddins(items, seen, RegistryHive.CurrentUser,
                @"SOFTWARE\Microsoft\Office", "Office Addins");

            // USERINIT SETUP APPS (run once after first logon for new users)
            ScanRunKey(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\UserinitmprLogonScript",
                "Winlogon");
            // TERMINAL SERVER RUNONCE
            ScanRunKey(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Terminal Server\Install\Software\Microsoft\Windows\CurrentVersion\RunOnce",
                "RunOnce");

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

            // FONT DRIVERS (NEW)
            ScanRunKey(items, seen, RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Font Drivers", "Font Drivers");
        }

        // ── Legacy helper kept for compatibility (ScanHive was unused) ──────
        internal static void ScanHive(RegistryHive hiveType, List<StartupItem> items, string hiveName)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ScanRunKey(items, seen, hiveType,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Run");
        }

        // ──────────────────────────────────────────────────────────────────
        // SCAN PRIMITIVES
        // ──────────────────────────────────────────────────────────────────

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
                        // Support our _disabled rename fallback AND the native IsInstalled=0 mechanism
                        bool renamedDisabled = rawSubName.EndsWith("_disabled", StringComparison.OrdinalIgnoreCase);
                        string subName = renamedDisabled
                            ? rawSubName.Substring(0, rawSubName.Length - 9)
                            : rawSubName;

                        string uid = $"{fullKeyPath}\\{subName}";
                        if (!seen.Add(uid)) continue;

                        using var sub = key.OpenSubKey(rawSubName);
                        if (sub == null) continue;

                        // Active Setup requires StubPath to run anything
                        string stubPath = sub.GetValue("StubPath")?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(stubPath)) continue;

                        string displayName = sub.GetValue("")?.ToString()
                                          ?? sub.GetValue("DisplayName")?.ToString()
                                          ?? subName;

                        string exePath = ExtractExePath(stubPath);
                        var (pub, desc) = GetFileInfo(exePath);
                        string friendlyDisplayName = GetFriendlyName(exePath, displayName, displayName);

                        // IsInstalled DWORD: 0 = disabled (native Windows mechanism)
                        // Our rename mechanism (_disabled suffix) is the fallback
                        var isInstalledRaw = sub.GetValue("IsInstalled");
                        bool isEnabled;
                        if (renamedDisabled)
                            isEnabled = false; // renamed by us
                        else if (isInstalledRaw != null)
                            isEnabled = Convert.ToInt32(isInstalledRaw) != 0;
                        else
                            isEnabled = true; // absent = enabled

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

        /// <summary>
        /// Scans sub-keys whose name or default value is a CLSID, resolving to DLL/EXE path.
        /// Used for: Explorer shell extensions, BHOs, Winsock catalog, Print Monitors,
        /// Winlogon Notify, ShellIconOverlay, IE extensions, etc.
        /// </summary>
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
                        // Support _disabled suffix — canonical name is without suffix
                        bool isDisabled = rawSubName.EndsWith("_disabled", StringComparison.OrdinalIgnoreCase);
                        string subName = isDisabled
                            ? rawSubName.Substring(0, rawSubName.Length - 9)
                            : rawSubName;

                        // UID always uses canonical name so enabled/disabled map to same item
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

        /// <summary>
        /// Scans Internet Explorer Extensions (toolbar buttons, menu items).
        /// Structure differs from shell extensions — uses ButtonText/Exec values directly,
        /// not InProcServer32. Also handles script-based extensions (ClsidExtension).
        /// </summary>
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

                        // Friendly name: ButtonText first, then MenuText, then sub-key name
                        string displayName = sub.GetValue("ButtonText")?.ToString()
                                          ?? sub.GetValue("MenuText")?.ToString()
                                          ?? subName.Trim();

                        // Executable: Exec value (direct path), or resolve via CLSID
                        string exePath = sub.GetValue("Exec")?.ToString() ?? "";

                        if (string.IsNullOrEmpty(exePath))
                        {
                            // Try ClsidExtension (script-based) or CLSID value
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

        /// <summary>
        /// Scans PROTOCOLS\Filter and PROTOCOLS\Handler.
        /// Each sub-key is a MIME type or URI scheme; the CLSID value resolves to the DLL.
        /// </summary>
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
                        // Type 16/32 = Win32 service
                        // Start: 0=Boot,1=System,2=Auto,3=Manual,4=Disabled
                        // Show ALL non-Microsoft services so user can manage everything
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
                        bool isEnabled = start != 4; // Start=4 = Disabled

                        // Detect Automatic (Delayed Start)
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

                        // Driver is considered disabled if Start=4 OR if _OriginalStart backup exists
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

        // ──────────────────────────────────────────────────────────────────
        // SCAN STATE HELPERS  (determine IsEnabled during scan)
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Checks whether a registry VALUE entry is enabled.
        /// For Run/RunOnce: uses StartupApproved.
        /// For everything else: checks whether "valueName_disabled" exists (our rename convention).
        /// </summary>
        private static bool IsValueEnabled(RegistryHive hive, string subKeyPath, string valueName, string category)
        {
            string fullKeyPath = $"{HivePrefix(hive)}\\{subKeyPath}";

            // Run / RunOnce → StartupApproved mechanism
            string approvedSubKey = GetApprovedSubKey(fullKeyPath);
            if (approvedSubKey != null)
                return IsRunKeyEntryEnabled(hive, valueName, subKeyPath);

            // Boot Execute / LSA Providers → check for _disabled_ backup value
            if (category == "Boot Execute" || category == "LSA Providers")
            {
                try
                {
                    using var root = OpenHive64(hive);
                    using var key = root.OpenSubKey(subKeyPath);
                    if (key == null) return true;
                    // If a backup value exists, the entry was disabled
                    string backupPrefix = $"_disabled_{valueName}_";
                    return !Array.Exists(key.GetValueNames(),
                        v => v.StartsWith(backupPrefix, StringComparison.OrdinalIgnoreCase));
                }
                catch { return true; }
            }

            // All other value-based categories: check for "valueName_disabled" variant
            try
            {
                using var root = OpenHive64(hive);
                using var key = root.OpenSubKey(subKeyPath);
                if (key == null) return true;
                // Enabled if value exists under normal name
                if (key.GetValue(valueName) != null) return true;
                // Disabled if renamed variant exists
                if (key.GetValue(valueName + "_disabled") != null) return false;
                return true;
            }
            catch { return true; }
        }

        /// <summary>
        /// Checks whether a registry SUB-KEY entry is enabled.
        /// Disabled sub-keys are renamed to "subKeyName_disabled" by our toggle mechanism.
        /// </summary>
        /// <summary>
        /// Called from within a scan loop: subKeyName is the ACTUAL name found in GetSubKeyNames().
        /// If the name ends with "_disabled" the entry was disabled by our toggle mechanism.
        /// </summary>
        private static bool IsSubKeyEnabled(string subKeyName)
            => !subKeyName.EndsWith("_disabled", StringComparison.OrdinalIgnoreCase);

        // ──────────────────────────────────────────────────────────────────
        // OFFICE COM ADDINS SCANNER
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Scans Office COM Addins under HKLM/HKCU\SOFTWARE\Microsoft\Office\*\*\Addins\*
        /// LoadBehavior DWORD: 0=disabled at startup, 2=disabled, 3=enabled at startup, 8=demand load
        /// </summary>
        private static void ScanOfficeAddins(List<StartupItem> items, HashSet<string> seen,
            RegistryHive hive, string officeRootPath, string category)
        {
            string hivePrefix = HivePrefix(hive);
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var officeRoot = root.OpenSubKey(officeRootPath);
                if (officeRoot == null) return;

                // Enumerate Office version keys: "14.0", "15.0", "16.0", etc.
                foreach (var versionName in officeRoot.GetSubKeyNames())
                {
                    if (!char.IsDigit(versionName[0])) continue; // skip non-version keys

                    using var versionKey = officeRoot.OpenSubKey(versionName);
                    if (versionKey == null) continue;

                    // Enumerate app keys: "Outlook", "Word", "Excel", etc.
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
                                // Only care about addins that load at startup (3, 9, 11) or are disabled (0, 2)
                                if (loadBehavior < 0) continue;

                                string friendlyName = addinKey.GetValue("FriendlyName")?.ToString()
                                                   ?? addinKey.GetValue("Description")?.ToString()
                                                   ?? addinName;
                                string description = addinKey.GetValue("Description")?.ToString() ?? "";

                                // Resolve COM server path via registry
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

                                // Enabled = LoadBehavior has bit 0 set (odd) AND not 0
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

        /// <summary>Resolves a COM ProgID or CLSID to the DLL/EXE path via InProcServer32.</summary>
        private static string ResolveCOMServer(string progId, RegistryKey hive)
        {
            if (string.IsNullOrEmpty(progId) || hive == null) return null;
            try
            {
                // Try as CLSID directly
                if (progId.StartsWith("{"))
                {
                    using var k = hive.OpenSubKey($@"SOFTWARE\Classes\CLSID\{progId}\InProcServer32")
                               ?? hive.OpenSubKey($@"SOFTWARE\Classes\CLSID\{progId}\LocalServer32");
                    return k?.GetValue("")?.ToString();
                }
                // Try ProgID → CLSID → InProcServer32
                using var progKey = hive.OpenSubKey($@"SOFTWARE\Classes\{progId}\CLSID");
                string clsid = progKey?.GetValue("")?.ToString();
                if (string.IsNullOrEmpty(clsid)) return null;
                using var clsidKey = hive.OpenSubKey($@"SOFTWARE\Classes\CLSID\{clsid}\InProcServer32")
                                  ?? hive.OpenSubKey($@"SOFTWARE\Classes\CLSID\{clsid}\LocalServer32");
                return clsidKey?.GetValue("")?.ToString();
            }
            catch { return null; }
        }

        // ──────────────────────────────────────────────────────────────────
        // TOGGLE / APPROVED KEY LOGIC
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the StartupApproved sub-key for Run/RunOnce keys, or null for
        /// all other registry locations (they don't use the Approved mechanism).
        /// </summary>
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

        /// <summary>
        /// Checks the StartupApproved key to determine if a Run/RunOnce entry is enabled.
        /// Only meaningful for Run/RunOnce keys — other categories always return true here.
        /// </summary>
        internal static bool IsRunKeyEntryEnabled(RegistryHive hiveType, string valueName, string subKeyPath)
        {
            string fullKeyPath = $"{HivePrefix(hiveType)}\\{subKeyPath}";
            string approvedSubKey = GetApprovedSubKey(fullKeyPath);
            if (approvedSubKey == null) return true;

            try
            {
                // StartupApproved is in the same hive as the Run key
                using var hive = OpenHive64(hiveType);
                using var key = hive.OpenSubKey(approvedSubKey);
                if (key == null) return true;
                if (key.GetValue(valueName) is byte[] data && data.Length > 0 && data[0] == 3)
                    return false;
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(IsRunKeyEntryEnabled)); }
            return true;
        }

        // ──────────────────────────────────────────────────────────────────
        // PATH / FILE UTILITIES
        // ──────────────────────────────────────────────────────────────────

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

                // Best friendly name: FileDescription → ProductName → InternalName → filename
                string desc = info.FileDescription;
                if (string.IsNullOrWhiteSpace(desc)) desc = info.ProductName;
                if (string.IsNullOrWhiteSpace(desc)) desc = info.InternalName;
                if (string.IsNullOrWhiteSpace(desc)) desc = Path.GetFileNameWithoutExtension(filePath);

                // Trim version numbers appended to description (e.g. "Skype 8.0")
                // Keep as-is — version numbers in description are actually useful

                return (pub, desc ?? string.Empty);
            }
            catch { return ("Unknown", string.Empty); }
        }

        internal static string GetPublisher(string filePath) => GetFileInfo(filePath).publisher;

        /// <summary>
        /// Gets the best display name for an item: checks registry DisplayName first,
        /// then falls back to FileDescription / ProductName from the binary.
        /// Used to match Autoruns' "friendly name" column.
        /// </summary>
        /// <summary>
        /// Resolves the best human-readable name for a startup entry, matching Autoruns behaviour.
        ///
        /// Priority:
        ///   1. registryDisplayName — if it looks like a real name (not a CLSID, not a path)
        ///   2. CLSID default value — HKLM\Classes\CLSID\{guid} default  (e.g. "Microsoft OneDrive")
        ///   3. FileDescription from PE version resource
        ///   4. ProductName from PE version resource
        ///   5. InternalName from PE version resource
        ///   6. Filename without extension
        ///   7. fallback
        /// </summary>
        internal static string GetFriendlyName(string filePath, string registryDisplayName, string fallback,
            string clsid = null)
        {
            // 1. Registry sub-key/value name — trim whitespace and use directly if it looks
            // like a real name. Examples: " OneDrive2" → "OneDrive2", "EnhancedStorageShell".
            // Only skip if it's a raw CLSID ({...}) or a file path.
            if (!string.IsNullOrWhiteSpace(registryDisplayName))
            {
                string trimmed = registryDisplayName.Trim();
                if (trimmed.Length > 0
                    && !trimmed.StartsWith("{")    // not a CLSID guid
                    && !trimmed.Contains("\\"))   // not a file path
                    return trimmed;
            }

            // 2. CLSID default value in registry (how Autoruns finds "Microsoft OneDrive" etc.)
            if (!string.IsNullOrEmpty(clsid) && clsid.StartsWith("{"))
            {
                string clsidName = GetClsidDisplayName(clsid);
                if (!string.IsNullOrWhiteSpace(clsidName)) return clsidName;
            }
            // Also try if registryDisplayName itself is a CLSID
            if (!string.IsNullOrWhiteSpace(registryDisplayName) && registryDisplayName.StartsWith("{"))
            {
                string clsidName = GetClsidDisplayName(registryDisplayName);
                if (!string.IsNullOrWhiteSpace(clsidName)) return clsidName;
            }

            // 3–5. PE version resource
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
                catch { }
                // 6. Filename
                return Path.GetFileNameWithoutExtension(filePath);
            }
            // 7. Fallback
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

                        // 1. Default value of the CLSID key itself
                        string name = clsidKey.GetValue("")?.ToString();
                        if (IsGoodDisplayName(name)) return name;

                        // 2. VersionIndependentProgID → ProgID friendly name
                        using var vipKey = clsidKey.OpenSubKey("VersionIndependentProgID");
                        string vip = vipKey?.GetValue("")?.ToString();
                        if (!string.IsNullOrEmpty(vip))
                        {
                            using var progKey = root.OpenSubKey(@"SOFTWARE\Classes\" + vip);
                            name = progKey?.GetValue("")?.ToString();
                            if (IsGoodDisplayName(name)) return name;
                        }

                        // 3. ProgID
                        using var pidKey = clsidKey.OpenSubKey("ProgID");
                        string pid = pidKey?.GetValue("")?.ToString();
                        if (!string.IsNullOrEmpty(pid))
                        {
                            using var progKey = root.OpenSubKey(@"SOFTWARE\Classes\" + pid);
                            name = progKey?.GetValue("")?.ToString();
                            if (IsGoodDisplayName(name)) return name;
                        }
                    }
                    catch { }
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