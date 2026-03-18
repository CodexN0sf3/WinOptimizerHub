using System.Windows;
using System.Windows.Controls;
using WinOptimizerHub.Services;
using WinOptimizerHub.ViewModels;

namespace WinOptimizerHub.Views
{
    public partial class CleanupView : UserControl
    {
        private CleanupViewModel VM => DataContext as CleanupViewModel;

        public CleanupView()
        {
            InitializeComponent();
        }

        private void CleanMode_Changed(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            if (ModeSafe.IsChecked == true)            VM.CurrentMode = CleaningMode.Safe;
            else if (ModeNormal.IsChecked == true)     VM.CurrentMode = CleaningMode.Normal;
            else if (ModeAggressive.IsChecked == true) VM.CurrentMode = CleaningMode.Aggressive;
        }
    }
}
