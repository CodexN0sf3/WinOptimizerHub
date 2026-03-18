using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Services;

namespace WinOptimizerHub.ViewModels
{
    public class RecycleViewModel : BaseViewModel
    {
        private readonly RecycleBinCleanerService _svc;
        private readonly MainViewModel _main;
        private readonly DispatcherTimer _autoRefresh;

        private string _binSize = "0 B";
        public string BinSize
        {
            get => _binSize;
            private set => SetProperty(ref _binSize, value);
        }

        private string _binCount = "0 items";
        public string BinCount
        {
            get => _binCount;
            private set => SetProperty(ref _binCount, value);
        }

        public ICommand EmptyCommand { get; }
        public ICommand RefreshCommand { get; }

        public RecycleViewModel(ObservableCollection<string> log, RecycleBinCleanerService svc, MainViewModel main) : base(log)
        {
            _svc = svc;
            _main = main;
            EmptyCommand = new AsyncRelayCommand(EmptyAsync);
            RefreshCommand = new RelayCommand(RefreshInfo);

            _autoRefresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _autoRefresh.Tick += (_, __) => RefreshInfo();
            _autoRefresh.Start();
        }

        public override void OnClose()
        {
            _autoRefresh.Stop();
            base.OnClose();
        }

        public (long size, int count) GetBinInfo()
        {
            try { return ((long size, int count))_svc.GetRecycleBinInfo(); }
            catch { return (0, 0); }
        }

        public void RefreshInfo()
        {
            try
            {
                var (size, count) = _svc.GetRecycleBinInfo();
                BinSize = FormatHelper.FormatSize(size);
                BinCount = $"{count} items";
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(RefreshInfo)); }
        }

        private async Task EmptyAsync()
        {
            var (sizeBefore, countBefore) = _svc.GetRecycleBinInfo();
            if (countBefore == 0)
            {
                StatusMessage = "Recycle Bin is already empty";
                return;
            }

            if (!DialogService.ConfirmDanger(
                    "Empty Recycle Bin",
                    $"Empty the Recycle Bin?\n\n{countBefore} items — {FormatHelper.FormatSize(sizeBefore)}",
                    "Empty", "Cancel")) return;

            SetBusy(true, "Emptying Recycle Bin...");
            try
            {
                bool ok = await _svc.EmptyRecycleBinAsync(MakeProgress());
                RefreshInfo();
                if (ok)
                {
                    Log($"Recycle Bin emptied: {FormatHelper.FormatSize(sizeBefore)} freed");
                    _main.Toast.ShowCleanComplete("Recycle Bin", sizeBefore);
                    SetBusy(false, $"Recycle Bin emptied — {FormatHelper.FormatSize(sizeBefore)} freed");
                }
                else
                {
                    _main.Toast.ShowError("Recycle Bin", "Failed to empty — try running as Administrator");
                    SetBusy(false, "Failed to empty Recycle Bin");
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(EmptyAsync)); SetBusy(false, "Error"); }
        }
    }
}