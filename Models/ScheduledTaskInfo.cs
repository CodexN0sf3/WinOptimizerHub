namespace WinOptimizerHub.Models
{
    public class ScheduledTaskInfo : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string LastRunTime { get; set; } = string.Empty;
        public string NextRunTime { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        public string StatusColor => Status switch
        {
            "Ready" or "Running" => "#22C55E",
            "Disabled" => "#8B949E",
            _ => "#F59E0B"
        };
    }
}