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
            "0" => "Install",       // APPLICATION_INSTALL
            "1" => "Uninstall",     // APPLICATION_UNINSTALL
            "10" => "Install",       // DEVICE_DRIVER_INSTALL
            "12" => "System",        // MODIFY_SETTINGS (shown as "System" in rstrui)
            "13" => "Undo",          // CANCELLED_OPERATION
            "14" => "System",        // SYSTEM_CHECKPOINT
            "15" => "Undo",          // UNDO
            "16" => "Manual",        // USER_CHECKPOINT (shown as "Manual" in rstrui)
            "17" => "Windows Update",
            "18" => "Critical Update",
            _ => string.IsNullOrEmpty(RestorePointType) ? "—" : RestorePointType
        };

        public string TypeIcon => RestorePointType switch
        {
            "0" or "1" => "",  // app install/uninstall
            "10" => "",  // driver
            "17" or "18" => "", // windows update
            "16" => "",  // manual/user
            _ => ""   // generic
        };
    }
}