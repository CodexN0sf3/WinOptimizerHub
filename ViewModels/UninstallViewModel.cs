using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;
using WinOptimizerHub.Services;

namespace WinOptimizerHub.ViewModels
{
    public class UninstallViewModel : BaseViewModel
    {
        private readonly UninstallManagerService _svc;
        private readonly MainViewModel _main;

        private List<InstalledProgram> _allWin32 = new List<InstalledProgram>();
        private List<InstalledProgram> _allWinApps = new List<InstalledProgram>();

        private string _activeTab = "Win32";
        public string ActiveTab
        {
            get => _activeTab;
            set { SetProperty(ref _activeTab, value); ApplySearch(); InvalidateCommands(); }
        }

        private List<InstalledProgram> _programs = new List<InstalledProgram>();
        public List<InstalledProgram> Programs
        {
            get => _programs;
            private set { SetProperty(ref _programs, value); OnPropertyChanged(nameof(ProgramCount)); }
        }

        public string ProgramCount => $"{_programs.Count} items";

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { SetProperty(ref _searchText, value); ApplySearch(); }
        }

        private InstalledProgram _selectedProgram;
        public InstalledProgram SelectedProgram
        {
            get => _selectedProgram;
            set { SetProperty(ref _selectedProgram, value); InvalidateCommands(); }
        }

        private bool _isUninstalling;
        public bool IsUninstalling
        {
            get => _isUninstalling;
            private set { SetProperty(ref _isUninstalling, value); InvalidateCommands(); }
        }

        private bool _loadingWin32;
        public bool LoadingWin32
        {
            get => _loadingWin32;
            set => SetProperty(ref _loadingWin32, value);
        }

