using WinOptimizerHub.Helpers;

namespace WinOptimizerHub.Models
{
    public class InstalledProgram
    {
        public string Name { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        private string _installDate = string.Empty;
        public string InstallDate
        {
            get => _installDate;
            set
            {
                _installDate = value;

                if (value?.Length == 8 &&
                    int.TryParse(value.Substring(0, 4), out int y) &&
                    int.TryParse(value.Substring(4, 2), out int m) &&
                    int.TryParse(value.Substring(6, 2), out int d))
                {
                    try { _installDate = new System.DateTime(y, m, d).ToString("dd MMM yyyy"); }
                    catch { _installDate = value; }
                }
            }
        }
        public long EstimatedSize { get; set; }
        public string UninstallString { get; set; } = string.Empty;
        public string InstallLocation { get; set; } = string.Empty;
        public string DisplayIcon { get; set; } = string.Empty;
        public string RegistryKey { get; set; } = string.Empty;

        public bool IsWindowsApp { get; set; }
        public string PackageFullName { get; set; } = string.Empty;
        public bool IsSystemApp { get; set; }

        public string SizeDisplay => EstimatedSize > 0
            ? FormatHelper.FormatSize(EstimatedSize * 1024) : "N/A";

        public string AppGroup => IsWindowsApp
            ? (IsSystemApp ? "System Components" : "Store Apps")
            : "Win32";

        public string TypeBadge => IsWindowsApp
            ? (IsSystemApp ? "⊞ System" : "⊞ Store")
            : "Win32";
        public string TypeColor => IsWindowsApp
            ? (IsSystemApp ? "#8B5CF6" : "#3B82F6")
            : "#9CA3AF";
    }
}