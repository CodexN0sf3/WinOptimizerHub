using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;
using WinOptimizerHub.Services.Startup;

namespace WinOptimizerHub.Services
{
    public class StartupManagerService
    {

        public async Task<(List<StartupItem> items, string diagnostics)> GetStartupItemsDiagnosticAsync()
        {
            return await Task.Run(() =>
            {
                var items = new List<StartupItem>();
                var diag = new System.Text.StringBuilder();

                int before = items.Count;
                try { StartupRegistryScanner.ScanAllKeys(items); }
                catch (Exception ex) { diag.Append($"Registry-ERR:{ex.Message} "); }
                diag.Append($"Registry:{items.Count - before} ");

                before = items.Count;
                try { StartupFolderScanner.Scan(items); }
                catch (Exception ex) { diag.Append($"Folders-ERR:{ex.Message} "); }
                diag.Append($"Folders:{items.Count - before} ");

                before = items.Count;
                try { StartupTaskScanner.Scan(items); }
                catch (Exception ex) { diag.Append($"Tasks-ERR:{ex.Message} "); }
                diag.Append($"Tasks:{items.Count - before}");

                return (items, diag.ToString().Trim());
            });
        }

        public async Task<(bool ok, string error)> SetStartupItemEnabledAsync(StartupItem item, bool enabled)
        {
            return await Task.Run(() =>
            {
                try
                {

                    if (item.Source == "Startup Folder" || item.Category == "Startup Folder")
                    {
                        StartupFolderScanner.Toggle(item, enabled);
                        item.IsEnabled = enabled;
                        return (true, (string)null);
                    }

                    if (item.Source == "TaskScheduler")
                        return ToggleScheduledTask(item, enabled);

                    return ToggleRegistryItem(item, enabled);
                }
                catch (UnauthorizedAccessException ex)
                {
                    return (false, $"Access denied: {ex.Message}\nTry running as Administrator.");
                }
                catch (Exception ex)
                {
                    return (false, $"Exception: {ex.GetType().Name}: {ex.Message}");
                }
            });
        }

        private static (bool ok, string error) ToggleScheduledTask(StartupItem item, bool enabled)
        {
            string taskPath = item.RegistryKey;
            if (!taskPath.StartsWith("\\")) taskPath = "\\" + taskPath;
            string flag = enabled ? "/ENABLE" : "/DISABLE";
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Change /TN \"{taskPath}\" {flag}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            string stderr = p?.StandardError.ReadToEnd() ?? "";
            p?.WaitForExit(10000);
            if (p?.ExitCode == 0) { item.IsEnabled = enabled; return (true, null); }
            return (false, $"schtasks failed (code {p?.ExitCode}): {stderr}");
        }

        private static (bool ok, string error) ToggleRegistryItem(StartupItem item, bool enabled)
        {
            string keyPath = item.RegistryKeyPath;

            string approvedSubKey = StartupRegistryScanner.GetApprovedSubKey(keyPath);
            if (approvedSubKey != null)
            {

                bool isHkcu = keyPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase)
                           || keyPath.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase);
                var approvedHive = isHkcu ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;
                using var root = StartupRegistryScanner.OpenHive64(approvedHive);
                using var approvedKey = root.OpenSubKey(approvedSubKey, writable: true)
                                     ?? root.CreateSubKey(approvedSubKey);
                if (approvedKey == null)
                    return (false, $"Cannot open/create: {approvedSubKey}");

                if (enabled)
                    approvedKey.DeleteValue(item.Name, throwOnMissingValue: false);
                else
                {
                    var data = new byte[12];
                    data[0] = 3;
                    approvedKey.SetValue(item.Name, data, RegistryValueKind.Binary);
                }
                item.IsEnabled = enabled;
                return (true, null);
            }

            if (item.Category == "Services")
            {
                string svcKeyPath = StripHivePrefix(item.RegistryKey);
                try
                {
                    using var root = StartupRegistryScanner.OpenHive64(RegistryHive.LocalMachine);
                    using var key = root.OpenSubKey(svcKeyPath, writable: true);
                    if (key == null) return (false, $"Cannot open service key: {item.RegistryKey}");
                    key.SetValue("Start", enabled ? 2 : 4, RegistryValueKind.DWord);
                    item.IsEnabled = enabled;
                    return (true, null);
                }
                catch (UnauthorizedAccessException ex)
                {
                    return (false, $"Access denied: {ex.Message}\nTry running as Administrator.");
                }
            }

