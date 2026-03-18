using System.Windows;
using WinOptimizerHub.Models;

namespace WinOptimizerHub.Helpers
{
    public static class DialogService
    {
        public static bool Confirm(string title, string message,
                                   string confirmText = "Confirm",
                                   string cancelText  = "Cancel")
            => Show(title, message, ConfirmIcon.Question, confirmText, cancelText);

        public static bool ConfirmWarning(string title, string message,
                                          string confirmText = "Continue",
                                          string cancelText  = "Cancel")
            => Show(title, message, ConfirmIcon.Warning, confirmText, cancelText);

        public static bool ConfirmDanger(string title, string message,
                                         string confirmText = "Delete",
                                         string cancelText  = "Cancel")
            => Show(title, message, ConfirmIcon.Danger, confirmText, cancelText);

        private static bool Show(string title, string message,
                                  ConfirmIcon icon,
                                  string confirmText, string cancelText)
        {
            var dlg = new CustomMessageBoxWindow(title, message, icon, confirmText, cancelText)
            {
                Owner = Application.Current?.MainWindow
            };
            return dlg.ShowDialog() == true;
        }
    }
}
