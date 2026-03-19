using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;

namespace WinOptimizerHub.Services
{
    public class RegistryCleanerService
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Wow64DisableWow64FsRedirection(out IntPtr oldValue);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Wow64RevertWow64FsRedirection(IntPtr oldValue);

        private static bool FileExistsReal(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            Wow64DisableWow64FsRedirection(out IntPtr old);
            try { return File.Exists(path); }
            finally { Wow64RevertWow64FsRedirection(old); }
        }

        private static bool DirectoryExistsReal(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            Wow64DisableWow64FsRedirection(out IntPtr old);
            try { return Directory.Exists(path); }
            finally { Wow64RevertWow64FsRedirection(old); }
        }

        private const int ScanTimeoutNormalMs = 5 * 60 * 1000;
        private const int ScanTimeoutDeepMs = 15 * 60 * 1000;

        public async Task<List<RegistryIssue>> ScanAsync(
            bool deepScan, IProgress<string> progress = null, CancellationToken ct = default)
        {
            int timeoutMs = deepScan ? ScanTimeoutDeepMs : ScanTimeoutNormalMs;
            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            var effectiveCt = linkedCts.Token;

            var issues = new List<RegistryIssue>();

            await Task.Run(() =>
            {
                progress?.Report("Scanning invalid file references...");
                ScanInvalidFileReferences(issues, effectiveCt);

                progress?.Report("Scanning uninstall entries...");
                ScanOrphanedUninstallEntries(issues, effectiveCt);

                progress?.Report("Scanning app paths...");
                ScanInvalidAppPaths(issues, effectiveCt);

                progress?.Report("Scanning shared DLLs...");
                ScanInvalidSharedDlls(issues, effectiveCt);

                progress?.Report("Scanning font references...");
                ScanInvalidFonts(issues, effectiveCt);

                progress?.Report("Scanning file type associations...");
                ScanOrphanedFileAssociations(issues, effectiveCt);

                progress?.Report("Scanning start menu items...");
                ScanInvalidStartMenu(issues, effectiveCt);

                if (deepScan)
                {
                    progress?.Report("Scanning MUI cache...");
                    ScanInvalidMuiCache(issues, effectiveCt);

                    progress?.Report("Scanning shell extensions...");
                    ScanOrphanedShellExtensions(issues, effectiveCt);

                    progress?.Report("Scanning user assist...");
                    ScanInvalidUserAssist(issues, effectiveCt);

                    progress?.Report("Scanning help files...");
                    ScanInvalidHelpFiles(issues, effectiveCt);

                    progress?.Report("Scanning COM objects...");
                    ScanOrphanedComObjects(issues, effectiveCt);
                }
            }, effectiveCt).ConfigureAwait(false);

            if (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
                progress?.Report($"⚠ Registry scan timed out after {timeoutMs / 60000} min — partial results returned.");

            return issues;
        }
        public static string LastBackupPath { get; private set; }

        public async Task<(int fixed_, int failed)> CleanAsync(
            IEnumerable<RegistryIssue> issues, IProgress<string> progress = null, CancellationToken ct = default)
        {
            var issueList = issues.Where(i => i.IsSelected).ToList();
            if (issueList.Count == 0) return (0, 0);

            progress?.Report("Creating registry backup...");
            LastBackupPath = await Task.Run(() => CreateRegBackup(issueList), ct).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(LastBackupPath))
                progress?.Report($"Backup saved: {LastBackupPath}");

            int fixedCount = 0, failed = 0;

            await Task.Run(() =>
            {
                foreach (var issue in issueList)
                {
                    if (ct.IsCancellationRequested) break;

                    progress?.Report($"Fixing: {issue.KeyPath}...");
                    try
                    {
                        DeleteRegistryValue(issue.KeyPath, issue.ValueName);
                        fixedCount++;
                    }
                    catch (Exception ex) { AppLogger.Log(ex, $"Registry.Clean:{issue.KeyPath}"); failed++; }
                }
            }, ct).ConfigureAwait(false);

            return (fixedCount, failed);
        }

