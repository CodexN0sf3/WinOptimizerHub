using System.Windows;
using System.Windows.Controls;
using WinOptimizerHub.ViewModels;

namespace WinOptimizerHub.Views
{
    public partial class BrowserView : UserControl
    {
        private BrowserViewModel VM => DataContext as BrowserViewModel;

        public BrowserView()
        {
            InitializeComponent();
        }

        private void CleanBrowsers_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.CleanHistory = CleanBrowserHistory.IsChecked == true;
            VM.CleanCookies = CleanBrowserCookies.IsChecked == true;
            _ = (VM.CleanCommand as AsyncRelayCommand)?.ExecuteAsync();
        }
    }
}
