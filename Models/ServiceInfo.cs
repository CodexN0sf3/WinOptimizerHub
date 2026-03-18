using System.ComponentModel;

namespace WinOptimizerHub.Models
{
    public class ServiceInfo : INotifyPropertyChanged
    {
        private string _status = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string StartType { get; set; } = string.Empty;
        public string RecommendedStartType { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public bool IsSafeToDisable { get; set; }
        public string Category { get; set; } = "Other";
        public bool NeedsOptimization => IsSafeToDisable && StartType != RecommendedStartType;

        public string StatusColor => Status switch
        {
            "Running" => "#22C55E",
            "Stopped" => "#8B949E",
            _ => "#F59E0B"
        };
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(StatusColor)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}