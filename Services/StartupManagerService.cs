using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
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
                    if (item.Source.StartsWith("Folder:", StringComparison.Ordinal))
                    {
                        StartupFolderScanner.Toggle(item, enabled);
                        item.IsEnabled = enabled;
                        return (true, null);
                    }

                    if (item.Source.StartsWith("TaskScheduler:", StringComparison.Ordinal))
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
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            string stderr = p?.StandardError.ReadToEnd() ?? "";
            p?.WaitForExit(10000);
            if (p?.ExitCode == 0) { item.IsEnabled = enabled; return (true, null); }
            return (false, $"schtasks failed (code {p?.ExitCode}): {stderr}");
        }

        private static (bool ok, string error) ToggleRegistryItem(StartupItem item, bool enabled)
        {
            bool isHkcu = item.RegistryKeyPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase)
                       || item.RegistryKeyPath.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase);
            var hiveType = isHkcu ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;

            string keyPath = item.RegistryKeyPath;
            foreach (var prefix in new[] { "HKEY_LOCAL_MACHINE\\", "HKEY_CURRENT_USER\\", "HKLM\\", "HKCU\\" })
                if (keyPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                { keyPath = keyPath.Substring(prefix.Length); break; }

            string approvedSubKey = StartupRegistryScanner.GetApprovedSubKey(keyPath);

            using var root64 = StartupRegistryScanner.OpenHive64(hiveType);
            using var approvedKey = root64.OpenSubKey(approvedSubKey, writable: true)
                                 ?? root64.CreateSubKey(approvedSubKey);

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

        public async Task<(bool ok, string error)> DeleteStartupItemAsync(StartupItem item)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (item.Category == "Startup Folder")
                    {
                        if (File.Exists(item.Command)) File.Delete(item.Command);
                        return (true, null);
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
                            CreateNoWindow = true
                        };
                        using var p = Process.Start(psi);
                        p?.WaitForExit(10000);
                        return p?.ExitCode == 0
                            ? (true, (string)null)
                            : (false, $"schtasks /Delete failed (code {p?.ExitCode})");
                    }

                    bool isHkcu = item.RegistryKeyPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase)
                               || item.RegistryKeyPath.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase);
                    var hiveType = isHkcu ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;

                    string keyPath = item.RegistryKeyPath;
                    foreach (var prefix in new[] { "HKEY_LOCAL_MACHINE\\", "HKEY_CURRENT_USER\\", "HKLM\\", "HKCU\\" })
                        if (keyPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        { keyPath = keyPath.Substring(prefix.Length); break; }

                    using var root = StartupRegistryScanner.OpenHive64(hiveType);
                    using var key = root.OpenSubKey(keyPath, writable: true);
                    if (key == null)
                        return (false, $"Cannot open registry key: {item.RegistryKeyPath}");

                    key.DeleteValue(item.Name, throwOnMissingValue: false);

                    string approvedSubKey = StartupRegistryScanner.GetApprovedSubKey(keyPath);
                    using var approvedKey = root.OpenSubKey(approvedSubKey, writable: true);
                    approvedKey?.DeleteValue(item.Name, throwOnMissingValue: false);

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
            });
        }
    }
}