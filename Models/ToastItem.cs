using System.Windows.Media;

namespace WinOptimizerHub.Models
{
    public class ToastItem
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
        public string Icon { get; set; } = "\uE946";
        public Brush AccentColor { get; set; }
        public Brush CardBackground { get; set; }
    }
}