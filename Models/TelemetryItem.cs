namespace WinOptimizerHub.Models
{
    public class TelemetryItem : ObservableObject
    {
        private bool _isSelected = true;
        private bool _isCurrentlyEnabled;

        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsApplied { get; set; }

        public bool IsCurrentlyEnabled
        {
            get => _isCurrentlyEnabled;
            set => SetProperty(ref _isCurrentlyEnabled, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}