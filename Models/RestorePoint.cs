using System;
using WinOptimizerHub.Helpers;

namespace WinOptimizerHub.Models
{
    public class RestorePoint
    {
        public uint SequenceNumber { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime CreationTime { get; set; }
        public string RestorePointType { get; set; } = string.Empty;
        public long Size { get; set; }
        public bool IsSelected { get; set; }

        public string SizeDisplay => Size > 0 ? FormatHelper.FormatSize(Size) : "—";
        public string FormattedDate => CreationTime.ToString("dd MMM yyyy  HH:mm");

        public string TypeDisplay => RestorePointType switch
        {
            "0" => "Install",
            "1" => "Uninstall",
            "10" => "Install",
            "12" => "System",
            "13" => "Undo",
            "14" => "System",
            "15" => "Undo",
            "16" => "Manual",
            "17" => "Windows Update",
            "18" => "Critical Update",
            _ => string.IsNullOrEmpty(RestorePointType) ? "—" : RestorePointType
        };

        public string TypeIcon => RestorePointType switch
        {
            "0" or "1" => "",
            "10" => "",
            "17" or "18" => "",
            "16" => "",
            _ => ""
        };
    }
}