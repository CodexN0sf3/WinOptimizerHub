using System.Windows;
using System.Windows.Controls;
using WinOptimizerHub.ViewModels;

namespace WinOptimizerHub.Views
{
    public partial class DiskView : UserControl
    {
        private DiskViewModel VM => DataContext as DiskViewModel;

        public DiskView()
        {
            InitializeComponent();
        }

        private void AnalyzeDisk_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.AnalyzePath = DiskAnalyzePath.Text;
            _ = (VM.AnalyzeCommand as AsyncRelayCommand)?.ExecuteAsync();
        }
    }
}
