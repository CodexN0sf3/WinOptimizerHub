using WinOptimizerHub.Services;

namespace WinOptimizerHub
{
    public sealed class AppServices
    {
        public RAMOptimizerService Ram { get; } = new RAMOptimizerService();
        public BrowserCacheService Browser { get; } = new BrowserCacheService();
        public DashboardHistoryService DashboardHistory { get; } = new DashboardHistoryService();
        public ToastNotificationService Toast { get; } = new ToastNotificationService();
        public NetworkOptimizerService Network { get; } = new NetworkOptimizerService();
        public RecycleBinCleanerService Recycle { get; } = new RecycleBinCleanerService();

        public JunkCleanerService Cleanup { get; } = new JunkCleanerService();
        public PrivacyCleanerService Privacy { get; } = new PrivacyCleanerService();
        public RegistryCleanerService Registry { get; } = new RegistryCleanerService();
        public StartupManagerService Startup { get; } = new StartupManagerService();
        public ServiceOptimizerService Services { get; } = new ServiceOptimizerService();
        public EventLogCleanerService EventLogs { get; } = new EventLogCleanerService();
        public SystemRestoreService RestorePoints { get; } = new SystemRestoreService();
        public DiskAnalyzerService Disk { get; } = new DiskAnalyzerService();
        public DuplicateFinderService Duplicates { get; } = new DuplicateFinderService();
        public UninstallManagerService Uninstall { get; } = new UninstallManagerService();
        public WindowsUpdateCleanupService WinSxS { get; } = new WindowsUpdateCleanupService();
        public SystemToolsService SystemTools { get; } = new SystemToolsService();
        public TelemetryDisablerService Telemetry { get; } = new TelemetryDisablerService();
        public SSDTweakerService SSD { get; } = new SSDTweakerService();
        public TaskSchedulerService TaskScheduler { get; } = new TaskSchedulerService();
        public FontCacheCleanerService FontCache { get; } = new FontCacheCleanerService();

    }
}