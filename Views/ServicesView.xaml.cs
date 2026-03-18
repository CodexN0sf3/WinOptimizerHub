using System.Windows;
using System.Windows.Controls;
using WinOptimizerHub.Models;
using WinOptimizerHub.ViewModels;

namespace WinOptimizerHub.Views
{
    public partial class ServicesView : UserControl
    {
        private ServicesViewModel VM => DataContext as ServicesViewModel;

        public ServicesView()
        {
            InitializeComponent();
        }

        private void ServiceList_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void ServiceApply_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ServiceInfo svc)
                _ = (VM?.ApplyCommand as AsyncRelayCommand)?.ExecuteAsync(svc);
        }

        private void ServiceContextMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && ServiceList.SelectedItem is ServiceInfo svc)
                _ = (VM?.SetStartTypeCommand as AsyncRelayCommand)
                    ?.ExecuteAsync((svc, mi.Tag?.ToString() ?? "Manual"));
        }

        private void ServiceStartStop_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && ServiceList.SelectedItem is ServiceInfo svc)
                _ = (VM?.StartStopCommand as AsyncRelayCommand)
                    ?.ExecuteAsync((svc, mi.Tag?.ToString() == "Start"));
        }
    }
}
