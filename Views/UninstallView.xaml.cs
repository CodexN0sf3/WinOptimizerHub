using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WinOptimizerHub.Models;
using WinOptimizerHub.ViewModels;

namespace WinOptimizerHub.Views
{
    public partial class UninstallView : UserControl
    {
        private UninstallViewModel VM => DataContext as UninstallViewModel;

        public UninstallView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is UninstallViewModel oldVm)
                oldVm.PropertyChanged -= Vm_PropertyChanged;

            if (e.NewValue is UninstallViewModel newVm)
            {
                newVm.PropertyChanged += Vm_PropertyChanged;
                TabWin32_Click(null, null);
            }
        }

        private void Vm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(UninstallViewModel.Programs)) return;
            Dispatcher.Invoke(RefreshItemsSource);
        }

        private void RefreshItemsSource()
        {
            if (VM == null) return;
            if (VM.ActiveTab == "WindowsApps")
                ApplyStoreGrouping();
            else
                ProgramList.ItemsSource = VM.Programs;
        }

        private void RefreshPrograms_Click(object sender, RoutedEventArgs e)
            => _ = VM?.LoadAsync();

        private void TabWin32_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.ActiveTab = "Win32";
            TabWin32Btn.Background = (System.Windows.Media.Brush)FindResource("AccentBrush");
            TabStoreBtn.ClearValue(BackgroundProperty);
            ProgramList.ItemsSource = VM.Programs;
        }

        private void TabStore_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.ActiveTab = "WindowsApps";
            TabStoreBtn.Background = (System.Windows.Media.Brush)FindResource("AccentBrush");
            TabWin32Btn.ClearValue(BackgroundProperty);
            ApplyStoreGrouping();
        }

        private void ApplyStoreGrouping()
        {
            if (VM?.ActiveTab != "WindowsApps") return;
            var lcv = new ListCollectionView(VM.Programs);
            lcv.GroupDescriptions.Add(new PropertyGroupDescription(nameof(InstalledProgram.AppGroup)));
            lcv.SortDescriptions.Add(new SortDescription(nameof(InstalledProgram.AppGroup), ListSortDirection.Ascending));
            lcv.SortDescriptions.Add(new SortDescription(nameof(InstalledProgram.Name), ListSortDirection.Ascending));
            ProgramList.ItemsSource = lcv;
        }

        private void ProgramList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VM != null) VM.SelectedProgram = ProgramList.SelectedItem as InstalledProgram;
        }

        private void UninstallProgram_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.SelectedProgram = ProgramList.SelectedItem as InstalledProgram;
            _ = (VM.UninstallCommand as AsyncRelayCommand)?.ExecuteAsync();
        }

        private void OpenInstallFolder_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.SelectedProgram = ProgramList.SelectedItem as InstalledProgram;
            VM.OpenInstallFolderCommand.Execute(null);
        }

        private void CopyProgramName_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.SelectedProgram = ProgramList.SelectedItem as InstalledProgram;
            VM.CopyNameCommand.Execute(null);
        }

        private void CopyUninstallString_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.SelectedProgram = ProgramList.SelectedItem as InstalledProgram;
            VM.CopyUninstallStringCommand.Execute(null);
        }

        private void ForceUninstall_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.SelectedProgram = ProgramList.SelectedItem as InstalledProgram;
            VM.ForceUninstallCommand.Execute(null);
        }
    }
}