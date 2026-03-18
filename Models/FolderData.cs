using System.ComponentModel;
using WinOptimizerHub.Helpers;

namespace WinOptimizerHub.Models
{
    public class FolderData : INotifyPropertyChanged
    {
        private long _size;
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size
        {
            get => _size;
            set { _size = value; OnPropertyChanged(nameof(Size)); OnPropertyChanged(nameof(SizeDisplay)); }
        }
        public int FileCount { get; set; }
        public int FolderCount { get; set; }
        public double PercentOfParent { get; set; }
        public string SizeDisplay => FormatHelper.FormatSize(Size);

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
