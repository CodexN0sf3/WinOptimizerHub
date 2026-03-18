using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Services;

namespace WinOptimizerHub.ViewModels
{
    public class FontCacheViewModel : BaseViewModel
    {
        private readonly FontCacheCleanerService _svc;
        private readonly MainViewModel _main;

        private string _cacheSize = "0 B";
        public string CacheSize
        {
            get => _cacheSize;
            private set => SetProperty(ref _cacheSize, value);
        }

        private string _cacheFiles = "0 cache files";
        public string CacheFiles
        {
            get => _cacheFiles;
            private set => SetProperty(ref _cacheFiles, value);
        }

        public ICommand RebuildCommand { get; }

        public FontCacheViewModel(ObservableCollection<string> log, FontCacheCleanerService svc, MainViewModel main) : base(log)
        {
            _svc = svc;
            _main = main;
            RebuildCommand = new AsyncRelayCommand(RebuildAsync);
        }

        public void RefreshInfo()
        {
            try
            {
                var (size, files) = _svc.GetFontCacheInfo();
                CacheSize = FormatHelper.FormatSize(size);
                CacheFiles = $"{files} cache files";
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(RefreshInfo)); }
        }

        private async Task RebuildAsync()
        {
            SetBusy(true, "Rebuilding font cache...");
            try
            {
                bool ok = await _svc.RebuildFontCacheAsync(MakeProgress());
                RefreshInfo();
                Log("Font cache rebuilt");
                if (ok)
                    _main.Toast.ShowSuccess("Font Cache", "Font cache rebuilt successfully!");
                else
                    _main.Toast.ShowError("Font Cache", "Rebuild failed. Try running as Administrator.");
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(RebuildAsync)); }
            finally { SetBusy(false, "Font cache rebuilt"); }
        }
    }
}