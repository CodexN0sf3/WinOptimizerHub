using WinOptimizerHub.Helpers;

namespace WinOptimizerHub.Models
{
    public class DiskInfo
    {
        public string DriveLetter { get; set; } = string.Empty;
        public string VolumeLabel { get; set; } = string.Empty;
        public string DriveType { get; set; } = string.Empty;
        public string FileSystem { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public long FreeSpace { get; set; }
        public long UsedSpace => TotalSize - FreeSpace;
        public double UsagePercent => TotalSize > 0 ? (double)UsedSpace / TotalSize * 100 : 0;
        public string TotalDisplay => FormatHelper.FormatSize(TotalSize);
        public string FreeDisplay => FormatHelper.FormatSize(FreeSpace);
        public string UsedDisplay => FormatHelper.FormatSize(UsedSpace);
        public bool IsSSD { get; set; }
    }
}
