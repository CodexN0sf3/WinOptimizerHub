using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;
using WinOptimizerHub.ViewModels;

namespace WinOptimizerHub
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; } = new MainViewModel();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = ViewModel;
            ToastContainer.ItemsSource = ViewModel.Toast.Toasts;

            RestoreWindowState();

            Loaded += MainWindow_Loaded;
            StateChanged += MainWindow_StateChanged;
            Closing += MainWindow_Closing;
        }

        // ── Settings restore ──────────────────────────────────────────────
        private void RestoreWindowState()
        {
            var s = UserSettings.Current;

            if (s.WindowLeft >= 0 && s.WindowTop >= 0)
            {
                Left = s.WindowLeft;
                Top = s.WindowTop;
            }

            if (s.WindowWidth > 0) Width = s.WindowWidth;
            if (s.WindowHeight > 0) Height = s.WindowHeight;

            if (s.WindowMaximized)
            {
                var area = SystemParameters.WorkArea;
                MaxWidth = area.Width;
                MaxHeight = area.Height;
                WindowState = WindowState.Maximized;
            }

            if (ThemeToggle != null)
                ThemeToggle.IsChecked = !s.IsDarkTheme;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var s = UserSettings.Current;

            s.IsDarkTheme = App.IsDarkTheme;
            s.LastPanel = ViewModel.CurrentPanel;
            s.WindowMaximized = WindowState == WindowState.Maximized;

            if (WindowState == WindowState.Normal)
            {
                s.WindowLeft = Left;
                s.WindowTop = Top;
                s.WindowWidth = Width;
                s.WindowHeight = Height;
            }

            if (ViewModel.Cleanup?.CurrentMode != null)
                s.CleaningMode = ViewModel.Cleanup.CurrentMode.ToString();

            s.Save();
            ViewModel.OnClose();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ViewModel.Initialize();
                ViewModel.Recycle.RefreshInfo();
                ViewModel.FontCache.RefreshInfo();
                ViewModel.Disk.LoadDrives();

                await Task.WhenAll(
                    ViewModel.Startup.LoadAsync(),
                    ViewModel.Uninstall.LoadAsync(),
                    ViewModel.EventLogs.LoadAsync(),
                    ViewModel.Telemetry.LoadAsync()
                );

                var s = UserSettings.Current;
                if (!string.IsNullOrEmpty(s.LastPanel) && s.LastPanel != "Dashboard")
                {
                    ViewModel.CurrentPanel = s.LastPanel;

                    var navStack = (System.Windows.Controls.StackPanel)
                        ((System.Windows.Controls.ScrollViewer)
                            ((System.Windows.Controls.Border)
                                ((System.Windows.Controls.Grid)Content).Children[0]).Child).Content;
                    foreach (System.Windows.UIElement el in navStack.Children)
                    {
                        if (el is System.Windows.Controls.RadioButton rb)
                            rb.IsChecked = rb.Tag?.ToString() == s.LastPanel;
                    }
                }

                if (Enum.TryParse<Services.CleaningMode>(s.CleaningMode, out var mode))
                    ViewModel.Cleanup.CurrentMode = mode;
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(MainWindow_Loaded)); }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                var area = SystemParameters.WorkArea;
                MaxWidth = area.Width;
                MaxHeight = area.Height;
                MaxRestoreIcon.Text = "\uE923";
            }
            else if (WindowState == WindowState.Normal)
            {
                MaxWidth = double.PositiveInfinity;
                MaxHeight = double.PositiveInfinity;
                MaxRestoreIcon.Text = "\uE922";
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 2;

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) { MaximizeWindow_Click(sender, e); return; }

            if (WindowState == WindowState.Maximized)
            {
                var screenPos = PointToScreen(e.GetPosition(this));
                MaxWidth = double.PositiveInfinity;
                MaxHeight = double.PositiveInfinity;
                WindowState = WindowState.Normal;
                double ratio = screenPos.X / SystemParameters.PrimaryScreenWidth;
                Left = Math.Max(0, screenPos.X - RestoreBounds.Width * ratio);
                Top = Math.Max(0, screenPos.Y - 20);
                e.Handled = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var hwnd = new WindowInteropHelper(this).Handle;
                    SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                }), System.Windows.Threading.DispatcherPriority.Input);
                return;
            }

            DragMove();
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                MaxWidth = double.PositiveInfinity;
                MaxHeight = double.PositiveInfinity;
                WindowState = WindowState.Normal;
            }
            else
            {
                var area = SystemParameters.WorkArea;
                MaxWidth = area.Width;
                MaxHeight = area.Height;
                WindowState = WindowState.Maximized;
            }
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

        private void Toast_Close_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ToastItem item)
                ViewModel.Toast.Dismiss(item);
        }

        private void ToastClose_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ToastItem item)
                ViewModel.Toast.Dismiss(item);
        }

        private void ThemeToggle_Changed(object sender, RoutedEventArgs e)
            => ViewModel.IsDarkTheme = ThemeToggle.IsChecked != true;

        private void NavItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton rb && rb.Tag is string tag)
                ViewModel.CurrentPanel = tag;
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
            => ViewModel.ClearLogCommand.Execute(null);

        private void ExportLog_Click(object sender, RoutedEventArgs e)
            => ViewModel.ExportLogCommand.Execute(null);
    }
}