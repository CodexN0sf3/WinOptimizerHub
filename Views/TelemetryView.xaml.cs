using System.Windows;
using System.Windows.Controls;
using WinOptimizerHub.Models;
using WinOptimizerHub.ViewModels;

namespace WinOptimizerHub.Views
{
    public partial class TelemetryView : UserControl
    {
        private TelemetryViewModel VM => DataContext as TelemetryViewModel;

        public TelemetryView()
        {
            InitializeComponent();
        }

        private void TelemetrySelectAll_Click(object sender, RoutedEventArgs e)
            => VM?.SelectAll();

        private void TelemetrySelectNone_Click(object sender, RoutedEventArgs e)
            => VM?.SelectNone();

        private void ToggleTelemetryItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TelemetryItem item)
                _ = (VM?.ToggleItemCommand as AsyncRelayCommand)?.ExecuteAsync(item);
        }
    }
}
