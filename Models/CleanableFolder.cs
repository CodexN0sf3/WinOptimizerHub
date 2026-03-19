using WinOptimizerHub.Helpers;
using WinOptimizerHub.Services;

namespace WinOptimizerHub.Models
{

    public class CleanableFolder : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public int FileCount { get; set; }
        public string Category { get; set; } = string.Empty;
        public CleaningMode MinMode { get; set; } = CleaningMode.Safe;
        public CleanStrategy Strategy { get; set; } = CleanStrategy.Folder;
        public string[] Extensions { get; set; } = null;

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private string _description;
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public bool HasDescription => !string.IsNullOrEmpty(_description);

        public string SizeDisplay => FormatHelper.FormatSize(Size);
    }
}

