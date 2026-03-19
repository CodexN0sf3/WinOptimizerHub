using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;
using WinOptimizerHub.ViewModels;

namespace WinOptimizerHub.Views
{
    public partial class StartupView : UserControl
    {
        private StartupViewModel VM => DataContext as StartupViewModel;

        // Prevents SelectionChanged from triggering ScrollIntoView during restore
        private bool _suppressSelectionScroll;

        public StartupView()
        {
            InitializeComponent();

            // Auto-load or apply grouping when the tab is first opened.
            // MainWindow may have already called LoadAsync() — if Items are populated,
            // just apply grouping. If still loading or not yet started, hook into Loaded.
            Loaded += OnViewLoaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            // Loaded fires once when the control enters the visual tree (first tab visit).
            // By this time DataContext is already set. Trigger load/grouping from here
            // so the ListView is guaranteed to be in the visual tree.
            if (VM == null) return;

            if (VM.Items.Count > 0)
                ApplyGrouping();
            else if (!VM.IsBusy)
                // Not loaded yet and not currently loading — start now
                _ = VM.LoadAsync().ContinueWith(_ => Dispatcher.Invoke(ApplyGrouping));
            else
                // Currently loading (MainWindow triggered it) — wait for it to finish
                VM.PropertyChanged += OnVmPropertyChanged;
        }

