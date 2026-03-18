using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Services;

namespace WinOptimizerHub.ViewModels
{
    public class DashboardViewModel : BaseViewModel
    {
        private readonly RAMOptimizerService _ramSvc;
        private readonly DashboardHistoryService _histSvc;
        private readonly NetworkOptimizerService _networkSvc;
        private readonly RecycleBinCleanerService _recycleSvc;
        private readonly MainViewModel _main;

        public ICommand QuickCleanCommand { get; }
        public ICommand QuickFlushDnsCommand { get; }
        public ICommand QuickRamOptimizeCommand { get; }
        public ICommand QuickEmptyRecycleCommand { get; }
        public ICommand QuickSfcCommand { get; }

        public DashboardViewModel(
            ObservableCollection<string> log,
            RAMOptimizerService ramSvc,
            DashboardHistoryService histSvc,
            NetworkOptimizerService networkSvc,
            RecycleBinCleanerService recycleSvc,
            MainViewModel main)
            : base(log)
        {
            _ramSvc = ramSvc;
            _histSvc = histSvc;
            _networkSvc = networkSvc;
            _recycleSvc = recycleSvc;
            _main = main;

            QuickCleanCommand = new AsyncRelayCommand(QuickCleanAsync);
            QuickFlushDnsCommand = new AsyncRelayCommand(QuickFlushDnsAsync);
            QuickRamOptimizeCommand = new AsyncRelayCommand(QuickRamOptimizeAsync);
            QuickEmptyRecycleCommand = new AsyncRelayCommand(QuickEmptyRecycleAsync);
            QuickSfcCommand = new RelayCommand(QuickSfc);
        }

        private async Task QuickCleanAsync()
        {
            _main.CurrentPanel = "Cleanup";
            await Task.Delay(200);
            await _main.Cleanup.ScanAsync();
        }

        private async Task QuickFlushDnsAsync()
        {
            SetBusy(true, "Flushing DNS...");
            try
            {
                string result = await _networkSvc.FlushDnsAsync(MakeProgress());
                Log("DNS flushed: " + (result.Split('\n').Length > 0 ? result.Split('\n')[0].Trim() : "OK"));
            }
            finally { SetBusy(false, "DNS flushed"); }
        }

        private async Task QuickRamOptimizeAsync()
        {
            SetBusy(true, "Optimizing RAM...");
            try
            {
                long freed = await _ramSvc.OptimizeAsync(null, MakeProgress());
                Log($"RAM: ~{freed} MB freed");
                SetBusy(false, $"RAM: {freed} MB freed");
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(QuickRamOptimizeAsync)); SetBusy(false, "Error"); }
        }

        private async Task QuickEmptyRecycleAsync()
        {
            var (size, count) = _recycleSvc.GetRecycleBinInfo();
            if (count == 0) { StatusMessage = "Recycle Bin is already empty"; return; }
            await _recycleSvc.EmptyRecycleBinAsync(MakeProgress());
            Log($"Recycle Bin emptied: {FormatHelper.FormatSize(size)}");
        }

        private void QuickSfc()
        {
            _main.CurrentPanel = "SystemTools";
            _ = _main.SystemTools.RunSfcAsync();
        }
    }
}