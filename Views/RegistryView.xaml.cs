using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using WinOptimizerHub.Models;
using WinOptimizerHub.ViewModels;

namespace WinOptimizerHub.Views
{
    public partial class RegistryView : UserControl
    {
        private RegistryViewModel VM => DataContext as RegistryViewModel;

        public RegistryView()
        {
            InitializeComponent();
            DataContextChanged += (_, __) => ApplyGrouping();
        }

        private void ScanRegistry_Click(object sender, System.Windows.RoutedEventArgs e)
            => _ = VM?.ScanAsync().ContinueWith(_ =>
                Dispatcher.Invoke(ApplyGrouping));

        private void ApplyGrouping()
        {
            if (VM == null) return;
            var lcv = new ListCollectionView(VM.Issues);
            lcv.GroupDescriptions.Add(new PropertyGroupDescription(nameof(RegistryIssue.IssueType)));
            lcv.SortDescriptions.Add(new SortDescription(nameof(RegistryIssue.IssueType), ListSortDirection.Ascending));
            lcv.SortDescriptions.Add(new SortDescription(nameof(RegistryIssue.KeyPath), ListSortDirection.Ascending));
            RegistryList.ItemsSource = lcv;
        }

        private void RegistrySelectAll_Click(object sender, System.Windows.RoutedEventArgs e)
            => VM?.SelectAll();

        private void RegistrySelectNone_Click(object sender, System.Windows.RoutedEventArgs e)
            => VM?.SelectNone();

        private void RegistryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView lv && lv.SelectedItem is RegistryIssue issue && VM != null)
                VM.SelectedIssue = issue;
        }
    }
}
