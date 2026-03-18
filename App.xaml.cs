using System;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;

namespace WinOptimizerHub
{
    public partial class App : Application
    {
        public static bool IsDarkTheme { get; private set; } = false;

        public static void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
            ApplyTheme();
            UserSettings.Current.IsDarkTheme = IsDarkTheme;
            UserSettings.Current.Save();
        }

        public static void ApplyTheme()
        {
            var dict = new ResourceDictionary
            {
                Source = IsDarkTheme
                    ? new Uri("Themes/DarkTheme.xaml", UriKind.Relative)
                    : new Uri("Themes/LightTheme.xaml", UriKind.Relative)
            };
            Current.Resources.MergedDictionaries.Clear();
            Current.Resources.MergedDictionaries.Add(dict);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += OnDispatcherUnhandledException;

            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            if (!IsRunningAsAdministrator())
            {
                var customMsgBox = new CustomMessageBoxWindow(
                    "Administrator Required",
                    "WinOptimizerHub requires administrator privileges to clean the registry, manage startup items, and perform system optimizations.\n\nRestart as administrator?",ConfirmIcon.Warning,"Restart as Admin", "Exit");

                if (customMsgBox.ShowDialog() == true)
                    RestartAsAdmin();

                Shutdown(1);
                return;
            }

            var settings = UserSettings.Current;
            IsDarkTheme = settings.IsDarkTheme;
            ApplyTheme();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            AppLogger.Log(e.Exception, "UI.UnhandledException");
            e.Handled = true; // prevent crash

            if (Current?.MainWindow?.DataContext is ViewModels.MainViewModel vm)
                vm.Toast.ShowError("Unexpected Error", e.Exception.Message);
            else
                MessageBox.Show(
                    $"Unexpected error: {e.Exception.Message}",
                    "WinOptimizerHub", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved(); // prevent process crash
            foreach (var ex in e.Exception.InnerExceptions)
                AppLogger.Log(ex, "Task.UnobservedException");
        }

        private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                AppLogger.Log(ex, "AppDomain.UnhandledException");
        }

        private static bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        private static void RestartAsAdmin()
        {
            try
            {
                var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = currentProcess.MainModule?.FileName,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                if (System.Diagnostics.Process.Start(psi) != null)
                {
                    System.Windows.Application.Current.Shutdown();
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                AppLogger.Log(ex, "RestartAsAdmin.UserCancelledElevation");
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, "RestartAsAdmin.GeneralFailure");
            }
        }
    }
}