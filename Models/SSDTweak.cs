namespace WinOptimizerHub.Models
{
    public class SSDTweak : ObservableObject
    {
        private bool _isSelected = true;
        private bool _isCurrentlyOptimal;

        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        public bool IsCurrentlyOptimal
        {
            get => _isCurrentlyOptimal;
            set
            {
                if (SetProperty(ref _isCurrentlyOptimal, value))
                {
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string StatusText => IsCurrentlyOptimal ? "Optimal" : "Not Optimal";
        public string StatusColor => IsCurrentlyOptimal ? "#22C55E" : "#F59E0B";
    }
}
