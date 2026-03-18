using System.Windows;

namespace WinOptimizerHub.Models
{
    public enum ConfirmIcon { Question, Warning, Danger }

    public partial class CustomMessageBoxWindow : Window
    {
        public CustomMessageBoxWindow(string title, string message,
                                      ConfirmIcon icon = ConfirmIcon.Question,
                                      string confirmText = "Confirm",
                                      string cancelText = "Cancel")
        {
            InitializeComponent();

            TitleText.Text  = title;
            MessageText.Text = message;
            BtnYes.Content  = confirmText;
            BtnNo.Content   = cancelText;

            switch (icon)
            {
                case ConfirmIcon.Warning:
                    IconGlyph.Text = "\uE7BA";
                    IconBorder.Background = System.Windows.Media.Brushes.Transparent;
                    IconBorder.SetResourceReference(BackgroundProperty, "WarningBrush");
                    break;
                case ConfirmIcon.Danger:
                    IconGlyph.Text = "\uE74D";
                    IconBorder.Background = System.Windows.Media.Brushes.Transparent;
                    IconBorder.SetResourceReference(BackgroundProperty, "DangerBrush");
                    break;
                default:
                    IconGlyph.Text = "\uE897";
                    break;
            }
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e) { DialogResult = true;  Close(); }
        private void BtnNo_Click (object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}
