using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Services;

namespace WinOptimizerHub.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly AppServices _svc;

        private RAMOptimizerService _ramSvc => _svc.Ram;
        private ToastNotificationService _toast => _svc.Toast;
        private DashboardHistoryService _histSvc => _svc.DashboardHistory;
        private BrowserCacheService _browserSvc => _svc.Browser;

        private readonly DispatcherTimer _monitorTimer = new DispatcherTimer();

        private PerformanceCounter _cpuCounter;
        private readonly object _cpuLock = new object();
        private bool _cpuReadPending;

        private long _sessionFreedBytes;

        private float _cpuPercent;
        public float CpuPercent
        {
            get => _cpuPercent;
            private set => SetProperty(ref _cpuPercent, value);
        }

        private double _ramUsedGb;
        public double RamUsedGb
        {
            get => _ramUsedGb;
            private set => SetProperty(ref _ramUsedGb, value);
        }

        private double _ramTotalGb;
        public double RamTotalGb
        {
            get => _ramTotalGb;
            private set => SetProperty(ref _ramTotalGb, value);
        }

        private double _ramPercent;
        public double RamPercent
        {
            get => _ramPercent;
            private set => SetProperty(ref _ramPercent, value);
        }

        private double _diskUsedGb;
        public double DiskUsedGb
        {
            get => _diskUsedGb;
            private set => SetProperty(ref _diskUsedGb, value);
        }

        private double _diskTotalGb;
        public double DiskTotalGb
        {
            get => _diskTotalGb;
            private set => SetProperty(ref _diskTotalGb, value);
        }

        private double _diskPercent;
        public double DiskPercent
        {
            get => _diskPercent;
            private set => SetProperty(ref _diskPercent, value);
        }

        private string _uptimeDisplay = "--";
        public string UptimeDisplay
        {
            get => _uptimeDisplay;
            private set => SetProperty(ref _uptimeDisplay, value);
        }

        private string _osName = "Windows";
        public string OsName
        {
            get => _osName;
            set => SetProperty(ref _osName, value);
        }

        private string _computerName = Environment.MachineName;
        public string ComputerName
        {
            get => _computerName;
            set => SetProperty(ref _computerName, value);
        }

        private string _cpuName = "CPU";
        public string CpuName
        {
            get => _cpuName;
            set => SetProperty(ref _cpuName, value);
        }

        private bool _isDarkTheme = App.IsDarkTheme;
        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set
            {
                if (SetProperty(ref _isDarkTheme, value))
                {
                    if (App.IsDarkTheme != value)
                        App.ToggleTheme();
                }
            }
        }

        public long SessionFreedBytes
        {
            get => _sessionFreedBytes;
            set
            {
                _sessionFreedBytes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SessionFreedDisplay));
            }
        }

        public string SessionFreedDisplay => FormatHelper.FormatSize(_sessionFreedBytes);

        public ToastNotificationService Toast => _toast;
        public DashboardViewModel Dashboard { get; }
        public CleanupViewModel Cleanup { get; }
        public BrowserViewModel Browser { get; }
        public PrivacyViewModel Privacy { get; }
        public RecycleViewModel Recycle { get; }
        public RegistryViewModel Registry { get; }
        public StartupViewModel Startup { get; }
        public ServicesViewModel Services { get; }
        public EventLogsViewModel EventLogs { get; }
        public RestorePointsViewModel RestorePoints { get; }
        public DiskViewModel Disk { get; }
        public DuplicatesViewModel Duplicates { get; }
        public UninstallViewModel Uninstall { get; }
        public WinSxSViewModel WinSxS { get; }
        public RamViewModel Ram { get; }
        public NetworkViewModel Network { get; }
        public SystemToolsViewModel SystemTools { get; }
        public TelemetryViewModel Telemetry { get; }
        public SSDViewModel SSD { get; }
        public TaskSchedulerViewModel TaskScheduler { get; }
        public FontCacheViewModel FontCache { get; }
        public PowerViewModel Power { get; }

        private string _currentPanel = "Dashboard";
        public string CurrentPanel
        {
            get => _currentPanel;
            set
            {
                string previous = _currentPanel;
                if (SetProperty(ref _currentPanel, value))
                {
                    OnPanelChanged(previous, value);
                    CurrentViewModel = PanelToViewModel(value);
                }
            }
        }

        private object _currentViewModel;
        public object CurrentViewModel
        {
            get => _currentViewModel;
            private set => SetProperty(ref _currentViewModel, value);
        }

        public ICommand NavigateCommand { get; }
        public ICommand OneClickCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand ExportLogCommand { get; }

        public MainViewModel() : this(new AppServices()) { }

        public MainViewModel(AppServices services) : base(new ObservableCollection<string>())
        {
            _svc = services;
            var log = ActivityLog;

            Dashboard = new DashboardViewModel(log, _svc.Ram, _svc.DashboardHistory, _svc.Network, _svc.Recycle, this);
            Cleanup = new CleanupViewModel(log, _svc.Cleanup, this);
            Browser = new BrowserViewModel(log, _svc.Browser, this);
            Privacy = new PrivacyViewModel(log, _svc.Privacy, this);
            Recycle = new RecycleViewModel(log, _svc.Recycle, this);
            Registry = new RegistryViewModel(log, _svc.Registry, this);
            Startup = new StartupViewModel(log, _svc.Startup, this);
            Services = new ServicesViewModel(log, _svc.Services, this);
            EventLogs = new EventLogsViewModel(log, _svc.EventLogs, this);
            RestorePoints = new RestorePointsViewModel(log, _svc.RestorePoints, this);
            Disk = new DiskViewModel(log, _svc.Disk, this);
            Duplicates = new DuplicatesViewModel(log, _svc.Duplicates, this);
            Uninstall = new UninstallViewModel(log, _svc.Uninstall, this);
            WinSxS = new WinSxSViewModel(log, _svc.WinSxS, this);
            Ram = new RamViewModel(log, _svc.Ram, this);
            Network = new NetworkViewModel(log, _svc.Network, this);
            SystemTools = new SystemToolsViewModel(log, _svc.SystemTools, this);
            Telemetry = new TelemetryViewModel(log, _svc.Telemetry, this);
            SSD = new SSDViewModel(log, _svc.SSD, this);
            TaskScheduler = new TaskSchedulerViewModel(log, _svc.TaskScheduler, this);
            FontCache = new FontCacheViewModel(log, _svc.FontCache, this);
            Power = new PowerViewModel(Network);

            NavigateCommand = new RelayCommand(tag => CurrentPanel = tag?.ToString());
            ClearLogCommand = new RelayCommand(() => ActivityLog.Clear());
            ExportLogCommand = new RelayCommand(ExportLog, () => ActivityLog.Count > 0);
            OneClickCommand = new AsyncRelayCommand(OneClickAsync);

            _currentViewModel = this;
        }

        public void Initialize()
        {
            LoadSystemInfo();
            InitializeMonitor();
        }

        private void InitializeMonitor()
        {
            Task.Run(() =>
            {
                try
                {
                    _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    _cpuCounter.NextValue();
                }
                catch (Exception ex) { AppLogger.Log(ex, nameof(InitializeMonitor)); }
            });

            _monitorTimer.Interval = TimeSpan.FromSeconds(2);
            _monitorTimer.Tick += MonitorTimer_Tick;
            _monitorTimer.Start();
        }

        private void MonitorTimer_Tick(object sender, EventArgs e)
        {
            if (!_cpuReadPending)
            {
                _cpuReadPending = true;
                Task.Run(() =>
                {
                    try
                    {
                        float val;
                        lock (_cpuLock)
                            val = _cpuCounter?.NextValue() ?? 0;
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            CpuPercent = val;
                            _cpuReadPending = false;
                        });
                    }
                    catch { _cpuReadPending = false; }
                });
            }

            try
            {
                var (used, total, _, __) = _ramSvc.GetRamInfo();
                RamUsedGb = used;
                RamTotalGb = total;
                RamPercent = total > 0 ? used / total * 100 : 0;

                TimeSpan uptime = TimeSpan.FromSeconds(
                    (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);
                UptimeDisplay = $"{(int)uptime.TotalHours}h {uptime.Minutes}m";

                var cDrive = System.IO.DriveInfo.GetDrives()
                    .FirstOrDefault(d =>
                        d.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase) && d.IsReady);

                if (cDrive != null)
                {
                    DiskTotalGb = cDrive.TotalSize / 1_073_741_824.0;
                    DiskUsedGb = (cDrive.TotalSize - cDrive.AvailableFreeSpace) / 1_073_741_824.0;
                    DiskPercent = DiskTotalGb > 0 ? DiskUsedGb / DiskTotalGb * 100 : 0;

                    _histSvc.Record(used, total,
                        cDrive.AvailableFreeSpace / 1_073_741_824.0,
                        DiskTotalGb,
                        _sessionFreedBytes);
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(MonitorTimer_Tick)); }
        }

        private void LoadSystemInfo()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    string product = key?.GetValue("ProductName")?.ToString();
                    string build = key?.GetValue("CurrentBuildNumber")?.ToString();
                    string ubr = key?.GetValue("UBR")?.ToString();

                    if (!string.IsNullOrEmpty(product))
                    {
                        OsName = string.IsNullOrEmpty(build)
                            ? product
                            : $"{product} (Build {build}{(string.IsNullOrEmpty(ubr) ? "" : $".{ubr}")})";
                    }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(LoadSystemInfo)); }

            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                {
                    string cpu = key?.GetValue("ProcessorNameString")?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(cpu))
                        CpuName = cpu;
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(LoadSystemInfo)); }

            ComputerName = Environment.MachineName;
        }

        private object PanelToViewModel(string tag) => tag switch
        {
            "Dashboard" => (object)this,
            "Cleanup" => Cleanup,
            "Browser" => Browser,
            "Registry" => Registry,
            "Startup" => Startup,
            "Services" => Services,
            "Disk" => Disk,
            "RAM" => Ram,
            "Network" => Network,
            "SystemTools" => SystemTools,
            "Telemetry" => Telemetry,
            "SSD" => SSD,
            "Privacy" => Privacy,
            "Recycle" => Recycle,
            "EventLogs" => EventLogs,
            "RestorePoints" => RestorePoints,
            "Duplicates" => Duplicates,
            "Uninstall" => Uninstall,
            "WinSxS" => WinSxS,
            "TaskScheduler" => TaskScheduler,
            "FontCache" => FontCache,
            "Power" => Power,
            _ => this,
        };

        private async Task OneClickAsync()
        {
            if (!DialogService.Confirm(
                    "One-Click Optimization",
                    "Run One-Click Optimization?\n\n• Scan and clean junk files\n• Flush DNS\n• Optimize RAM\n• Empty Recycle Bin",
                    "Run", "Cancel")) return;

            ActivityLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] === One-Click Optimize started ===");

            long ramBefore = Ram.RamUsedBytes;
            var (binSize, _) = Recycle.GetBinInfo();

            await Cleanup.ScanAsync();
            long junkFreed = Cleanup.HasFolders
                ? await Cleanup.CleanAndGetFreedAsync() : 0;

            await (Network.FlushDnsCommand as AsyncRelayCommand)?.ExecuteAsync();
            await (Ram.OptimizeCommand as AsyncRelayCommand)?.ExecuteAsync();
            long ramFreed = Math.Max(0, ramBefore - Ram.RamUsedBytes);
            await (Recycle.EmptyCommand as AsyncRelayCommand)?.ExecuteAsync();

            ActivityLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] === One-Click Optimize complete ===");

            var sb = new System.Text.StringBuilder();
            if (junkFreed > 0) sb.AppendLine($"• Junk: {FormatHelper.FormatSize(junkFreed)} freed");
            else sb.AppendLine("• Junk: nothing found");
            sb.AppendLine("• DNS cache flushed");
            if (ramFreed > 0) sb.AppendLine($"• RAM: {FormatHelper.FormatSize(ramFreed)} freed");
            if (binSize > 0) sb.AppendLine($"• Recycle Bin: {FormatHelper.FormatSize(binSize)} cleared");

            _toast.ShowInfo("✅ One-Click Optimization complete", sb.ToString().TrimEnd());
        }

        private void OnPanelChanged(string previous, string next)
        {
            StatusMessage = $"Viewing: {next}";

            if (previous == "RAM") Ram.SetTimerActive(false);

            switch (next)
            {
                case "RAM":
                    Ram.SetTimerActive(true);
                    Ram.Refresh();
                    break;
                case "Telemetry" when Telemetry.Items.Count == 0:
                    _ = Telemetry.LoadAsync(); break;
                case "SSD" when SSD.Items.Count == 0:
                    _ = SSD.LoadAsync(); break;
                case "Services" when Services.Items.Count == 0:
                    _ = Services.LoadAsync(); break;
                case "RestorePoints":
                    _ = RestorePoints.LoadAsync(); break;
                case "Network":
                    _ = Network.LoadAdaptersAsync(); break;
                case "TaskScheduler" when TaskScheduler.Items.Count == 0:
                    _ = TaskScheduler.LoadAsync(); break;
            }
        }

        private void ExportLog()
        {
            if (ActivityLog.Count == 0) return;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Activity Log",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"WinOptimizerHub_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                DefaultExt = ".txt"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"WinOptimizerHub Activity Log — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine(new string('─', 60));
                foreach (var entry in ActivityLog)
                    sb.AppendLine(entry);

                System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), System.Text.Encoding.UTF8);
                _svc.Toast.ShowSuccess("Log Exported", $"Saved to: {System.IO.Path.GetFileName(dlg.FileName)}");
            }
            catch (Exception ex)
            {
                _svc.Toast.ShowError("Export Failed", ex.Message);
            }
        }

        public override void OnClose()
        {
            base.OnClose();
            _monitorTimer.Stop();
            _cpuCounter?.Dispose();
            _svc.Toast.Dispose();
        }

        public void UpdateFreedSpace(long bytes)
        {
            if (bytes > 0)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SessionFreedBytes += bytes;
                });
            }
        }
    }
}