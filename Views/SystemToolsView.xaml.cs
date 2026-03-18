using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using WinOptimizerHub.ViewModels;

namespace WinOptimizerHub.Views
{
    public partial class SystemToolsView : UserControl
    {
        private SystemToolsViewModel VM => DataContext as SystemToolsViewModel;

        public SystemToolsView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is SystemToolsViewModel oldVm)
                oldVm.OutputLines.CollectionChanged -= OutputLines_Changed;

            if (e.NewValue is SystemToolsViewModel newVm)
                newVm.OutputLines.CollectionChanged += OutputLines_Changed;
        }

        private void OutputLines_Changed(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add ||
                e.Action == NotifyCollectionChangedAction.Reset)
            {
                OutputScroller?.ScrollToBottom();
            }
        }

        private void RunSfc_Click(object sender, RoutedEventArgs e)
            => _ = VM?.RunSfcAsync();

        private void RunDismCheck_Click(object sender, RoutedEventArgs e)
            => _ = (VM?.RunDismCheckCommand as AsyncRelayCommand)?.ExecuteAsync();

        private void RunDismRestore_Click(object sender, RoutedEventArgs e)
            => _ = (VM?.RunDismRestoreCommand as AsyncRelayCommand)?.ExecuteAsync();

        private void RunDefrag_Click(object sender, RoutedEventArgs e)
            => _ = (VM?.RunDefragCommand as AsyncRelayCommand)?.ExecuteAsync();

        private void CopyOutput_Click(object sender, RoutedEventArgs e)
            => VM?.CopyOutputCommand.Execute(null);

        private void OpenTaskManager_Click(object sender, RoutedEventArgs e)
            => VM?.OpenTaskManagerCommand.Execute(null);

        private void OpenDeviceManager_Click(object sender, RoutedEventArgs e)
            => VM?.OpenDeviceManagerCommand.Execute(null);

        private void OpenDiskMgmt_Click(object sender, RoutedEventArgs e)
            => VM?.OpenDiskMgmtCommand.Execute(null);

        private void OpenResourceMonitor_Click(object sender, RoutedEventArgs e)
            => VM?.OpenResourceMonitorCommand.Execute(null);

        private void OpenEventViewer_Click(object sender, RoutedEventArgs e)
            => VM?.OpenEventViewerCommand.Execute(null);

        private void OpenRegedit_Click(object sender, RoutedEventArgs e)
            => VM?.OpenRegeditCommand.Execute(null);

        private void OpenGpedit_Click(object sender, RoutedEventArgs e)
            => VM?.OpenGpeditCommand.Execute(null);

        private void OpenSysProperties_Click(object sender, RoutedEventArgs e)
            => VM?.OpenSysPropertiesCommand.Execute(null);
    }
}