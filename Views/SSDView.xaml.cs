using System.Windows;
using System.Windows.Controls;
using WinOptimizerHub.Models;
using WinOptimizerHub.ViewModels;

namespace WinOptimizerHub.Views
{
    public partial class SSDView : UserControl
    {
        private SSDViewModel VM => DataContext as SSDViewModel;

        public SSDView()
        {
            InitializeComponent();
        }

        private void ApplySSDTweak_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is SSDTweak tweak)
                _ = (VM?.ApplyOneCommand as AsyncRelayCommand)?.ExecuteAsync(tweak);
        }
    }
}