        private bool _loadingWinApps;
        public bool LoadingWinApps
        {
            get => _loadingWinApps;
            private set => SetProperty(ref _loadingWinApps, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand UninstallCommand { get; }
        public ICommand SwitchTabWin32Command { get; }
        public ICommand SwitchTabWinAppsCommand { get; }
        public ICommand OpenInstallFolderCommand { get; }
        public ICommand CopyNameCommand { get; }
        public ICommand CopyUninstallStringCommand { get; }
        public ICommand ForceUninstallCommand { get; }

        public UninstallViewModel(ObservableCollection<string> log, UninstallManagerService svc, MainViewModel main) : base(log)
        {
            _svc = svc;
            _main = main;

            RefreshCommand = new AsyncRelayCommand(LoadAsync);
            UninstallCommand = new AsyncRelayCommand(UninstallAsync,
                () => SelectedProgram != null && !IsUninstalling);
            SwitchTabWin32Command = new RelayCommand(() => ActiveTab = "Win32");
            SwitchTabWinAppsCommand = new RelayCommand(() => ActiveTab = "WindowsApps");

            OpenInstallFolderCommand = new RelayCommand(OpenInstallFolder,
                () => SelectedProgram?.IsWindowsApp == false);
            CopyNameCommand = new RelayCommand(CopyName,
                () => SelectedProgram != null);
            CopyUninstallStringCommand = new RelayCommand(CopyUninstallString,
                () => SelectedProgram?.IsWindowsApp == false
                   && !string.IsNullOrEmpty(SelectedProgram?.UninstallString));
            ForceUninstallCommand = new AsyncRelayCommand(
                ForceUninstallAsync,
                () => SelectedProgram != null && !IsUninstalling && SelectedProgram?.IsWindowsApp == false);
        }

        public async Task LoadAsync()
        {
            LoadingWin32 = true;
            LoadingWinApps = true;
            SetBusy(true, "Loading installed programs...");

            var win32Task = Task.Run(async () =>
            {
                try
                {
                    _allWin32 = await _svc.GetInstalledProgramsAsync();
                    LoadingWin32 = false;
                    if (ActiveTab == "Win32") ApplySearch();
                }
                catch (Exception ex) { AppLogger.Log(ex, "LoadWin32"); LoadingWin32 = false; }
            });

            var storeTask = Task.Run(async () =>
            {
                try
                {
                    StatusMessage = "Loading Windows Store apps (PowerShell)...";
                    _allWinApps = await _svc.GetWindowsAppsAsync();
                    LoadingWinApps = false;
                    if (ActiveTab == "WindowsApps") ApplySearch();
                    Log($"Windows Apps: {_allWinApps.Count} packages");
                }
                catch (Exception ex) { AppLogger.Log(ex, "LoadWinApps"); LoadingWinApps = false; }
            });

            await Task.WhenAll(win32Task, storeTask);
            ApplySearch();
            SetBusy(false, $"Win32: {_allWin32.Count}  |  Store: {_allWinApps.Count}  — {DateTime.Now:HH:mm}");
        }

        private void ApplySearch()
        {
            var source = ActiveTab == "WindowsApps" ? _allWinApps : _allWin32;
            string q = SearchText.Trim().ToLowerInvariant();
            var result = string.IsNullOrEmpty(q)
                ? source.ToList()
                : source.Where(p =>
                    p.Name.ToLowerInvariant().Contains(q) ||
                    p.Publisher.ToLowerInvariant().Contains(q)).ToList();

            if (Application.Current?.Dispatcher.CheckAccess() == false)
                Application.Current.Dispatcher.Invoke(() => Programs = result);
            else
                Programs = result;
        }

        private async Task UninstallAsync()
        {
            if (SelectedProgram == null || IsUninstalling) return;

            string name = SelectedProgram.Name;

            string body = SelectedProgram.IsWindowsApp
                ? BuildStoreConfirmMessage(SelectedProgram)
                : $"Uninstall '{name}'?\n\nThe program's own uninstaller will open.";

            if (SelectedProgram.IsSystemApp
                    ? !DialogService.ConfirmWarning("Confirm Uninstall", body, "Uninstall", "Cancel")
                    : !DialogService.Confirm("Confirm Uninstall", body, "Uninstall", "Cancel")) return;

            IsUninstalling = true;
            SetBusy(true, $"Uninstalling {name}...");

            try
            {
                var (ok, error) = await _svc.UninstallProgramAsync(SelectedProgram, MakeProgress());

                SetBusy(true, "Refreshing program list...");

                if (SelectedProgram?.IsWindowsApp == true)
                    await Task.Delay(1500);
                await LoadAsync();
                SetBusy(false);

                if (ok)
                {
                    Log($"Uninstalled: {name}");
                    _main.Toast.ShowSuccess($"Uninstalled: {name}",
                        SelectedProgram?.IsWindowsApp == true
                            ? "Windows Store app removed."
                            : "Program removed successfully.");
                }
                else
                {
                    Log($"Uninstall failed: {name} — {error}");
                    _main.Toast.ShowError($"Failed to uninstall: {name}",
                        string.IsNullOrEmpty(error) ? "Unknown error." : error);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, nameof(UninstallAsync));
                SetBusy(false);
                _main.Toast.ShowError($"Error during uninstall: {name}", ex.Message);
            }
            finally
            {
                IsUninstalling = false;
            }
        }

        private static string BuildStoreConfirmMessage(InstalledProgram app)
        {
            string warn = app.IsSystemApp
                ? "\n\n⚠  This is a system app — removing it may affect Windows functionality."
                : "";
            return $"Remove Windows Store app '{app.Name}'?{warn}\n\nCommand: Remove-AppxPackage\n\nContinue?";
        }

        private void OpenInstallFolder()
        {
            if (SelectedProgram == null || SelectedProgram.IsWindowsApp) return;
            try
            {
                string dir = ResolveInstallFolder(SelectedProgram);
                if (!string.IsNullOrEmpty(dir))
                    Process.Start("explorer.exe", dir);
                else
                    _main.Toast.ShowWarning("Uninstall Manager", $"Could not find install folder for '{SelectedProgram.Name}'.");
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(OpenInstallFolder)); }
        }

        private static string ResolveInstallFolder(InstalledProgram p)
        {
            if (!string.IsNullOrEmpty(p.InstallLocation))
            {
                string loc = p.InstallLocation.Trim('"').TrimEnd('\\', '/');
                if (Directory.Exists(loc)) return loc;
            }

            if (!string.IsNullOrEmpty(p.DisplayIcon))
            {
                string icon = p.DisplayIcon.Split(',')[0].Trim('"');
                icon = Environment.ExpandEnvironmentVariables(icon);
                if (File.Exists(icon))
                {
                    string d = Path.GetDirectoryName(icon);
                    if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) return d;
                }
            }

            if (!string.IsNullOrEmpty(p.UninstallString))
            {
                string raw = p.UninstallString.Trim();
                string exe = raw.StartsWith("\"")
                    ? raw.Substring(1, Math.Max(0, raw.IndexOf('"', 1) - 1))
                    : raw.Split(' ')[0];
                exe = Environment.ExpandEnvironmentVariables(exe);
                if (File.Exists(exe))
                {
                    string d = Path.GetDirectoryName(exe) ?? "";
                    if (d.EndsWith("Uninstall", StringComparison.OrdinalIgnoreCase) ||
                        d.EndsWith("Setup", StringComparison.OrdinalIgnoreCase) ||
                        d.EndsWith("Installer", StringComparison.OrdinalIgnoreCase))
                        d = Path.GetDirectoryName(d) ?? d;
                    if (Directory.Exists(d)) return d;
                }
            }

            string firstName = p.Name.Split(' ')[0];
            string[] roots =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
            };
            foreach (var root in roots)
            {
                if (!Directory.Exists(root)) continue;
                string exact = Path.Combine(root, p.Name);
                if (Directory.Exists(exact)) return exact;
                if (!string.IsNullOrEmpty(p.Publisher))
                {
                    string pubDir = Path.Combine(root, p.Publisher.Split(' ')[0], p.Name);
                    if (Directory.Exists(pubDir)) return pubDir;
                }
                try
                {
                    foreach (var dir in Directory.GetDirectories(root))
                    {
                        if (Path.GetFileName(dir).IndexOf(firstName,
                                StringComparison.OrdinalIgnoreCase) >= 0)
                            return dir;
                    }
                }
                catch { }
            }

