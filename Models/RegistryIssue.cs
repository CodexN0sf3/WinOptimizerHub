namespace WinOptimizerHub.Models
{
    public class RegistryIssue : ObservableObject
    {
        public string KeyPath { get; set; } = string.Empty;
        public string ValueName { get; set; } = string.Empty;
        public string IssueType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSafe { get; set; } = true;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string SafetyColor => IsSafe ? "#22C55E" : "#F59E0B";
    }
}