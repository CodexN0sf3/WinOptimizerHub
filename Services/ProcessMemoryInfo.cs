using WinOptimizerHub.Models;

namespace WinOptimizerHub.Services
{
    public class ProcessMemoryInfo : ObservableObject
    {
        public string Name { get; }
        public int Pid { get; }
        public double MemMB { get; }
        public double MemPercent { get; }
        public bool IsSystem { get; }

        public string MemDisplay => $"{MemMB:F0} MB";
        public string PercentDisplay => $"{MemPercent:F1}%";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public ProcessMemoryInfo(string name, int pid, double memMB, double totalMB, bool isSystem)
        {
            Name = name;
            Pid = pid;
            MemMB = memMB;
            MemPercent = totalMB > 0 ? memMB / totalMB * 100 : 0;
            IsSystem = isSystem;
            _isSelected = !isSystem;
        }
    }
}