            return string.Empty;
        }

        private void CopyName()
        {
            if (SelectedProgram == null) return;
            try { Clipboard.SetText(SelectedProgram.Name); } catch { }
        }

        private void CopyUninstallString()
        {
            if (SelectedProgram?.UninstallString == null) return;
            try { Clipboard.SetText(SelectedProgram.UninstallString); } catch { }
        }

        private async Task ForceUninstallAsync()
        {
            if (SelectedProgram == null) return;
            if (SelectedProgram.IsWindowsApp)
            {
                _main.Toast.ShowInfo("Force Uninstall", "Use the standard Uninstall button for Store apps.");
                return;
            }

            string installDir = ResolveInstallFolder(SelectedProgram);

            string plan = $"Force remove '{SelectedProgram.Name}'?\n\n"
                        + "This will:\n"
                        + "  ✓ Remove uninstall registry entries\n"
                        + "  ✓ Clean App Paths registry entries\n"
                        + "  ✓ Clean Active Setup components (HKLM\\...\\Active Setup)\n"
                        + "  ✓ Clean Services registry keys (HKLM\\SYSTEM\\...\\Services)\n"
                        + (string.IsNullOrEmpty(installDir)
                            ? "  ✗ Install folder not found (skipped)\n"
                            : $"  ✓ Delete install folder: {installDir}\n")
                        + "  ✓ Remove Start Menu & Desktop shortcuts\n"
                        + "  ✓ Remove associated Scheduled Tasks\n"
                        + "  ✓ Stop & remove associated Windows Services\n"
                        + "  ✓ Remove Windows Firewall rules\n\n"
                        + "⚠ This is irreversible. Use only when the normal uninstaller is broken.";

            if (!DialogService.ConfirmDanger("Force Uninstall — Confirm", plan,
                    "Force Uninstall", "Cancel")) return;

            IsUninstalling = true;
            SetBusy(true, $"Force removing {SelectedProgram.Name}...");
            var program = SelectedProgram;
            var capturedDir = installDir;

            await Task.Run(() =>
            {
                var log = new System.Text.StringBuilder();
                void Report(string msg)
                {
                    log.AppendLine(msg);
                    Application.Current?.Dispatcher?.Invoke(() => SetBusy(true, msg));
                }

                Report("Removing uninstall registry entries...");
                RemoveUninstallKeys(program, Report);

                Report("Cleaning App Paths...");
                RemoveAppPaths(program, capturedDir, Report);

                Report("Cleaning Active Setup components...");
                RemoveActiveSetupComponents(program, capturedDir, Report);

                Report("Cleaning Services registry entries...");
                RemoveServiceRegistryKeys(program, capturedDir, Report);

                if (!string.IsNullOrEmpty(capturedDir) && Directory.Exists(capturedDir))
                {
                    Report($"Deleting install folder: {capturedDir}...");
                    DeleteFolderSafe(capturedDir, Report);
                }

                Report("Removing shortcuts...");
                RemoveShortcuts(program, capturedDir, Report);

                Report("Removing scheduled tasks...");
                RemoveScheduledTasks(program, capturedDir, Report);

                Report("Stopping and removing services...");
                RemoveServices(program, capturedDir, Report);

                Report("Removing firewall rules...");
                RemoveFirewallRules(program, capturedDir, Report);

                Log($"Force remove complete: {program.Name}\n{log}");
            });

            SetBusy(false);
            IsUninstalling = false;
            _main.Toast.ShowInfo("Force Uninstall", $"'{program.Name}' removed successfully");
            await LoadAsync();
        }

        private static void RemoveUninstallKeys(InstalledProgram p, Action<string> report)
        {
            string[] paths =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };
            foreach (var path in paths)
            {
                try
                {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(path, writable: true))
                    {
                        if (key == null) continue;
                        try { key.DeleteSubKeyTree(p.RegistryKey, false); report($"  Removed HKLM\\{path}\\{p.RegistryKey}"); }
                        catch { }
                    }
                }
                catch { }
            }
            try
            {
                using (var hkcu = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", writable: true))
                {
                    try { hkcu?.DeleteSubKeyTree(p.RegistryKey, false); report("  Removed HKCU uninstall key"); }
                    catch { }
                }
            }
            catch { }
        }

        private static void RemoveAppPaths(InstalledProgram p, string installDir, Action<string> report)
        {
            try
            {
                string appPathsKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(appPathsKey, writable: true))
                {
                    if (key == null) return;
                    var toDelete = new List<string>();
                    foreach (var sub in key.GetSubKeyNames())
                    {
                        try
                        {
                            using (var subKey = key.OpenSubKey(sub))
                            {
                                string path = subKey?.GetValue(null)?.ToString() ?? "";
                                bool match = path.IndexOf(p.Name, StringComparison.OrdinalIgnoreCase) >= 0
                                          || (!string.IsNullOrEmpty(installDir) &&
                                              path.IndexOf(installDir, StringComparison.OrdinalIgnoreCase) >= 0);
                                if (match) toDelete.Add(sub);
                            }
                        }
                        catch { }
                    }
                    foreach (var sub in toDelete)
                    {
                        try { key.DeleteSubKeyTree(sub, false); report($"  Removed App Path: {sub}"); }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private static void RemoveActiveSetupComponents(InstalledProgram p, string installDir, Action<string> report)
        {
            string[] activeSetupPaths =
            {
                @"SOFTWARE\Microsoft\Active Setup\Installed Components",
                @"SOFTWARE\WOW6432Node\Microsoft\Active Setup\Installed Components",
            };

            foreach (var keyPath in activeSetupPaths)
            {
                try
                {
                    using (var root = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath, writable: true))
                    {
                        if (root == null) continue;
                        var toDelete = new List<string>();
                        foreach (var subName in root.GetSubKeyNames())
                        {
                            try
                            {
                                using (var sub = root.OpenSubKey(subName))
                                {
                                    string stubPath = Environment.ExpandEnvironmentVariables(
                                        sub?.GetValue("StubPath")?.ToString() ?? "").Trim('"');
                                    string compName = sub?.GetValue(null)?.ToString() ?? "";

                                    bool pathMatch = !string.IsNullOrEmpty(installDir)
                                                  && stubPath.StartsWith(installDir, StringComparison.OrdinalIgnoreCase);
                                    bool nameMatch = compName.IndexOf(p.Name.Split(' ')[0],
                                                        StringComparison.OrdinalIgnoreCase) >= 0
                                                  || (!string.IsNullOrEmpty(p.Publisher) && p.Publisher.Length > 3
                                                      && compName.IndexOf(p.Publisher.Split(' ')[0],
                                                          StringComparison.OrdinalIgnoreCase) >= 0);

                                    if (pathMatch || nameMatch) toDelete.Add(subName);
                                }
                            }
                            catch { }
                        }
                        foreach (var name in toDelete)
                        {
                            try { root.DeleteSubKeyTree(name, false); report($"  Removed Active Setup: {name}"); }
                            catch { }
                        }
                    }
                }
                catch { }
            }
        }

        private static void RemoveServiceRegistryKeys(InstalledProgram p, string installDir, Action<string> report)
        {
            try
            {
                using (var servicesRoot = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Services", writable: true))
                {
                    if (servicesRoot == null) return;
                    var toDelete = new List<string>();

                    foreach (var svcName in servicesRoot.GetSubKeyNames())
                    {
                        try
                        {
                            using (var svcKey = servicesRoot.OpenSubKey(svcName))
                            {
                                string imagePath = Environment.ExpandEnvironmentVariables(
                                    svcKey?.GetValue("ImagePath")?.ToString() ?? "").Trim('"');
                                string displayName = svcKey?.GetValue("DisplayName")?.ToString() ?? "";

                                bool pathMatch = !string.IsNullOrEmpty(installDir)
                                              && imagePath.StartsWith(installDir, StringComparison.OrdinalIgnoreCase);
                                bool nameMatch = displayName.IndexOf(p.Name.Split(' ')[0],
                                                     StringComparison.OrdinalIgnoreCase) >= 0
                                              || (!string.IsNullOrEmpty(p.Publisher) && p.Publisher.Length > 3
                                                  && displayName.IndexOf(p.Publisher.Split(' ')[0],
                                                      StringComparison.OrdinalIgnoreCase) >= 0);

                                if (pathMatch || nameMatch) toDelete.Add(svcName);
                            }
                        }
                        catch { }
                    }

                    foreach (var name in toDelete)
                    {
                        try
                        {
                            try
                            {
                                using (var sc = new System.ServiceProcess.ServiceController(name))
                                {
                                    if (sc.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                                    {
                                        sc.Stop();
                                        sc.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Stopped,
                                            TimeSpan.FromSeconds(5));
                                    }
                                }
                            }
                            catch { }
                            servicesRoot.DeleteSubKeyTree(name, false);
                            report($"  Removed service key: HKLM\\SYSTEM\\...\\Services\\{name}");
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex) { report($"  Services registry warning: {ex.Message}"); }
        }

        private static void DeleteFolderSafe(string dir, Action<string> report)
        {
            try
            {
                foreach (var proc in Process.GetProcesses())
                {
                    try
                    {
                        string exe = proc.MainModule?.FileName ?? "";
                        if (exe.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
                        {
                            proc.Kill();
                            report($"  Killed process: {proc.ProcessName}");
                        }
                    }
                    catch { }
                    finally { proc.Dispose(); }
                }
                Directory.Delete(dir, recursive: true);
                report($"  Deleted: {dir}");
            }
            catch (Exception ex)
            {
                report($"  Warning: Could not fully delete folder — {ex.Message}");
                try
                {
                    foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            File.SetAttributes(file, FileAttributes.Normal);
                            File.Delete(file);
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        private static void RemoveShortcuts(InstalledProgram p, string installDir, Action<string> report)
        {
            var searchPaths = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            };

            foreach (var searchPath in searchPaths)
            {
                if (!Directory.Exists(searchPath)) continue;
                try
                {
                    foreach (var lnk in Directory.GetFiles(searchPath, "*.lnk", SearchOption.AllDirectories))
                    {
                        try
                        {
                            string lnkName = Path.GetFileNameWithoutExtension(lnk);
                            bool nameMatch = lnkName.IndexOf(p.Name.Split(' ')[0],
                                                 StringComparison.OrdinalIgnoreCase) >= 0;
                            bool pathMatch = !string.IsNullOrEmpty(installDir) &&
                                              ReadLnkTarget(lnk).StartsWith(installDir,
                                                  StringComparison.OrdinalIgnoreCase);

                            if (nameMatch || pathMatch)
                            {
                                File.Delete(lnk);
                                report($"  Removed shortcut: {lnk}");
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        private static string ReadLnkTarget(string lnkPath)
        {
            try
            {
                byte[] data = File.ReadAllBytes(lnkPath);
                if (data.Length < 76) return "";
                return System.Text.Encoding.Unicode.GetString(data);
            }
            catch { return ""; }
        }

        private static void RemoveScheduledTasks(InstalledProgram p, string installDir, Action<string> report)
        {
            try
            {
                string output = CommandHelper.RunSync("schtasks.exe", "/query /fo CSV /nh", timeoutMs: 10000);
                foreach (var line in output.Split('\n'))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string[] parts = line.Split(',');
                    if (parts.Length < 1) continue;
                    string taskName = parts[0].Trim('"', ' ', '\r');
                    if (string.IsNullOrEmpty(taskName)) continue;

                    bool match = taskName.IndexOf(p.Name.Split(' ')[0],
                                     StringComparison.OrdinalIgnoreCase) >= 0
                              || (!string.IsNullOrEmpty(p.Publisher) && p.Publisher.Length > 3 &&
                                  taskName.IndexOf(p.Publisher.Split(' ')[0],
                                      StringComparison.OrdinalIgnoreCase) >= 0);
                    if (match)
                    {
                        try
                        {
                            CommandHelper.RunSync("schtasks.exe",
                                $"/delete /tn \"{taskName}\" /f", timeoutMs: 5000);
                            report($"  Removed task: {taskName}");
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex) { report($"  Tasks warning: {ex.Message}"); }
        }

        private static void RemoveServices(InstalledProgram p, string installDir, Action<string> report)
        {
            if (string.IsNullOrEmpty(installDir)) return;
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Services"))
                {
                    if (key == null) return;
                    foreach (var svcName in key.GetSubKeyNames())
                    {
                        try
                        {
                            using (var svcKey = key.OpenSubKey(svcName))
                            {
                                string imagePath = Environment.ExpandEnvironmentVariables(
                                    svcKey?.GetValue("ImagePath")?.ToString() ?? "").Trim('"');
                                if (imagePath.StartsWith(installDir, StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        using (var sc = new System.ServiceProcess.ServiceController(svcName))
                                        {
                                            if (sc.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                                                sc.Stop();
                                        }
                                    }
                                    catch { }
                                    CommandHelper.RunSync("sc.exe", $"delete \"{svcName}\"", timeoutMs: 5000);
                                    report($"  Removed service: {svcName}");
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex) { report($"  Services warning: {ex.Message}"); }
        }

        private static void RemoveFirewallRules(InstalledProgram p, string installDir, Action<string> report)
        {
            try
            {
                string output = CommandHelper.RunSync("netsh",
                    "advfirewall firewall show rule name=all verbose", timeoutMs: 15000);
                string currentRule = "";
                foreach (var line in output.Split('\n'))
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("Rule Name:"))
                        currentRule = trimmed.Replace("Rule Name:", "").Trim();
                    else if (trimmed.StartsWith("Program:") && !string.IsNullOrEmpty(currentRule))
                    {
                        string prog = trimmed.Replace("Program:", "").Trim();
                        bool match = (!string.IsNullOrEmpty(installDir) &&
                                      prog.StartsWith(installDir, StringComparison.OrdinalIgnoreCase))
                                  || prog.IndexOf(p.Name.Split(' ')[0],
                                         StringComparison.OrdinalIgnoreCase) >= 0;
                        if (match)
                        {
                            try
                            {
                                CommandHelper.RunSync("netsh",
                                    $"advfirewall firewall delete rule name=\"{currentRule}\"",
                                    timeoutMs: 5000);
                                report($"  Removed firewall rule: {currentRule}");
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex) { report($"  Firewall warning: {ex.Message}"); }
        }
    }
}