            if (item.Category == "Drivers")
            {
                string drvKeyPath = StripHivePrefix(item.RegistryKey);
                try
                {
                    using var root = StartupRegistryScanner.OpenHive64(RegistryHive.LocalMachine);
                    using var key = root.OpenSubKey(drvKeyPath, writable: true);
                    if (key == null) return (false, $"Cannot open driver key: {item.RegistryKey}");

                    if (enabled)
                    {
                        var orig = key.GetValue("_OriginalStart");
                        int restoreValue = orig is int origInt ? origInt : 1;
                        key.SetValue("Start", restoreValue, RegistryValueKind.DWord);
                        key.DeleteValue("_OriginalStart", throwOnMissingValue: false);
                    }
                    else
                    {
                        int currentStart = Convert.ToInt32(key.GetValue("Start") ?? 1);
                        key.SetValue("_OriginalStart", currentStart, RegistryValueKind.DWord);
                        key.SetValue("Start", 4, RegistryValueKind.DWord);
                    }
                    item.IsEnabled = enabled;
                    return (true, null);
                }
                catch (UnauthorizedAccessException ex)
                {
                    return (false, $"Access denied: {ex.Message}\nTry running as Administrator.");
                }
            }

            if (item.Category == "Boot Execute" || item.Category == "LSA Providers")
                return ToggleMultiSzEntry(item, enabled);

            if (item.Category == "Winlogon")
                return ToggleByValueRename(item, enabled);

            if (item.Category == "Installed Components")
                return ToggleActiveSetupItem(item, enabled);

            if (item.Category == "Office Addins")
                return ToggleOfficeAddin(item, enabled);

