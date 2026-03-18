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
    public class StartupViewModel : BaseViewModel
    {
        private readonly StartupManagerService _svc;
        private readonly MainViewModel _main;

        private ObservableCollection<StartupItem> _items = new ObservableCollection<StartupItem>();
        public ObservableCollection<StartupItem> Items
        {
            get => _items;
            private set { SetProperty(ref _items, value); OnPropertyChanged(nameof(ItemCount)); RebuildGroups(); }
        }

        private ObservableCollection<StartupGroup> _groupedItems = new ObservableCollection<StartupGroup>();
        public ObservableCollection<StartupGroup> GroupedItems
        {
            get => _groupedItems;
            private set => SetProperty(ref _groupedItems, value);
        }

        public string ItemCount => $"{_items.Count} startup items found";

        private StartupItem _selectedItem;
        public StartupItem SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand ToggleCommand { get; }
        public ICommand EnableCommand { get; }
        public ICommand DisableCommand { get; }
        public ICommand CopyPathCommand { get; }
        public ICommand DeleteCommand { get; }

        public StartupViewModel(ObservableCollection<string> log, StartupManagerService svc, MainViewModel main) : base(log)
        {
            _svc = svc;
            _main = main;
            RefreshCommand = new AsyncRelayCommand(LoadAsync);
            ToggleCommand = new AsyncRelayCommand(ToggleItemAsync);
            EnableCommand = new AsyncRelayCommand(EnableItemAsync, () => SelectedItem != null);
            DisableCommand = new AsyncRelayCommand(DisableItemAsync, () => SelectedItem != null);
            CopyPathCommand = new RelayCommand(CopyPath, () => SelectedItem != null);
            DeleteCommand = new AsyncRelayCommand(DeleteItemAsync, () => SelectedItem != null);
        }

        public void SetStatus(string message) => StatusMessage = message;

        public async Task LoadAsync()
        {
            SetBusy(true, "Loading startup items...");
            try
            {
                var (items, _) = await _svc.GetStartupItemsDiagnosticAsync();
                Items = new ObservableCollection<StartupItem>(items);
                SetBusy(false, $"{items.Count} startup items — refreshed at {DateTime.Now:HH:mm}");
                Log($"Startup: {items.Count} items loaded");

                _ = LoadIconsAsync(items);
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(LoadAsync)); SetBusy(false, "Error loading startup items"); }
        }

        private async Task LoadIconsAsync(System.Collections.Generic.List<StartupItem> items)
        {
            const int batchSize = 20;
            for (int i = 0; i < items.Count; i += batchSize)
            {
                var batch = items.Skip(i).Take(batchSize).ToList();

                var icons = await System.Threading.Tasks.Task.Run(() =>
                {
                    var result = new System.Collections.Generic.List<(StartupItem item, System.Windows.Media.ImageSource icon)>();
                    foreach (var item in batch)
                    {
                        var icon = StartupIconHelper.GetIcon(item.Command);
                        result.Add((item, icon));
                    }
                    return result;
                });

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var (item, icon) in icons)
                        item.IconSource = icon;
                });

                await Task.Delay(10);
            }
        }

        private async Task ToggleItemAsync(object parameter)
        {
            if (parameter is not StartupItem item) return;

            bool desiredState = item.IsEnabled;
            var (ok, error) = await _svc.SetStartupItemEnabledAsync(item, desiredState);

            if (!ok)
            {
                item.IsEnabled = !desiredState;
                _main.Toast.ShowError("Startup Manager", $"Could not {(desiredState ? "enable" : "disable")} '{item.Name}'. {error}");
            }
            else
            {
                Log($"Startup '{item.Name}' {(desiredState ? "enabled" : "disabled")}");
                StatusMessage = $"'{item.Name}' {(desiredState ? "enabled" : "disabled")}";
            }
        }

        private async Task EnableItemAsync()
        {
            if (SelectedItem == null) return;
            if (SelectedItem.IsEnabled) { StatusMessage = $"'{SelectedItem.Name}' is already enabled"; return; }
            await SetEnabledAsync(SelectedItem, true);
        }

        private async Task DisableItemAsync()
        {
            if (SelectedItem == null) return;
            if (!SelectedItem.IsEnabled) { StatusMessage = $"'{SelectedItem.Name}' is already disabled"; return; }
            await SetEnabledAsync(SelectedItem, false);
        }

        private async Task SetEnabledAsync(StartupItem item, bool enable)
        {
            var (ok, error) = await _svc.SetStartupItemEnabledAsync(item, enable);
            if (!ok)
                _main.Toast.ShowError("Startup Manager", $"Could not {(enable ? "enable" : "disable")} '{item.Name}'. {error}");
            else
            {
                item.IsEnabled = enable;
                Log($"Startup '{item.Name}' {(enable ? "enabled" : "disabled")}");
                StatusMessage = $"'{item.Name}' {(enable ? "enabled" : "disabled")}";
            }
        }

        private void CopyPath()
        {
            if (SelectedItem == null) return;
            try
            {
                string path = SelectedItem.Command?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(path))
                    System.Windows.Clipboard.SetText(path);
                StatusMessage = "Command path copied to clipboard";
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(CopyPath)); }
        }

        private async Task DeleteItemAsync()
        {
            if (SelectedItem == null) return;

            if (!DialogService.ConfirmWarning(
                    "Confirm Remove",
                    $"Remove '{SelectedItem.Name}' from startup?\n\n"
                  + "This removes the startup entry only — the program itself is not deleted.",
                    "Remove", "Cancel")) return;

            var (ok, error) = await _svc.DeleteStartupItemAsync(SelectedItem);
            if (!ok)
                _main.Toast.ShowError("Startup Manager", $"Could not remove '{SelectedItem.Name}' from startup. {error}");
            else
            {
                Log($"Startup entry deleted: '{SelectedItem.Name}'");
                await LoadAsync();
            }
        }

        private void RebuildGroups()
        {
            var col = new ObservableCollection<StartupGroup>();
            foreach (var catGroup in _items.GroupBy(i => i.Category).OrderBy(g => g.Key))
            {
                var sg = new StartupGroup { Category = catGroup.Key };
                foreach (var keyGroup in catGroup.GroupBy(i => i.RegistryKeyPath).OrderBy(g => g.Key))
                {
                    var kg = new StartupKeyGroup { KeyPath = keyGroup.Key };
                    foreach (var item in keyGroup)
                        kg.Items.Add(item);
                    sg.KeyGroups.Add(kg);
                }
                col.Add(sg);
            }
            GroupedItems = col;
        }
    }
}