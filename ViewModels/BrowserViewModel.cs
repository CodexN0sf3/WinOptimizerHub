using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Services;

namespace WinOptimizerHub.ViewModels
{
    public class BrowserViewModel : BaseViewModel
    {
        private readonly BrowserCacheService _svc;
        private readonly MainViewModel _main;

        public bool CleanHistory { get; set; } = false;
        public bool CleanCookies { get; set; } = false;

        private bool _isScanning;
        public bool IsScanning
        {
            get => _isScanning;
            private set => SetProperty(ref _isScanning, value);
        }

        private ObservableCollection<BrowserInfo> _browsers = new ObservableCollection<BrowserInfo>();
        public ObservableCollection<BrowserInfo> Browsers
        {
            get => _browsers;
            private set
            {
                SetProperty(ref _browsers, value);
                OnPropertyChanged(nameof(HasBrowsers));
            }
        }

        public bool HasBrowsers => _browsers.Count > 0;

        public ICommand ScanCommand { get; }
        public ICommand CleanCommand { get; }

        public BrowserViewModel(ObservableCollection<string> log,
                                BrowserCacheService svc) : base(log)
        {
            _svc = svc;
            _main = null;

            ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsScanning);
            CleanCommand = new AsyncRelayCommand(CleanAsync, () => Browsers.Any(b => b.IsSelected) && !IsScanning);
        }

        public BrowserViewModel(ObservableCollection<string> log,
                                BrowserCacheService svc,
                                MainViewModel main) : this(log, svc)
        {
            _main = main;
        }

        public async Task ScanAsync()
        {
            IsScanning = true;
            ReportStatus("Searching for browser caches...");
            try
            {
                var results = await Task.Run(() => _svc.ScanBrowsers(forceRefresh: true));
                Browsers = new ObservableCollection<BrowserInfo>(results);

                ReportStatus(Browsers.Count == 0
                    ? "No browser cache found."
                    : $"Found cache in {Browsers.Count} location(s).");
            }
            catch (Exception ex) { HandleError(ex, nameof(ScanAsync)); }
            finally { IsScanning = false; }
        }

        public async Task CleanAsync()
        {
            IsScanning = true;
            try
            {
                var selected = Browsers.Where(b => b.IsSelected).ToList();
                long freed = await Task.Run(() => _svc.CleanBrowserCache(selected));

                _main?.UpdateFreedSpace(freed);

                Log($"Browser cache: {FormatHelper.FormatSize(freed)} freed");
                _main?.Toast.ShowCleanComplete("Browser Cache", freed);

                await ScanAsync();
            }
            catch (Exception ex) { HandleError(ex, nameof(CleanAsync)); }
            finally { IsScanning = false; }
        }
    }
}