        private void OnVmPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VM.IsBusy) && !VM.IsBusy && VM.Items.Count > 0)
            {
                VM.PropertyChanged -= OnVmPropertyChanged;
                Dispatcher.Invoke(ApplyGrouping);
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // DataContext changed — if already loaded, apply grouping.
            // The Loaded event handles the initial load trigger.
            if (VM != null && VM.Items.Count > 0)
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)ApplyGrouping);
        }

        public void ApplyGrouping()
        {
            if (VM == null) return;
            var lcv = new ListCollectionView(VM.Items);
            lcv.GroupDescriptions.Add(new PropertyGroupDescription(nameof(StartupItem.Category)));
            lcv.SortDescriptions.Add(new SortDescription(nameof(StartupItem.CategoryOrder), ListSortDirection.Ascending));
            lcv.SortDescriptions.Add(new SortDescription(nameof(StartupItem.RegistryKeyOrder), ListSortDirection.Ascending));
            lcv.SortDescriptions.Add(new SortDescription(nameof(StartupItem.RegistryKeyPath), ListSortDirection.Ascending));
            lcv.SortDescriptions.Add(new SortDescription(nameof(StartupItem.Name), ListSortDirection.Ascending));
            StartupList.ItemsSource = lcv;
        }

        private void RefreshStartup_Click(object sender, RoutedEventArgs e)
            => _ = VM?.LoadAsync().ContinueWith(_ => Dispatcher.Invoke(ApplyGrouping));

        /// <summary>
        /// Reloads after toggle/delete preserving scroll position.
        ///
        /// Key insight: ApplyGrouping() replaces ItemsSource which always resets the
        /// ScrollViewer. We must restore the offset AFTER WPF finishes its full layout
        /// pass — DispatcherPriority.Background runs after Render, ensuring the new
        /// items are measured and arranged before we touch VerticalOffset.
        /// We also avoid setting StartupList.SelectedItem (which triggers ScrollIntoView)
        /// and instead only update VM.SelectedItem via the silent path.
        /// </summary>
        // Pending scroll offset to restore after next ItemsSource change
        private double _pendingScrollOffset = -1;

        private async Task RefreshAfterOperationAsync()
        {
            if (VM == null) return;

            string selectedKey = VM.SelectedItem?.RegistryKey;
            double scrollOffset = await Dispatcher.InvokeAsync(GetScrollOffset);

            await VM.LoadAsync();

            await Dispatcher.InvokeAsync(() =>
            {
                // Arm the pending scroll restore BEFORE ApplyGrouping replaces ItemsSource.
                // ScrollChanged fires once after ItemsSource is set and items are laid out.
                _pendingScrollOffset = scrollOffset;

                ApplyGrouping();

                // Restore selection silently
                if (!string.IsNullOrEmpty(selectedKey) && StartupList.ItemsSource is ListCollectionView lcv)
                {
                    foreach (var obj in lcv)
                    {
                        if (obj is StartupItem si && si.RegistryKey == selectedKey)
                        {
                            _suppressSelectionScroll = true;
                            StartupList.SelectedItem = si;
                            _suppressSelectionScroll = false;
                            break;
                        }
                    }
                }
            });
        }

        private void StartupList_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Fired after ItemsSource changes and WPF has finished layout.
            // Restore our saved offset exactly once, then disarm.
            if (_pendingScrollOffset >= 0 && e.ExtentHeightChange != 0)
            {
                double offset = _pendingScrollOffset;
                _pendingScrollOffset = -1;
                RestoreScrollOffset(offset);
            }
        }

        // ── Scroll helpers ────────────────────────────────────────────────

        private double GetScrollOffset()
            => FindScrollViewer(StartupList)?.VerticalOffset ?? 0;

        private void RestoreScrollOffset(double offset)
            => FindScrollViewer(StartupList)?.ScrollToVerticalOffset(offset);

        private void RestoreSelection(string registryKey)
        {
            if (StartupList.ItemsSource is not ListCollectionView lcv) return;
            foreach (var obj in lcv)
            {
                if (obj is StartupItem si && si.RegistryKey == registryKey)
                {
                    StartupList.SelectedItem = si;
                    break;
                }
            }
        }

        private static ScrollViewer FindScrollViewer(DependencyObject element)
        {
            if (element == null) return null;
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(element, i);
                if (child is ScrollViewer sv) return sv;
                var found = FindScrollViewer(child);
                if (found != null) return found;
            }
            return null;
        }

        // ── Event handlers ────────────────────────────────────────────────

        private void StartupList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VM == null) return;
            VM.SelectedItem = StartupList.SelectedItem as StartupItem;

            // When _suppressSelectionScroll is true we are mid-restore — don't let WPF
            // auto-scroll to the newly selected item (it would override our saved offset).
            if (_suppressSelectionScroll && StartupList.SelectedItem != null)
            {
                // Scroll to the item ourselves but we will override it right after with
                // the saved offset — so actually do nothing here, just block the default.
                e.Handled = true;
            }
        }

        private void StartupToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton tb && tb.Tag is StartupItem item)
                _ = (VM?.ToggleCommand as AsyncRelayCommand)?.ExecuteAsync(item);
            // No refresh needed — item.IsEnabled is updated directly, binding handles the UI
        }

        private void StartupCtx_Enable_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.SelectedItem = StartupList.SelectedItem as StartupItem;
            _ = (VM.EnableCommand as AsyncRelayCommand)?.ExecuteAsync();
            // No refresh needed — item.IsEnabled updated directly by ViewModel
        }

        private void StartupCtx_Disable_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.SelectedItem = StartupList.SelectedItem as StartupItem;
            _ = (VM.DisableCommand as AsyncRelayCommand)?.ExecuteAsync();
            // No refresh needed — item.IsEnabled updated directly by ViewModel
        }

        private void StartupCtx_CopyPath_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.SelectedItem = StartupList.SelectedItem as StartupItem;
            VM.CopyPathCommand.Execute(null);
        }

        private void StartupCtx_CopyRegKey_Click(object sender, RoutedEventArgs e)
        {
            var item = StartupList.SelectedItem as StartupItem;
            if (item == null) return;
            try
            {
                string key = item.RegistryKey?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(key)) System.Windows.Clipboard.SetText(key);
                VM?.SetStatus("Registry key copied to clipboard");
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(StartupCtx_CopyRegKey_Click)); }
        }

        private void StartupCtx_JumpToEntry_Click(object sender, RoutedEventArgs e)
        {
            var item = StartupList.SelectedItem as StartupItem;
            if (item == null) return;
            try
            {
                string fullKey = item.RegistryKey?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(fullKey)) return;
                string keyPath = fullKey.Contains("\\")
                    ? fullKey.Substring(0, fullKey.LastIndexOf('\\'))
                    : fullKey;
                using (var regKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit"))
                    regKey?.SetValue("LastKey", keyPath);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "regedit.exe",
                    UseShellExecute = true
                });
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(StartupCtx_JumpToEntry_Click)); }
        }

        private void StartupCtx_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.SelectedItem = StartupList.SelectedItem as StartupItem;
            double offset = GetScrollOffset();
            _ = (VM.DeleteCommand as AsyncRelayCommand)
                ?.ExecuteAsync()
                .ContinueWith(_ => Dispatcher.InvokeAsync(() =>
                {
                    // Item already removed from ObservableCollection by ViewModel.
                    // LCV updates automatically. Just restore scroll position.
                    _pendingScrollOffset = offset;
                }));
        }
    }
}