            return ToggleByValueRename(item, enabled);
        }

        private static (bool ok, string error) ToggleActiveSetupItem(StartupItem item, bool enabled)
        {
            bool isHkcu = item.RegistryKeyPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase)
                       || item.RegistryKeyPath.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase);
            var hiveType = isHkcu ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;

            string subKeyPath = StripHivePrefix(item.RegistryKey);

            if (enabled && subKeyPath.EndsWith("_disabled", StringComparison.OrdinalIgnoreCase))
            {
                string parentPath = subKeyPath.Substring(0, subKeyPath.LastIndexOf('\\'));
                string keyName = subKeyPath.Substring(parentPath.Length + 1);
                string newName = keyName.Substring(0, keyName.Length - 9);
                try
                {
                    using var root = StartupRegistryScanner.OpenHive64(hiveType);
                    using var parentKey = root.OpenSubKey(parentPath, writable: true);
                    if (parentKey != null) RenameSubKey(parentKey, keyName, newName);
                    subKeyPath = parentPath + "\\" + newName;
                }
                catch (Exception ex) { return (false, $"Cannot rename key: {ex.Message}"); }
            }

            try
            {
                using var root = StartupRegistryScanner.OpenHive64(hiveType);
                using var key = root.OpenSubKey(subKeyPath, writable: true);
                if (key == null)
                    return (false, $"Cannot open Active Setup key: {item.RegistryKey}");

                if (enabled)
                {
                    key.SetValue("IsInstalled", 1, RegistryValueKind.DWord);

                    if (string.IsNullOrEmpty(key.GetValue("StubPath")?.ToString())
                        && !string.IsNullOrEmpty(item.Command))
                        key.SetValue("StubPath", item.Command, RegistryValueKind.String);
                }
                else
                {
                    key.SetValue("IsInstalled", 0, RegistryValueKind.DWord);
                }
                item.IsEnabled = enabled;
                return (true, null);
            }
            catch (UnauthorizedAccessException ex)
            {
                return (false, $"Access denied: {ex.Message}\nTry running as Administrator.");
            }
            catch (Exception ex)
            {
                return (false, $"Exception: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static (bool ok, string error) ToggleOfficeAddin(StartupItem item, bool enabled)
        {
            bool isHkcu = item.RegistryKeyPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase)
                       || item.RegistryKeyPath.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase);
            var hiveType = isHkcu ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;

            string addinKeyPath = StripHivePrefix(item.RegistryKey);
            try
            {
                using var root = StartupRegistryScanner.OpenHive64(hiveType);
                using var addinKey = root.OpenSubKey(addinKeyPath, writable: true);
                if (addinKey == null)
                    return (false, $"Cannot open addin key: {item.RegistryKey}");

                if (enabled)
                {
                    var orig = addinKey.GetValue("_OriginalLoadBehavior");
                    int restoreValue = orig is int v ? v : 3;
                    addinKey.SetValue("LoadBehavior", restoreValue, RegistryValueKind.DWord);
                    addinKey.DeleteValue("_OriginalLoadBehavior", throwOnMissingValue: false);
                }
                else
                {
                    int current = Convert.ToInt32(addinKey.GetValue("LoadBehavior") ?? 3);
                    addinKey.SetValue("_OriginalLoadBehavior", current, RegistryValueKind.DWord);
                    addinKey.SetValue("LoadBehavior", 2, RegistryValueKind.DWord);
                }
                item.IsEnabled = enabled;
                return (true, null);
            }
            catch (UnauthorizedAccessException ex)
            {
                return (false, $"Access denied: {ex.Message}\nTry running as Administrator.");
            }
            catch (Exception ex)
            {
                return (false, $"Exception: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static (bool ok, string error) ToggleMultiSzEntry(StartupItem item, bool enabled)
        {
            bool isHkcu = item.RegistryKeyPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase)
                       || item.RegistryKeyPath.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase);
            var hiveType = isHkcu ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;
            string subPath = StripHivePrefix(item.RegistryKeyPath);

            try
            {
                using var root = StartupRegistryScanner.OpenHive64(hiveType);
                using var key = root.OpenSubKey(subPath, writable: true);
                if (key == null)
                    return (false, $"Cannot open registry key: {item.RegistryKeyPath}");

                string valueName = item.Name;
                string backupName = $"_disabled_{valueName}_{item.FileName}";

                if (enabled)
                {

                    string backedUp = key.GetValue(backupName)?.ToString();
                    if (!string.IsNullOrEmpty(backedUp))
                    {
                        var existing = GetMultiSz(key, valueName);
                        if (!Array.Exists(existing, e => string.Equals(e, backedUp, StringComparison.OrdinalIgnoreCase)))
                        {
                            var newList = new string[existing.Length + 1];
                            Array.Copy(existing, newList, existing.Length);
                            newList[existing.Length] = backedUp;
                            key.SetValue(valueName, newList, RegistryValueKind.MultiString);
                        }
                        key.DeleteValue(backupName, throwOnMissingValue: false);
                    }
                }
                else
                {

                    var existing = GetMultiSz(key, valueName);

                    string target = item.Command;
                    string matched = Array.Find(existing,
                        e => string.Equals(e, target, StringComparison.OrdinalIgnoreCase)
                          || string.Equals(e, item.FileName, StringComparison.OrdinalIgnoreCase)
                          || string.Equals(e, item.Name, StringComparison.OrdinalIgnoreCase));

                    if (matched != null)
                    {

                        key.SetValue(backupName, matched, RegistryValueKind.String);

                        var filtered = Array.FindAll(existing,
                            e => !string.Equals(e, matched, StringComparison.OrdinalIgnoreCase));
                        key.SetValue(valueName, filtered, RegistryValueKind.MultiString);
                    }
                }
                item.IsEnabled = enabled;
                return (true, null);
            }
            catch (UnauthorizedAccessException ex)
            {
                return (false, $"Access denied: {ex.Message}\nTry running as Administrator.");
            }
            catch (Exception ex)
            {
                return (false, $"Exception: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static string[] GetMultiSz(RegistryKey key, string valueName)
        {
            var raw = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (raw is string[] arr) return arr;
            if (raw is string s) return new[] { s };
            return Array.Empty<string>();
        }

        private static (bool ok, string error) ToggleByValueRename(StartupItem item, bool enabled)
        {
            const string disabledSuffix = "_disabled";

            bool isHkcu = item.RegistryKeyPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase)
                       || item.RegistryKeyPath.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase);
            var hiveType = isHkcu ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;

            string subPath = StripHivePrefix(item.RegistryKeyPath);

            bool isSubKeyItem = IsClsidSubKeyCategory(item.Category);

            try
            {
                using var root = StartupRegistryScanner.OpenHive64(hiveType);

                if (isSubKeyItem)
                    return ToggleSubKeyRename(root, subPath, item, enabled, disabledSuffix);
                else
                    return ToggleValueRename(root, subPath, item, enabled, disabledSuffix);
            }
            catch (UnauthorizedAccessException ex)
            {
                return (false, $"Access denied: {ex.Message}\nTry running as Administrator.");
            }
            catch (Exception ex)
            {
                return (false, $"Exception: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static (bool ok, string error) ToggleValueRename(
            RegistryKey root, string subPath, StartupItem item, bool enabled, string suffix)
        {
            using var key = root.OpenSubKey(subPath, writable: true);
            if (key == null)
                return (false, $"Cannot open registry key: {item.RegistryKeyPath}");

            if (enabled)
            {

                string disabledName = item.Name + suffix;
                var val = key.GetValue(disabledName);
                if (val != null)
                {
                    var kind = key.GetValueKind(disabledName);
                    key.SetValue(item.Name, val, kind);
                    key.DeleteValue(disabledName, throwOnMissingValue: false);
                }

            }
            else
            {

                var val = key.GetValue(item.Name);
                if (val != null)
                {
                    var kind = key.GetValueKind(item.Name);
                    key.SetValue(item.Name + suffix, val, kind);
                    key.DeleteValue(item.Name, throwOnMissingValue: false);
                }
            }
            item.IsEnabled = enabled;
            return (true, null);
        }

        private static (bool ok, string error) ToggleSubKeyRename(
            RegistryKey root, string parentPath, StartupItem item, bool enabled, string suffix)
        {
            string registryKeyStripped = StripHivePrefix(item.RegistryKey);
            string parentStripped = StripHivePrefix(item.RegistryKeyPath);

            string canonicalName = registryKeyStripped.Substring(parentStripped.Length).TrimStart('\\');
            if (canonicalName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                canonicalName = canonicalName.Substring(0, canonicalName.Length - suffix.Length);

            string disabledName = canonicalName + suffix;

            using var parentKey = root.OpenSubKey(parentStripped, writable: true);
            if (parentKey == null)
                return (false, $"Cannot open parent key: {item.RegistryKeyPath}");

            if (enabled)
            {

                if (parentKey.OpenSubKey(disabledName) != null)
                    RenameSubKey(parentKey, disabledName, canonicalName);

            }
            else
            {

                if (parentKey.OpenSubKey(canonicalName) != null)
                    RenameSubKey(parentKey, canonicalName, disabledName);

            }

            string hivePrefix = item.RegistryKey.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase)
                             || item.RegistryKey.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase)
                ? "HKEY_CURRENT_USER" : "HKEY_LOCAL_MACHINE";
            item.RegistryKey = $"{hivePrefix}\\{parentStripped}\\{canonicalName}";
            item.IsEnabled = enabled;
            return (true, null);
        }

        private static void RenameSubKey(RegistryKey parent, string oldName, string newName)
        {

            using var oldKey = parent.OpenSubKey(oldName, writable: true);
            if (oldKey == null) return;
            using var newKey = parent.CreateSubKey(newName, writable: true);
            if (newKey == null) return;
            CopyKey(oldKey, newKey);

            try { parent.DeleteSubKeyTree(oldName, throwOnMissingSubKey: false); }
            catch (Exception ex) { AppLogger.Log(ex, $"RenameSubKey: delete {oldName}"); }
        }

        private static void CopyKey(RegistryKey src, RegistryKey dst)
        {
            if (src == null || dst == null) return;
            foreach (var v in src.GetValueNames())
            {
                try { dst.SetValue(v, src.GetValue(v), src.GetValueKind(v)); }
                catch  { AppLogger.Log(new Exception("Unhandled"), nameof(AppLogger)); }
            }
            foreach (var sub in src.GetSubKeyNames())
            {
                try
                {
                    using var srcSub = src.OpenSubKey(sub, writable: false);
                    using var dstSub = dst.CreateSubKey(sub, writable: true);
                    if (srcSub != null && dstSub != null) CopyKey(srcSub, dstSub);
                }
                catch  { AppLogger.Log(new Exception("Unhandled"), nameof(AppLogger)); }
            }
        }

        public async Task<(bool ok, string error)> DeleteStartupItemAsync(StartupItem item)
        {
            return await Task.Run(() =>
            {
                try
                {

                    if (item.Source == "Startup Folder" || item.Category == "Startup Folder")
                    {
                        if (File.Exists(item.Command)) File.Delete(item.Command);

                        string disabled = item.Command + ".disabled";
                        if (File.Exists(disabled)) File.Delete(disabled);
                        return (true, (string)null);
                    }

                    if (item.Source == "TaskScheduler")
                    {
                        string taskPath = item.RegistryKey;
                        if (!taskPath.StartsWith("\\")) taskPath = "\\" + taskPath;
                        var psi = new ProcessStartInfo
                        {
                            FileName = "schtasks.exe",
                            Arguments = $"/Delete /TN \"{taskPath}\" /F",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                        };
                        using var p = Process.Start(psi);
                        p?.WaitForExit(10000);
                        return p?.ExitCode == 0
                            ? (true, (string)null)
                            : (false, $"schtasks /Delete failed (code {p?.ExitCode})");
                    }

                    return DeleteRegistryItem(item);
                }
                catch (UnauthorizedAccessException ex)
                {
                    return (false, $"Access denied: {ex.Message}\nTry running as Administrator.");
                }
                catch (Exception ex)
                {
                    return (false, $"Exception: {ex.GetType().Name}: {ex.Message}");
                }
            });
        }

        private static (bool ok, string error) DeleteRegistryItem(StartupItem item)
        {
            bool isHkcu = item.RegistryKeyPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase)
                       || item.RegistryKeyPath.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase);
            var hiveType = isHkcu ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;

            string keyPath = StripHivePrefix(item.RegistryKeyPath);

            using var root = StartupRegistryScanner.OpenHive64(hiveType);

            string approvedSubKey = StartupRegistryScanner.GetApprovedSubKey(item.RegistryKeyPath);
            if (approvedSubKey != null)
            {
                using var key = root.OpenSubKey(keyPath, writable: true);
                if (key == null)
                    return (false, $"Cannot open registry key: {item.RegistryKeyPath}");

                key.DeleteValue(item.Name, throwOnMissingValue: false);
                key.DeleteValue(item.Name + "_disabled", throwOnMissingValue: false);

                bool isApprovedHkcu = item.RegistryKeyPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase)
                                   || item.RegistryKeyPath.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase);
                var approvedHive2 = isApprovedHkcu ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;
                using var hklm = StartupRegistryScanner.OpenHive64(approvedHive2);
                using var approvedKey = hklm.OpenSubKey(approvedSubKey, writable: true);
                approvedKey?.DeleteValue(item.Name, throwOnMissingValue: false);
                return (true, null);
            }

            if (IsClsidSubKeyCategory(item.Category))
            {
                string registryKeyStripped = StripHivePrefix(item.RegistryKey);
                string parentStripped = StripHivePrefix(item.RegistryKeyPath);
                string subKeyName = registryKeyStripped.Substring(parentStripped.Length).TrimStart('\\');

                using var parentKey = root.OpenSubKey(parentStripped, writable: true);
                if (parentKey == null)
                    return (false, $"Cannot open parent key: {item.RegistryKeyPath}");

                if (parentKey.OpenSubKey(subKeyName) != null)
                    parentKey.DeleteSubKeyTree(subKeyName);
                else
                {
                    string dis = subKeyName + "_disabled";
                    if (parentKey.OpenSubKey(dis) != null)
                        parentKey.DeleteSubKeyTree(dis);
                }
                return (true, null);
            }

            if (item.Category == "Services")
            {
                string svcKeyPath = StripHivePrefix(item.RegistryKey);
                if (!svcKeyPath.StartsWith(@"SYSTEM\CurrentControlSet\Services\",
                    StringComparison.OrdinalIgnoreCase))
                    return (false, "Unexpected key path for service — deletion aborted for safety.");
                try { root.DeleteSubKeyTree(svcKeyPath); return (true, null); }
                catch (Exception ex) { return (false, $"Failed to delete service key: {ex.Message}"); }
            }

            if (item.Category == "Drivers")
            {
                string drvKeyPath = StripHivePrefix(item.RegistryKey);
                if (!drvKeyPath.StartsWith(@"SYSTEM\CurrentControlSet\Services\",
                    StringComparison.OrdinalIgnoreCase))
                    return (false, "Unexpected key path for driver — deletion aborted for safety.");
                try { root.DeleteSubKeyTree(drvKeyPath); return (true, null); }
                catch (Exception ex) { return (false, $"Failed to delete driver key: {ex.Message}"); }
            }

            if (item.Category == "Boot Execute" || item.Category == "LSA Providers")
            {
                using var key = root.OpenSubKey(keyPath, writable: true);
                if (key == null)
                    return (false, $"Cannot open registry key: {item.RegistryKeyPath}");

                string valueName = item.Name;
                var existing = GetMultiSz(key, valueName);
                string target = item.Command;
                var filtered = Array.FindAll(existing,
                    e => !string.Equals(e, target, StringComparison.OrdinalIgnoreCase)
                      && !string.Equals(e, item.FileName, StringComparison.OrdinalIgnoreCase)
                      && !string.Equals(e, item.Name, StringComparison.OrdinalIgnoreCase));
                key.SetValue(valueName, filtered, RegistryValueKind.MultiString);

                string backupName = $"_disabled_{valueName}_{item.FileName}";
                key.DeleteValue(backupName, throwOnMissingValue: false);
                return (true, null);
            }

            if (item.Category == "Office Addins")
            {
                string addinPath = StripHivePrefix(item.RegistryKey);

                int lastSlash = addinPath.LastIndexOf('\\');
                if (lastSlash > 0)
                {
                    string parentPath = addinPath.Substring(0, lastSlash);
                    string addinName = addinPath.Substring(lastSlash + 1);
                    using var parentKey = root.OpenSubKey(parentPath, writable: true);
                    if (parentKey != null)
                    {
                        parentKey.DeleteSubKeyTree(addinName, throwOnMissingSubKey: false);
                        return (true, null);
                    }
                }
                return (false, "Cannot locate addin parent key.");
            }

            using var singleKey = root.OpenSubKey(keyPath, writable: true);
            if (singleKey == null)
                return (false, $"Cannot open registry key: {item.RegistryKeyPath}");

            singleKey.DeleteValue(item.Name, throwOnMissingValue: false);
            singleKey.DeleteValue(item.Name + "_disabled", throwOnMissingValue: false);
            return (true, null);
        }

        public static bool IsDangerousCategory(StartupItem item)
        {
            if (item.Category is "Boot Execute" or "LSA Providers" or
                "Winsock Providers" or "KnownDLLs")
                return true;

            if (item.Category == "Winlogon" &&
                (item.Name.Equals("Shell", StringComparison.OrdinalIgnoreCase) ||
                 item.Name.Equals("Userinit", StringComparison.OrdinalIgnoreCase)))
                return true;

            return false;
        }

        public static string GetDangerWarning(StartupItem item)
        {
            if (item.Category == "Winlogon" &&
                (item.Name.Equals("Shell", StringComparison.OrdinalIgnoreCase) ||
                 item.Name.Equals("Userinit", StringComparison.OrdinalIgnoreCase)))
                return $"'{item.Name}' is a critical Windows logon value.\n\n" +
                       "Removing or disabling it will prevent Windows from starting correctly " +
                       "and may require recovery tools to fix.\n\n" +
                       "Are you absolutely sure you want to continue?";

            return $"'{item.Name}' is in the '{item.Category}' category.\n\n" +
                   "This is a system-critical registry location. Modifying it can " +
                   "cause boot failures, loss of network connectivity, or system " +
                   "instability.\n\n" +
                   "Are you absolutely sure you want to continue?";
        }

        private static bool IsClsidSubKeyCategory(string category) =>
            category is "Explorer" or "Internet Explorer" or "Winlogon Notify" or
                        "Installed Components" or "Print Monitors" or "Winsock Providers" or
                        "Protocol Filters" or "Protocol Handlers";

        private static string StripHivePrefix(string fullPath)
        {
            foreach (var prefix in new[]
            {
                "HKEY_LOCAL_MACHINE\\", "HKEY_CURRENT_USER\\",
                "HKLM\\", "HKCU\\"
            })
            {
                if (fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return fullPath.Substring(prefix.Length);
            }
            return fullPath;
        }
    }
}