using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;
using WinOptimizerHub.ViewModels;

namespace WinOptimizerHub.Views
{
    public partial class StartupView : UserControl
    {
        private StartupViewModel VM => DataContext as StartupViewModel;

        public StartupView()
        {
            InitializeComponent();
            DataContextChanged += (_, __) =>
            {
                if (VM?.Items?.Count > 0)
                    ApplyGrouping();
                else if (VM != null)
                    VM.Items.CollectionChanged += (s, e2) =>
                    {
                        if (VM.Items.Count > 0) Dispatcher.Invoke(ApplyGrouping);
                    };
            };
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

        private async Task RefreshAfterOperationAsync()
        {
            await Task.Delay(800);
            await VM.LoadAsync();
            Dispatcher.Invoke(ApplyGrouping);
        }

        private void StartupList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VM != null) VM.SelectedItem = StartupList.SelectedItem as StartupItem;
        }

        private void StartupToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton tb && tb.Tag is StartupItem item)
                _ = (VM?.ToggleCommand as AsyncRelayCommand)
                    ?.ExecuteAsync(item)
                    .ContinueWith(_ => RefreshAfterOperationAsync());
        }

        private void StartupCtx_Enable_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.SelectedItem = StartupList.SelectedItem as StartupItem;
            _ = (VM.EnableCommand as AsyncRelayCommand)
                ?.ExecuteAsync()
                .ContinueWith(_ => RefreshAfterOperationAsync());
        }

        private void StartupCtx_Disable_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.SelectedItem = StartupList.SelectedItem as StartupItem;
            _ = (VM.DisableCommand as AsyncRelayCommand)
                ?.ExecuteAsync()
                .ContinueWith(_ => RefreshAfterOperationAsync());
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
                if (!string.IsNullOrEmpty(key))
                    System.Windows.Clipboard.SetText(key);
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
                {
                    regKey?.SetValue("LastKey", keyPath);
                }

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
            _ = (VM.DeleteCommand as AsyncRelayCommand)
                ?.ExecuteAsync()
                .ContinueWith(_ => Dispatcher.Invoke(ApplyGrouping));
        }
    }
}
