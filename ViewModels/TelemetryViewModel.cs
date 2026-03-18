using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;
using WinOptimizerHub.Services;

namespace WinOptimizerHub.ViewModels
{
    public class TelemetryViewModel : BaseViewModel
    {
        private readonly TelemetryDisablerService _svc;
        private readonly MainViewModel _main;

        private ObservableCollection<TelemetryItem> _items
            = new ObservableCollection<TelemetryItem>();
        public ObservableCollection<TelemetryItem> Items
        {
            get => _items;
            private set
            {
                SetProperty(ref _items, value);
                OnPropertyChanged(nameof(SummaryText));
                OnPropertyChanged(nameof(HasItems));
                RebuildGroups();
            }
        }

        private ObservableCollection<TelemetryCategoryGroup> _groupedItems
            = new ObservableCollection<TelemetryCategoryGroup>();
        public ObservableCollection<TelemetryCategoryGroup> GroupedItems
        {
            get => _groupedItems;
            private set => SetProperty(ref _groupedItems, value);
        }

        public bool HasItems => _items.Any();
        public string SummaryText
        {
            get
            {
                int active = _items.Count(i => i.IsCurrentlyEnabled);
                int disabled = _items.Count(i => !i.IsCurrentlyEnabled);
                return _items.Any()
                    ? $"{active} active  ·  {disabled} already disabled  ·  {_items.Count} total"
                    : string.Empty;
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand ApplyAllCommand { get; }
        public ICommand RevertAllCommand { get; }
        public ICommand ToggleItemCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand SelectNoneCommand { get; }

        public TelemetryViewModel(ObservableCollection<string> log, TelemetryDisablerService svc, MainViewModel main) : base(log)
        {
            _svc = svc;
            _main = main;
            RefreshCommand = new AsyncRelayCommand(LoadAsync);
            ApplyAllCommand = new AsyncRelayCommand(ApplyAllAsync, () => !IsBusy);
            RevertAllCommand = new AsyncRelayCommand(RevertAllAsync, () => !IsBusy);
            ToggleItemCommand = new AsyncRelayCommand(ToggleItemAsync);
            SelectAllCommand = new RelayCommand(SelectAll);
            SelectNoneCommand = new RelayCommand(SelectNone);
        }

        public async Task LoadAsync()
        {
            SetBusy(true, "Checking telemetry status...");
            try
            {
                var list = await _svc.GetTelemetryStatusAsync();
                Items = new ObservableCollection<TelemetryItem>(list);
                int active = list.Count(i => i.IsCurrentlyEnabled);
                SetBusy(false, active > 0
                    ? $"{active} of {list.Count} telemetry features are active"
                    : $"All {list.Count} telemetry features are disabled");
                Log($"Telemetry scan: {active} active, {list.Count - active} disabled");
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(LoadAsync)); SetBusy(false, "Error"); }
        }

        private async Task ApplyAllAsync()
        {
            var toApply = _items.Where(i => i.IsSelected && i.IsCurrentlyEnabled).ToList();
            if (!toApply.Any())
            {
                _main.Toast.ShowInfo("Telemetry Disabler", "No active items selected to disable.");
                return;
            }

            if (!DialogService.ConfirmWarning(
                    "Confirm Disable All",
                    $"Disable {toApply.Count} selected telemetry features?\n\n"
                  + "This modifies registry keys and Windows services.\n"
                  + "A system restore point is recommended first.",
                    "Disable", "Cancel")) return;

            SetBusy(true, "Disabling telemetry...");
            var ct = ResetCts();
            try
            {
                var (applied, failed) = await _svc.ApplyAllAsync(toApply, MakeProgress(), ct);
                Log($"Telemetry: {applied} disabled, {failed} failed");
                _main.Toast.ShowInfo("Telemetry", $"{applied} features disabled");
                await LoadAsync();
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(ApplyAllAsync)); SetBusy(false, "Error"); }
        }

        private async Task RevertAllAsync()
        {
            if (!DialogService.Confirm(
                    "Confirm Restore Defaults",
                    "Restore all telemetry features to Windows defaults?\n\n"
                  + "This will re-enable data collection settings.",
                    "Restore", "Cancel")) return;

            SetBusy(true, "Restoring defaults...");
            var ct = ResetCts();
            try
            {
                var (applied, failed) = await _svc.RevertAllAsync(_items, MakeProgress(), ct);
                Log($"Telemetry: {applied} restored, {failed} failed");
                _main.Toast.ShowInfo("Telemetry", $"{applied} features restored to default");
                await LoadAsync();
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(RevertAllAsync)); SetBusy(false, "Error"); }
        }

        private async Task ToggleItemAsync(object parameter)
        {
            if (parameter is not TelemetryItem item) return;
            bool disable = item.IsCurrentlyEnabled;
            SetBusy(true, $"{(disable ? "Disabling" : "Enabling")} {item.Name}...");
            try
            {
                await _svc.ApplyItemAsync(item, disable);
                Log($"Telemetry '{item.Name}' {(disable ? "disabled" : "re-enabled")}");
                await LoadAsync();
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(ToggleItemAsync)); SetBusy(false, "Error"); }
        }

        public void SelectAll()
        {
            foreach (var i in _items) i.IsSelected = true;
            OnPropertyChanged(nameof(SummaryText));
        }

        public void SelectNone()
        {
            foreach (var i in _items) i.IsSelected = false;
            OnPropertyChanged(nameof(SummaryText));
        }

        private void RebuildGroups()
        {
            var col = new ObservableCollection<TelemetryCategoryGroup>();
            foreach (var grp in _items.GroupBy(i => i.Category))
            {
                int activeCount = grp.Count(i => i.IsCurrentlyEnabled);
                col.Add(new TelemetryCategoryGroup
                {
                    Category = grp.Key,
                    ActiveCount = activeCount,
                    Items = new ObservableCollection<TelemetryItem>(grp)
                });
            }
            GroupedItems = col;
        }
    }
}