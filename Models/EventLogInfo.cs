using System;
using WinOptimizerHub.Helpers;

namespace WinOptimizerHub.Models
{
    public class EventLogInfo : ObservableObject
    {
        private bool _isSelected;

        public string LogName { get; set; } = string.Empty;
        public long Entries { get; set; }
        public long SizeBytes { get; set; }
        public string SizeDisplay => FormatHelper.FormatSize(SizeBytes);
        public DateTime LastWriteTime { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