        private static string CreateRegBackup(IEnumerable<RegistryIssue> issues)
        {
            try
            {
                string backupDir = AppLogger.BackupRoot;
                Directory.CreateDirectory(backupDir);

                string filePath = Path.Combine(backupDir,
                    $"registry_{DateTime.Now:yyyyMMdd_HHmmss}.reg");

                var sb = new StringBuilder();
                sb.AppendLine("Windows Registry Editor Version 5.00");
                sb.AppendLine();

                foreach (var group in issues.GroupBy(i => i.KeyPath))
                {
                    RegistryKey key = null;
                    try
                    {
                        key = OpenKeyForBackup(group.Key);
                        if (key == null) continue;

                        string formattedKey = group.Key.Replace("HKLM\\", "HKEY_LOCAL_MACHINE\\")
                            .Replace("HKCU\\", "HKEY_CURRENT_USER\\")
                            .Replace("HKCR\\", "HKEY_CLASSES_ROOT\\");

                        sb.AppendLine($"[{formattedKey}]");

                        foreach (var issue in group)
                        {
                            if (string.IsNullOrEmpty(issue.ValueName) || issue.ValueName == "(Default)")
                            {
                                sb.AppendLine($"; Subkey backup not supported — manually export: {group.Key}");
                                continue;
                            }

                            object val = key.GetValue(issue.ValueName, null,
                                RegistryValueOptions.DoNotExpandEnvironmentNames);
                            if (val == null) continue;

                            var kind = key.GetValueKind(issue.ValueName);
                            string regLine = FormatRegValue(issue.ValueName, val, kind);
                            if (regLine != null) sb.AppendLine(regLine);
                        }
                        sb.AppendLine();
                    }
                    catch (Exception ex) { AppLogger.Log(ex, "Registry.Backup"); }
                    finally { key?.Dispose(); }
                }

                File.WriteAllText(filePath, sb.ToString(), Encoding.Unicode);
                return filePath;
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, "Registry.CreateBackup");
                return null;
            }
        }

        private static RegistryKey OpenKeyForBackup(string fullPath)
        {
            var parts = fullPath.Split('\\');
            if (parts.Length < 2) return null;
            RegistryHive hive = parts[0] switch
            {
                "HKLM" => RegistryHive.LocalMachine,
                "HKCU" => RegistryHive.CurrentUser,
                "HKCR" => RegistryHive.ClassesRoot,
                _ => RegistryHive.LocalMachine
            };
            string sub = string.Join("\\", parts, 1, parts.Length - 1);
            return RegistryKey.OpenBaseKey(hive, RegistryView.Registry64).OpenSubKey(sub);
        }

        private static string FormatRegValue(string name, object val, RegistryValueKind kind)
        {
            string escapedName = name == "" ? "@" : $"\"{name.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
            return kind switch
            {
                RegistryValueKind.String =>
                    $"{escapedName}=\"{val.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"")}\"",
                RegistryValueKind.ExpandString =>
                    $"{escapedName}=hex(2):{StringToRegHex(val.ToString())}",
                RegistryValueKind.DWord =>
                    $"{escapedName}=dword:{Convert.ToUInt32(val):x8}",
                RegistryValueKind.QWord =>
                    $"{escapedName}=hex(b):{QWordToRegHex(Convert.ToUInt64(val))}",
                RegistryValueKind.Binary =>
                    $"{escapedName}=hex:{BitConverter.ToString((byte[])val).Replace("-", ",").ToLower()}",
                _ => null
            };
        }

        private static string StringToRegHex(string s)
        {
            var bytes = Encoding.Unicode.GetBytes(s + "\0");
            return string.Join(",", bytes.Select(b => b.ToString("x2")));
        }

        private static string QWordToRegHex(ulong v)
        {
            var bytes = BitConverter.GetBytes(v);
            return string.Join(",", bytes.Select(b => b.ToString("x2")));
        }

        private static RegistryKey OpenHive64(RegistryHive h) =>
            RegistryKey.OpenBaseKey(h, RegistryView.Registry64);

        private void ScanInvalidFileReferences(List<RegistryIssue> issues, CancellationToken ct)
        {
            const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            ScanKeyForInvalidPaths(RegistryHive.CurrentUser, keyPath, "Invalid Startup Reference", issues, ct);
            ScanKeyForInvalidPaths(RegistryHive.LocalMachine, keyPath, "Invalid Startup Reference", issues, ct);
        }

        private void ScanKeyForInvalidPaths(RegistryHive hiveType, string subKey,
            string issueType, List<RegistryIssue> issues, CancellationToken ct)
        {
            try
            {
                using var hive = OpenHive64(hiveType);
                using var key = hive.OpenSubKey(subKey);
                if (key == null) return;

                foreach (var valueName in key.GetValueNames())
                {
                    if (ct.IsCancellationRequested) return;
                    try
                    {
                        string value = key.GetValue(valueName)?.ToString() ?? "";
                        string exePath = ExtractPath(value);

                        if (!string.IsNullOrEmpty(exePath) && exePath.Length > 2
                            && !FileExistsReal(exePath) && !DirectoryExistsReal(exePath))
                        {
                            issues.Add(new RegistryIssue
                            {
                                KeyPath = $"{GetHiveName(hiveType)}\\{subKey}",
                                ValueName = valueName,
                                IssueType = issueType,
                                Description = $"Referenced file not found: {exePath}",
                                IsSafe = true,
                                IsSelected = true
                            });
                        }
                    }
                    catch (Exception ex) { AppLogger.Log(ex, "Registry.Scan"); }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, "Registry.Scan"); }
        }

        private void ScanOrphanedUninstallEntries(List<RegistryIssue> issues, CancellationToken ct)
        {
            const string uninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            ScanUninstallKey(RegistryHive.LocalMachine, uninstallKey, issues, ct);
            ScanUninstallKey(RegistryHive.CurrentUser, uninstallKey, issues, ct);
            ScanUninstallKey(RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", issues, ct);
        }

        private void ScanUninstallKey(RegistryHive hiveType, string subKey,
            List<RegistryIssue> issues, CancellationToken ct)
        {
            try
            {
                using var hive = OpenHive64(hiveType);
                using var key = hive.OpenSubKey(subKey);
                if (key == null) return;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    if (ct.IsCancellationRequested) return;
                    try
                    {
                        using var appKey = key.OpenSubKey(subKeyName);
                        if (appKey == null) continue;

                        string installLocation = appKey.GetValue("InstallLocation")?.ToString();
                        string displayName = appKey.GetValue("DisplayName")?.ToString();

                        if (string.IsNullOrEmpty(displayName)) continue;
                        if (!string.IsNullOrEmpty(installLocation) && installLocation.Length > 3
                            && !DirectoryExistsReal(installLocation))
                        {
                            issues.Add(new RegistryIssue
                            {
                                KeyPath = $"{GetHiveName(hiveType)}\\{subKey}\\{subKeyName}",
                                ValueName = "InstallLocation",
                                IssueType = "Orphaned Uninstall Entry",
                                Description = $"{displayName}: Install folder missing ({installLocation})",
                                IsSafe = true,
                                IsSelected = true
                            });
                        }
                    }
                    catch (Exception ex) { AppLogger.Log(ex, "Registry.Scan"); }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, "Registry.Scan"); }
        }

        private void ScanInvalidAppPaths(List<RegistryIssue> issues, CancellationToken ct)
        {
            const string appPathsKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
            try
            {
                using var hive = OpenHive64(RegistryHive.LocalMachine);
                using var key = hive.OpenSubKey(appPathsKey);
                if (key == null) return;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    if (ct.IsCancellationRequested) return;
                    try
                    {
                        using var appKey = key.OpenSubKey(subKeyName);
                        string path = appKey?.GetValue("")?.ToString();
                        if (!string.IsNullOrEmpty(path) && !FileExistsReal(path))
                        {
                            issues.Add(new RegistryIssue
                            {
                                KeyPath = $@"HKLM\{appPathsKey}\{subKeyName}",
                                ValueName = "(Default)",
                                IssueType = "Invalid App Path",
                                Description = $"App path not found: {path}",
                                IsSafe = true,
                                IsSelected = true
                            });
                        }
                    }
                    catch (Exception ex) { AppLogger.Log(ex, "Registry.Scan"); }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, "Registry.Scan"); }
        }

        private void ScanInvalidMuiCache(List<RegistryIssue> issues, CancellationToken ct)
        {
            const string muiCacheKey = @"SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache";
            try
            {
                using var hive = OpenHive64(RegistryHive.CurrentUser);
                using var key = hive.OpenSubKey(muiCacheKey);
                if (key == null) return;
                int count = 0;

                foreach (var valueName in key.GetValueNames())
                {
                    if (ct.IsCancellationRequested) return;
                    if (valueName.Contains(".") && !valueName.Contains("FriendlyAppName")) continue;

                    string exePath = valueName.Split('.')[0];
                    if (exePath.Length > 3 && exePath.Contains("\\") && !FileExistsReal(exePath) && count < 50)
                    {
                        issues.Add(new RegistryIssue
                        {
                            KeyPath = @"HKCU\" + muiCacheKey,
                            ValueName = valueName,
                            IssueType = "Obsolete MUI Cache",
                            Description = $"MUI cache for missing file: {exePath}",
                            IsSafe = true,
                            IsSelected = true
                        });
                        count++;
                    }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, "Registry.Scan"); }
        }

        private void ScanOrphanedShellExtensions(List<RegistryIssue> issues, CancellationToken ct)
        {
            const string shellExKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved";
            try
            {
                using var hive = OpenHive64(RegistryHive.LocalMachine);
                using var key = hive.OpenSubKey(shellExKey);
                if (key == null) return;

                foreach (var valueName in key.GetValueNames())
                {
                    if (ct.IsCancellationRequested) return;
                    string clsidPath = $@"SOFTWARE\Classes\CLSID\{valueName}";
                    using var clsidHive = OpenHive64(RegistryHive.ClassesRoot);
                    using var clsidKey = clsidHive.OpenSubKey($@"CLSID\{valueName}");
                    if (clsidKey == null)
                    {
                        string desc = key.GetValue(valueName)?.ToString();
                        issues.Add(new RegistryIssue
                        {
                            KeyPath = @"HKLM\" + shellExKey,
                            ValueName = valueName,
                            IssueType = "Orphaned Shell Extension",
                            Description = $"Missing CLSID for: {desc ?? valueName}",
                            IsSafe = true,
                            IsSelected = true
                        });
                    }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, "Registry.Scan"); }
        }

        private void ScanInvalidUserAssist(List<RegistryIssue> issues, CancellationToken ct)
        {
            const string userAssistKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\UserAssist";
            try
            {
                using var hive = OpenHive64(RegistryHive.CurrentUser);
                using var key = hive.OpenSubKey(userAssistKey);
                if (key == null) return;

                int total = 0;
                foreach (var sub in key.GetSubKeyNames())
                {
                    using var countKey = key.OpenSubKey(sub + "\\Count");
                    if (countKey != null) total += countKey.ValueCount;
                }
                if (total > 200)
                {
                    issues.Add(new RegistryIssue
                    {
                        KeyPath = @"HKCU\" + userAssistKey,
                        ValueName = "",
                        IssueType = "Bloated UserAssist Cache",
                        Description = $"{total} program execution history entries",
                        IsSafe = true,
                        IsSelected = false
                    });
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, "Registry.Scan"); }
        }

        private void ScanInvalidSharedDlls(List<RegistryIssue> issues, CancellationToken ct)
        {
            const string key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs";
            try
            {
                using var hive = OpenHive64(RegistryHive.LocalMachine);
                using var k = hive.OpenSubKey(key);
                if (k == null) return;
                foreach (var valueName in k.GetValueNames())
                {
                    if (ct.IsCancellationRequested) return;
                    string path = Environment.ExpandEnvironmentVariables(valueName);
                    if (!string.IsNullOrEmpty(path) && path.Contains("\\") && !FileExistsReal(path))
                        issues.Add(new RegistryIssue
                        {
                            KeyPath = $@"HKLM\{key}",
                            ValueName = valueName,
                            IssueType = "Invalid Shared DLL",
                            Description = $"DLL not found: {path}",
                            IsSafe = true,
                            IsSelected = true
                        });
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(ScanInvalidSharedDlls)); }
        }

        private void ScanInvalidFonts(List<RegistryIssue> issues, CancellationToken ct)
        {
            const string key = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";
            string fontsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
            try
            {
                using var hive = OpenHive64(RegistryHive.LocalMachine);
                using var k = hive.OpenSubKey(key);
                if (k == null) return;
                foreach (var valueName in k.GetValueNames())
                {
                    if (ct.IsCancellationRequested) return;
                    string fontFile = k.GetValue(valueName)?.ToString() ?? "";
                    if (string.IsNullOrEmpty(fontFile)) continue;

                    string fullPath = fontFile.Contains("\\") ? fontFile
                                    : Path.Combine(fontsDir, fontFile);
                    fullPath = Environment.ExpandEnvironmentVariables(fullPath);
                    if (!FileExistsReal(fullPath))
                        issues.Add(new RegistryIssue
                        {
                            KeyPath = $@"HKLM\{key}",
                            ValueName = valueName,
                            IssueType = "Invalid Font Reference",
                            Description = $"Font file not found: {fontFile}",
                            IsSafe = true,
                            IsSelected = true
                        });
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(ScanInvalidFonts)); }
        }

        private void ScanOrphanedFileAssociations(List<RegistryIssue> issues, CancellationToken ct)
        {
            const string key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts";
            try
            {
                using var hive = OpenHive64(RegistryHive.CurrentUser);
                using var k = hive.OpenSubKey(key);
                if (k == null) return;
                foreach (var ext in k.GetSubKeyNames())
                {
                    if (ct.IsCancellationRequested) return;
                    try
                    {
                        using var extKey = k.OpenSubKey(ext + "\\UserChoice");
                        string progId = extKey?.GetValue("ProgId")?.ToString();
                        if (string.IsNullOrEmpty(progId)) continue;

                        using var cr = OpenHive64(RegistryHive.ClassesRoot);
                        using var pg = cr.OpenSubKey(progId);
                        if (pg == null)
                            issues.Add(new RegistryIssue
                            {
                                KeyPath = $@"HKCU\{key}\{ext}\UserChoice",
                                ValueName = "ProgId",
                                IssueType = "Orphaned File Association",
                                Description = $"File type {ext} points to missing ProgId: {progId}",
                                IsSafe = false,
                                IsSelected = false
                            });
                    }
                    catch  { AppLogger.Log(new Exception("Unhandled"), nameof(AppLogger)); }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(ScanOrphanedFileAssociations)); }
        }

        private void ScanInvalidStartMenu(List<RegistryIssue> issues, CancellationToken ct)
        {
            string[] startMenuPaths = {
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            };
            foreach (var folder in startMenuPaths)
            {
                if (!Directory.Exists(folder)) continue;
                foreach (var lnk in Directory.GetFiles(folder, "*.lnk", SearchOption.AllDirectories))
                {
                    if (ct.IsCancellationRequested) return;
                    try
                    {
                        var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                        var shortcut = shell.GetType().InvokeMember("CreateShortcut",
                            System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                        string target = shortcut.GetType().InvokeMember("TargetPath",
                            System.Reflection.BindingFlags.GetProperty, null, shortcut, null)?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(target) && target.Contains("\\")
                            && !FileExistsReal(target) && !DirectoryExistsReal(target))
                            issues.Add(new RegistryIssue
                            {
                                KeyPath = folder,
                                ValueName = Path.GetFileNameWithoutExtension(lnk),
                                IssueType = "Invalid Start Menu Item",
                                Description = $"Shortcut target missing: {target}",
                                IsSafe = true,
                                IsSelected = true
                            });
                    }
                    catch  { AppLogger.Log(new Exception("Unhandled"), nameof(AppLogger)); }
                }
            }
        }

        private void ScanInvalidHelpFiles(List<RegistryIssue> issues, CancellationToken ct)
        {
            const string key = @"SOFTWARE\Microsoft\Windows\HTMLHelp";
            try
            {
                using var hive = OpenHive64(RegistryHive.LocalMachine);
                using var k = hive.OpenSubKey(key);
                if (k == null) return;
                foreach (var valueName in k.GetValueNames())
                {
                    if (ct.IsCancellationRequested) return;
                    string path = Environment.ExpandEnvironmentVariables(k.GetValue(valueName)?.ToString() ?? "");
                    if (!string.IsNullOrEmpty(path) && path.Contains("\\") && !FileExistsReal(path))
                        issues.Add(new RegistryIssue
                        {
                            KeyPath = $@"HKLM\{key}",
                            ValueName = valueName,
                            IssueType = "Invalid Help File",
                            Description = $"Help file not found: {path}",
                            IsSafe = true,
                            IsSelected = true
                        });
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(ScanInvalidHelpFiles)); }
        }

        private void ScanOrphanedComObjects(List<RegistryIssue> issues, CancellationToken ct)
        {
            const string key = @"CLSID";
            int count = 0;
            const int maxCom = 100;
            try
            {
                using var cr = OpenHive64(RegistryHive.ClassesRoot);
                using var k = cr.OpenSubKey(key);
                if (k == null) return;
                foreach (var clsid in k.GetSubKeyNames())
                {
                    if (ct.IsCancellationRequested || count >= maxCom) return;
                    try
                    {
                        using var clsidKey = k.OpenSubKey(clsid);
                        using var inprocKey = clsidKey?.OpenSubKey("InProcServer32");
                        string server = inprocKey?.GetValue("")?.ToString();
                        if (string.IsNullOrEmpty(server)) continue;
                        server = Environment.ExpandEnvironmentVariables(server.Trim('"'));
                        if (server.Contains("\\") && !FileExistsReal(server))
                        {
                            string name = clsidKey.GetValue("")?.ToString() ?? clsid;
                            issues.Add(new RegistryIssue
                            {
                                KeyPath = $@"HKCR\CLSID\{clsid}\InProcServer32",
                                ValueName = "(Default)",
                                IssueType = "Orphaned COM Object",
                                Description = $"{name}: server DLL missing ({server})",
                                IsSafe = false,
                                IsSelected = false
                            });
                            count++;
                        }
                    }
                    catch  { AppLogger.Log(new Exception("Unhandled"), nameof(AppLogger)); }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(ScanOrphanedComObjects)); }
        }

        private static void DeleteRegistryValue(string fullKeyPath, string valueName)
        {
            var parts = fullKeyPath.Split('\\');
            if (parts.Length < 2) return;

            RegistryHive hiveType = parts[0] switch
            {
                "HKLM" => RegistryHive.LocalMachine,
                "HKCU" => RegistryHive.CurrentUser,
                "HKCR" => RegistryHive.ClassesRoot,
                _ => RegistryHive.LocalMachine
            };

            string subPath = string.Join("\\", parts, 1, parts.Length - 1);
            int lastSlash = subPath.LastIndexOf('\\');
            string parentPath = lastSlash > 0 ? subPath.Substring(0, lastSlash) : "";
            string leafName = lastSlash > 0 ? subPath.Substring(lastSlash + 1) : subPath;

            using var hive64 = RegistryKey.OpenBaseKey(hiveType, RegistryView.Registry64);

            if (string.IsNullOrEmpty(valueName) || valueName == "(Default)")
            {
                if (string.IsNullOrEmpty(parentPath))
                {
                    hive64.DeleteSubKeyTree(leafName, throwOnMissingSubKey: false);
                }
                else
                {
                    using var parentKey = hive64.OpenSubKey(parentPath, writable: true);
                    if (parentKey != null)
                    {
                        try { parentKey.DeleteSubKeyTree(leafName, throwOnMissingSubKey: false); }
                        catch { parentKey.DeleteSubKey(leafName, throwOnMissingSubKey: false); }
                    }
                }
            }
            else
            {
                using var key = hive64.OpenSubKey(subPath, writable: true);
                key?.DeleteValue(valueName, throwOnMissingValue: false);
            }
        }

        private static string ExtractPath(string command)
        {
            if (string.IsNullOrEmpty(command)) return string.Empty;
            command = Environment.ExpandEnvironmentVariables(command.Trim());
            if (command.StartsWith("\"")) command = command.Substring(1);
            int quote = command.IndexOf('"');
            int space = command.IndexOf(' ');
            int end = quote >= 0 ? quote : space >= 0 ? space : command.Length;
            return command.Substring(0, end).Trim();
        }

        private static string GetHiveName(RegistryHive hive) => hive switch
        {
            RegistryHive.LocalMachine => "HKLM",
            RegistryHive.CurrentUser => "HKCU",
            RegistryHive.ClassesRoot => "HKCR",
            _ => "HKLM"
        };
    }
}