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

            // ── Global exception handlers ─────────────────────────────────

            // 1. WPF UI thread exceptions
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            // 2. Background Task exceptions that were never awaited/observed
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            // 3. Non-WPF thread exceptions (finalizers, thread pool etc.)
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            // ── Admin check ───────────────────────────────────────────────
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

            // ── Load persisted settings ───────────────────────────────────
            var settings = UserSettings.Current;
            IsDarkTheme = settings.IsDarkTheme;
            ApplyTheme();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Window state is saved by MainWindow on Close
            base.OnExit(e);
        }

        // ── Exception handlers ────────────────────────────────────────────

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            AppLogger.Log(e.Exception, "UI.UnhandledException");
            e.Handled = true; // prevent crash

            // Show toast if main window is available, otherwise MessageBox
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

        // ── Helpers ───────────────────────────────────────────────────────

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
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (System.ComponentModel.Win32Exception) { /* user cancelled UAC */ }
        }
    }
}