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
    public class SSDViewModel : BaseViewModel
    {
        private readonly SSDTweakerService _svc;
        private readonly MainViewModel _main;

        private ObservableCollection<SSDTweak> _items
            = new ObservableCollection<SSDTweak>();
        public ObservableCollection<SSDTweak> Items
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

        private ObservableCollection<SSDCategoryGroup> _groupedItems
            = new ObservableCollection<SSDCategoryGroup>();
        public ObservableCollection<SSDCategoryGroup> GroupedItems
        {
            get => _groupedItems;
            private set => SetProperty(ref _groupedItems, value);
        }

        public bool HasItems => _items.Any();
        public string SummaryText
        {
            get
            {
                int optimal = _items.Count(i => i.IsCurrentlyOptimal);
                int notOptimal = _items.Count(i => !i.IsCurrentlyOptimal);
                return _items.Any()
                    ? $"{optimal} optimal  ·  {notOptimal} need attention  ·  {_items.Count} total"
                    : string.Empty;
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand ApplyAllCommand { get; }
        public ICommand RevertAllCommand { get; }
        public ICommand ApplyOneCommand { get; }

        public SSDViewModel(ObservableCollection<string> log, SSDTweakerService svc, MainViewModel main) : base(log)
        {
            _svc = svc;
            _main = main;
            RefreshCommand = new AsyncRelayCommand(LoadAsync);
            ApplyAllCommand = new AsyncRelayCommand(ApplyAllAsync, () => !IsBusy);
            RevertAllCommand = new AsyncRelayCommand(RevertAllAsync, () => !IsBusy);
            ApplyOneCommand = new AsyncRelayCommand(ApplyOneAsync);
        }

        public async Task LoadAsync()
        {
            SetBusy(true, "Checking SSD settings...");
            try
            {
                var list = await _svc.GetSSDStatusAsync();
                Items = new ObservableCollection<SSDTweak>(list);
                int notOpt = list.Count(i => !i.IsCurrentlyOptimal);
                SetBusy(false, notOpt > 0
                    ? $"{notOpt} settings can be optimized"
                    : "All settings are optimal!");
                Log($"SSD: {list.Count(i => i.IsCurrentlyOptimal)}/{list.Count} optimal");
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(LoadAsync)); SetBusy(false, "Error"); }
        }

        private async Task ApplyAllAsync()
        {
            var toApply = _items.Where(i => i.IsSelected && !i.IsCurrentlyOptimal).ToList();
            if (!toApply.Any())
            {
                _main.Toast.ShowInfo("SSD Tweaker", "All selected tweaks are already optimal.");
                return;
            }

            if (!DialogService.ConfirmWarning(
                    "Confirm Apply",
                    $"Apply {toApply.Count} SSD tweaks?\n\nThis modifies registry settings and services.\nA restart may be required for some changes.",
                    "Apply", "Cancel")) return;

            SetBusy(true, "Applying SSD tweaks...");
            var ct = ResetCts();
            try
            {
                var (applied, failed) = await _svc.ApplyTweaksAsync(toApply, MakeProgress(), ct);
                Log($"SSD tweaks: {applied} applied, {failed} failed");
                _main.Toast.ShowInfo("SSD Tweaker", $"{applied} tweaks applied successfully");
                await LoadAsync();
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(ApplyAllAsync)); SetBusy(false, "Error"); }
        }

        private async Task RevertAllAsync()
        {
            if (!DialogService.ConfirmWarning(
                    "Confirm Revert",
                    "Revert all SSD tweaks to Windows defaults?\n\nThis will undo all optimizations.",
                    "Revert", "Cancel")) return;

            SetBusy(true, "Reverting SSD tweaks...");
            var ct = ResetCts();
            try
            {
                var (reverted, failed) = await _svc.RevertTweaksAsync(_items, MakeProgress(), ct);
                Log($"SSD tweaks reverted: {reverted}, failed: {failed}");
                _main.Toast.ShowInfo("SSD Tweaker", $"{reverted} tweaks restored to defaults");
                await LoadAsync();
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(RevertAllAsync)); SetBusy(false, "Error"); }
        }

        private async Task ApplyOneAsync(object parameter)
        {
            if (parameter is not SSDTweak tweak) return;
            SetBusy(true, $"Applying: {tweak.Name}...");
            try
            {
                await _svc.ApplyTweaksAsync(new[] { tweak }, null);
                Log($"SSD tweak applied: {tweak.Name}");
                await LoadAsync();
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(ApplyOneAsync)); SetBusy(false, "Error"); }
        }

        public void SelectAll() { foreach (var i in _items) i.IsSelected = true; }
        public void SelectNone() { foreach (var i in _items) i.IsSelected = false; }

        private void RebuildGroups()
        {
            var col = new ObservableCollection<SSDCategoryGroup>();
            foreach (var grp in _items.GroupBy(i => i.Category))
            {
                int notOpt = grp.Count(i => !i.IsCurrentlyOptimal);
                col.Add(new SSDCategoryGroup
                {
                    Category = grp.Key,
                    NeedCount = notOpt,
                    Items = new ObservableCollection<SSDTweak>(grp)
                });
            }
            GroupedItems = col;
        }
    }
}