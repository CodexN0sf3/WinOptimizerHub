using System.Windows;
using System.Windows.Controls;
using WinOptimizerHub.Models;
using WinOptimizerHub.ViewModels;

namespace WinOptimizerHub.Views
{
    public partial class TaskSchedulerView : UserControl
    {
        private TaskSchedulerViewModel VM => DataContext as TaskSchedulerViewModel;

        public TaskSchedulerView()
        {
            InitializeComponent();
        }

        private void ToggleTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ScheduledTaskInfo task)
                _ = (VM?.ToggleCommand as AsyncRelayCommand)?.ExecuteAsync(task);
        }
    }
}
