using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using WinOptimizerHub.ViewModels;

namespace WinOptimizerHub.Views
{
    public partial class WinSxSView : UserControl
    {
        private WinSxSViewModel VM => DataContext as WinSxSViewModel;

        public WinSxSView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is WinSxSViewModel oldVm && oldVm.OutputLines is INotifyCollectionChanged oldCol)
                oldCol.CollectionChanged -= OutputLines_Changed;

            if (e.NewValue is WinSxSViewModel newVm && newVm.OutputLines is INotifyCollectionChanged newCol)
                newCol.CollectionChanged += OutputLines_Changed;
        }

        private void OutputLines_Changed(object sender, NotifyCollectionChangedEventArgs e)
        {
            try { Dispatcher.InvokeAsync(() => WinSxSScroller.ScrollToBottom()); }
            catch  { AppLogger.Log(new Exception("Unhandled"), nameof(AppLogger)); }
        }
    }
}
