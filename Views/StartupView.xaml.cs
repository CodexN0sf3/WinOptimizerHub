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

        private bool _suppressSelectionScroll;

        public StartupView()
        {
            InitializeComponent();
            Loaded += OnViewLoaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;

            if (VM.Items.Count > 0)
                ApplyGrouping();
            else if (!VM.IsBusy)
                _ = VM.LoadAsync().ContinueWith(_ => Dispatcher.Invoke(ApplyGrouping));
            else
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

        private double _pendingScrollOffset = -1;

        private void StartupList_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_pendingScrollOffset >= 0 && e.ExtentHeightChange != 0)
            {
                double offset = _pendingScrollOffset;
                _pendingScrollOffset = -1;
                RestoreScrollOffset(offset);
            }
        }

        private double GetScrollOffset()
            => FindScrollViewer(StartupList)?.VerticalOffset ?? 0;

        private void RestoreScrollOffset(double offset)
            => FindScrollViewer(StartupList)?.ScrollToVerticalOffset(offset);

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

        private void StartupList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VM == null) return;
            VM.SelectedItem = StartupList.SelectedItem as StartupItem;

            if (_suppressSelectionScroll && StartupList.SelectedItem != null)
            {
                e.Handled = true;
            }
        }

        private void StartupToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton tb && tb.Tag is StartupItem item)
                _ = (VM?.ToggleCommand as AsyncRelayCommand)?.ExecuteAsync(item);
        }

        private void StartupCtx_Enable_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.SelectedItem = StartupList.SelectedItem as StartupItem;
            _ = (VM.EnableCommand as AsyncRelayCommand)?.ExecuteAsync();
        }

        private void StartupCtx_Disable_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.SelectedItem = StartupList.SelectedItem as StartupItem;
            _ = (VM.DisableCommand as AsyncRelayCommand)?.ExecuteAsync();
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
                    _pendingScrollOffset = offset;
                }));
        }
    }
}