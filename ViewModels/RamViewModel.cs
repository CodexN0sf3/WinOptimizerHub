using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;
using WinOptimizerHub.Services;

namespace WinOptimizerHub.ViewModels
{
    public class RamViewModel : BaseViewModel
    {
        private readonly RAMOptimizerService _svc;
        private readonly MainViewModel _main;
        private readonly DispatcherTimer _timer;

        public long RamUsedBytes => (long)(_ramUsedGb * 1_073_741_824.0);

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

        private string _ramDetail = "--";
        public string RamDetail
        {
            get => _ramDetail;
            private set => SetProperty(ref _ramDetail, value);
        }

        private string _ramFreeText = "";
        public string RamFreeText
        {
            get => _ramFreeText;
            private set => SetProperty(ref _ramFreeText, value);
        }

        public ObservableCollection<RamSample> History { get; }
            = new ObservableCollection<RamSample>();

        private ObservableCollection<ProcessMemoryInfo> _processes
            = new ObservableCollection<ProcessMemoryInfo>();
        public ObservableCollection<ProcessMemoryInfo> Processes
        {
            get => _processes;
            private set => SetProperty(ref _processes, value);
        }

        public ICommand OptimizeCommand { get; }
        public ICommand OptimizeSelectedCommand { get; }
        public ICommand RefreshProcessesCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand SelectNoneCommand { get; }

        public RamViewModel(ObservableCollection<string> log,
                            RAMOptimizerService svc, MainViewModel main)
            : base(log)
        {
            _svc = svc;
            _main = main;

            OptimizeCommand = new AsyncRelayCommand(OptimizeAllAsync, () => !IsBusy);
            OptimizeSelectedCommand = new AsyncRelayCommand(OptimizeSelectedAsync, () => !IsBusy);
            RefreshProcessesCommand = new AsyncRelayCommand(RefreshProcessesAsync);
            SelectAllCommand = new RelayCommand(() => SetAllSelected(true));
            SelectNoneCommand = new RelayCommand(() => SetAllSelected(false));

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += async (_, __) => await RefreshStatsAsync();
        }

        public void SetTimerActive(bool active)
        {
            if (active && !_timer.IsEnabled)
                _timer.Start();
            else if (!active && _timer.IsEnabled)
                _timer.Stop();
        }

        public void Refresh()
        {
            _ = RefreshStatsAsync();
            _ = RefreshProcessesAsync();
        }

        private async Task RefreshStatsAsync()
        {
            try
            {
                var (used, total, free, pct) = await Task.Run(() => _svc.GetRamInfo());

                RamUsedGb = used;
                RamTotalGb = total;
                RamPercent = pct;
                RamDetail = $"{used:F1} / {total:F1} GB  ({pct:F0}% used)";
                RamFreeText = $"{free:F1} GB free";

                if (History.Count >= 60) History.RemoveAt(0);
                History.Add(new RamSample { Percent = pct, Time = DateTime.Now });
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(RefreshStatsAsync)); }
        }

        private async Task RefreshProcessesAsync()
        {
            try
            {
                var list = await Task.Run(() => _svc.GetTopMemoryProcesses(20));
                Processes = new ObservableCollection<ProcessMemoryInfo>(list);
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(RefreshProcessesAsync)); }
        }

        private void SetAllSelected(bool value)
        {
            foreach (var p in _processes) p.IsSelected = value;
        }

        private async Task OptimizeAllAsync()
        {
            SetBusy(true, "Optimizing RAM...");
            try
            {
                long freed = await _svc.OptimizeAsync(null, MakeProgress());
                await RefreshStatsAsync();
                await RefreshProcessesAsync();
                Log($"RAM optimized: ~{freed} MB freed");
                _main.UpdateFreedSpace(freed * 1024 * 1024);

                if (freed > 0)
                {
                    SetBusy(false, $"~{freed} MB freed");
                    _main.Toast.ShowSuccess("RAM Optimizer", $"~{freed} MB freed successfully");
                }
                else
                {
                    SetBusy(false, "RAM already optimal");
                    _main.Toast.ShowInfo("RAM Optimizer", "RAM is already at optimal usage");
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(OptimizeAllAsync)); SetBusy(false, "Error"); }
        }

        private async Task OptimizeSelectedAsync()
        {
            var selected = _processes.Where(p => p.IsSelected).ToList();
            if (!selected.Any())
            {
                _main.Toast.ShowWarning("RAM Optimizer", "No processes selected.");
                return;
            }

            SetBusy(true, $"Optimizing {selected.Count} selected processes...");
            try
            {
                long freed = await _svc.OptimizeAsync(selected, MakeProgress());
                await RefreshStatsAsync();
                await RefreshProcessesAsync();
                Log($"RAM optimized (selected): ~{freed} MB freed");

                if (freed > 0)
                {
                    SetBusy(false, $"~{freed} MB freed");
                    _main.Toast.ShowSuccess("RAM Optimizer", $"~{freed} MB freed from {selected.Count} processes");
                }
                else
                {
                    SetBusy(false, "Optimization complete");
                    _main.Toast.ShowInfo("RAM Optimizer", "Selected processes already at minimal memory usage");
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(OptimizeSelectedAsync)); SetBusy(false, "Error"); }
        }

        public void StopTimer() => _timer?.Stop();
    }
}