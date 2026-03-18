using System.Windows;
using System.Windows.Controls;
using WinOptimizerHub.Services;
using WinOptimizerHub.ViewModels;

namespace WinOptimizerHub.Views
{
    public partial class DashboardView : UserControl
    {
        private MainViewModel VM => DataContext as MainViewModel;

        public DashboardView()
        {
            InitializeComponent();
        }

        private void CleanMode_Changed(object sender, RoutedEventArgs e)
        {
            if (VM?.Cleanup == null) return;
            if (ModeSafe.IsChecked == true)            VM.Cleanup.CurrentMode = CleaningMode.Safe;
            else if (ModeNormal.IsChecked == true)     VM.Cleanup.CurrentMode = CleaningMode.Normal;
            else if (ModeAggressive.IsChecked == true) VM.Cleanup.CurrentMode = CleaningMode.Aggressive;
        }

        private void QuickClean_Click(object sender, RoutedEventArgs e)
        {
            VM?.NavigateCommand.Execute("Cleanup");
            _ = VM?.Cleanup.ScanAsync();
        }

        private void QuickFlushDns_Click(object sender, RoutedEventArgs e)
            => _ = (VM?.Network.FlushDnsCommand as AsyncRelayCommand)?.ExecuteAsync();

        private void QuickRamOptimize_Click(object sender, RoutedEventArgs e)
            => _ = (VM?.Ram.OptimizeCommand as AsyncRelayCommand)?.ExecuteAsync();

        private void QuickEmptyRecycleBin_Click(object sender, RoutedEventArgs e)
            => _ = (VM?.Recycle.EmptyCommand as AsyncRelayCommand)?.ExecuteAsync();

        private void QuickSfc_Click(object sender, RoutedEventArgs e)
        {
            VM?.NavigateCommand.Execute("SystemTools");
            _ = VM?.SystemTools.RunSfcAsync();
        }

        private void QuickDefrag_Click(object sender, RoutedEventArgs e)
        {
            VM?.NavigateCommand.Execute("SystemTools");
            _ = (VM?.SystemTools.RunDefragCommand as AsyncRelayCommand)?.ExecuteAsync();
        }
    }
}
