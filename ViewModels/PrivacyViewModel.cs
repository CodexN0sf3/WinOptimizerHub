using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;
using WinOptimizerHub.Services;

namespace WinOptimizerHub.ViewModels
{
    public class PrivacyViewModel : BaseViewModel
    {
        private readonly PrivacyCleanerService _svc;
        private readonly MainViewModel _main;

        private List<PrivacyCleanerService.PrivacyItem> _items = new List<PrivacyCleanerService.PrivacyItem>();
        public List<PrivacyCleanerService.PrivacyItem> Items
        {
            get => _items;
            private set
            {
                SetProperty(ref _items, value);
                OnPropertyChanged(nameof(HasItems));
                OnPropertyChanged(nameof(SummaryText));
                RebuildGroups();
            }
        }

        private ObservableCollection<PrivacyCategoryGroup> _groupedItems = new ObservableCollection<PrivacyCategoryGroup>();
        public ObservableCollection<PrivacyCategoryGroup> GroupedItems
        {
            get => _groupedItems;
            private set => SetProperty(ref _groupedItems, value);
        }

        public bool HasItems => _items.Any();

        public string SummaryText
        {
            get
            {
                int selected = _items.Count(i => i.IsSelected);
                long total = _items.Where(i => i.IsSelected).Sum(i => i.EstimatedSize);
                return selected == 0
                    ? "No items selected"
                    : $"{selected} items selected — {Converters.FileSizeConverter.FormatSize(total)} to clean";
            }
        }

        public ICommand ScanCommand { get; }
        public ICommand CleanCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand SelectNoneCommand { get; }

        public PrivacyViewModel(ObservableCollection<string> log, PrivacyCleanerService svc, MainViewModel main) : base(log)
        {
            _svc = svc;
            _main = main;
            ScanCommand = new AsyncRelayCommand(ScanAsync);
            CleanCommand = new AsyncRelayCommand(CleanAsync, () => HasItems);
            SelectAllCommand = new RelayCommand(SelectAll);
            SelectNoneCommand = new RelayCommand(SelectNone);
        }

        private async Task ScanAsync()
        {
            SetBusy(true, "Scanning privacy traces...");
            try
            {
                Items = await _svc.ScanAsync();
                SetBusy(false, $"Found {Items.Count} privacy items");
                Log($"Privacy: {Items.Count} items found");
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(ScanAsync)); SetBusy(false, "Error scanning"); }
        }

        private async Task CleanAsync()
        {
            SetBusy(true, "Cleaning privacy traces...");
            try
            {
                int cleaned = await _svc.CleanAsync(Items, MakeProgress());
                Log($"Privacy: {cleaned} items cleaned");
                SetBusy(false, $"Cleaned {cleaned} items successfully");
                _main.Toast.ShowSuccess("Privacy Cleaner", $"{cleaned} items cleaned successfully");
                await ScanAsync();
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(CleanAsync)); SetBusy(false, "Error cleaning"); }
        }

        public void SelectAll()
        {
            foreach (var item in _items) item.IsSelected = true;
            OnPropertyChanged(nameof(SummaryText));
        }

        public void SelectNone()
        {
            foreach (var item in _items) item.IsSelected = false;
            OnPropertyChanged(nameof(SummaryText));
        }

        private void RebuildGroups()
        {
            var col = new ObservableCollection<PrivacyCategoryGroup>();
            foreach (var grp in _items.GroupBy(i => i.Category))
            {
                col.Add(new PrivacyCategoryGroup
                {
                    Category = grp.Key,
                    Items = new ObservableCollection<PrivacyCleanerService.PrivacyItem>(grp)
                });
            }
            GroupedItems = col;
        }
    }
}