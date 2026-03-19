using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.ViewModels;

namespace WinOptimizerHub.Views
{
    public partial class PowerView : UserControl
    {
        private PowerViewModel VM => DataContext as PowerViewModel;

        public PowerView()
        {
            InitializeComponent();
        }

        private void PowerShutdown_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Shut down this computer?", "Confirm Shutdown",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            System.Diagnostics.Process.Start("shutdown.exe", "/s /t 30 /c \"Shutting down via WinOptimizerHub in 30 seconds...\"");
            MessageBox.Show("Shutdown scheduled in 30 seconds.\n\nRun 'shutdown /a' to abort.", "Shutdown", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PowerRestart_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Restart this computer?", "Confirm Restart",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            System.Diagnostics.Process.Start("shutdown.exe", "/r /t 30 /c \"Restarting via WinOptimizerHub in 30 seconds...\"");
            MessageBox.Show("Restart scheduled in 30 seconds.\n\nRun 'shutdown /a' to abort.", "Restart", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PowerSleep_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Put this computer to sleep?", "Confirm Sleep",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0")
            { UseShellExecute = true });
        }

        private void PowerHibernate_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Hibernate this computer?", "Confirm Hibernate",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            System.Diagnostics.Process.Start("shutdown.exe", "/h");
        }

        private void PowerLock_Click(object sender, RoutedEventArgs e)
            => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "rundll32.exe", "user32.dll,LockWorkStation")
            { UseShellExecute = true });

        private void PowerSignOut_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Sign out the current user?", "Confirm Sign Out",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            System.Diagnostics.Process.Start("shutdown.exe", "/l");
        }

        private void PowerSwitchUser_Click(object sender, RoutedEventArgs e)
            => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "tsdiscon.exe")
            { UseShellExecute = true });

        private async void PowerRestartExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Restart Windows Explorer?\n\nThe taskbar and desktop will briefly disappear.",
                "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                foreach (var p in System.Diagnostics.Process.GetProcessesByName("explorer"))
                    p.Kill();
                await Task.Delay(1500);
                System.Diagnostics.Process.Start("explorer.exe");
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(PowerRestartExplorer_Click)); }
        }

        private void PowerFlushDns_Click(object sender, RoutedEventArgs e)
            => _ = (VM?.Network.FlushDnsCommand as AsyncRelayCommand)?.ExecuteAsync();

        private void PowerClearTemp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string tmp = System.IO.Path.GetTempPath();
                int deleted = 0;
                foreach (var f in System.IO.Directory.GetFiles(tmp))
                    try { System.IO.File.Delete(f); deleted++; } catch  { AppLogger.Log(new Exception("Unhandled"), nameof(AppLogger)); }
                MessageBox.Show($"Cleared {deleted} temp files.", "WinOptimizerHub", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(PowerClearTemp_Click)); }
        }

        private void PowerOpenBackupFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.IO.Directory.CreateDirectory(AppLogger.BackupRoot);
                System.Diagnostics.Process.Start("explorer.exe", AppLogger.BackupRoot);
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(PowerOpenBackupFolder_Click)); }
        }

        private void PowerOpenLogsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string logsDir = AppLogger.BackupRoot.Replace("Backups", "Logs");
                System.IO.Directory.CreateDirectory(logsDir);
                System.Diagnostics.Process.Start("explorer.exe", logsDir);
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(PowerOpenLogsFolder_Click)); }
        }
    }
}