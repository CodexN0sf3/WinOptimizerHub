using System;
using System.ComponentModel;
using System.Windows.Media;

namespace WinOptimizerHub.Models
{
    public class StartupItem : INotifyPropertyChanged
    {
        private bool _isEnabled;
        public string Name { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string RegistryKey { get; set; } = string.Empty;
        public string Category { get; set; } = "Logon";
        public string RegistryKeyPath { get; set; } = string.Empty;
        public string ImpactLevel { get; set; } = "Unknown";
        private ImageSource _iconSource;
        public ImageSource IconSource
        {
            get => _iconSource;
            set { _iconSource = value; OnPropertyChanged(nameof(IconSource)); }
        }
        public string Description { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public int CategoryOrder => Category switch
        {
            "Run" => 0,
            "Startup Folder" => 1,
            "Alternate Shell" => 2,
            "Installed Components" => 3,
            "RunOnce" => 4,
            "Explorer" => 5,
            "Internet Explorer" => 6,
            "Scheduled Tasks" => 7,
            "Services" => 8,
            "Drivers" => 9,
            "Image Hijacks" => 10,
            "AppInit" => 11,
            "Winlogon" => 12,
            "Winlogon Notify" => 13,
            "Boot Execute" => 14,
            "LSA Providers" => 15,
            "Winsock Providers" => 16,
            "Print Monitors" => 17,
            "Network Providers" => 18,
            "KnownDLLs" => 19,
            _ => 99
        };

        public int RegistryKeyOrder
        {
            get
            {
                if (RegistryKeyPath.EndsWith("\\Run", StringComparison.OrdinalIgnoreCase)) return 0;
                if (RegistryKeyPath.EndsWith("\\RunOnce", StringComparison.OrdinalIgnoreCase)) return 1;
                if (RegistryKeyPath.EndsWith("\\RunOnceEx", StringComparison.OrdinalIgnoreCase)) return 2;
                if (RegistryKeyPath.IndexOf("Policies", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
                if (RegistryKeyPath.IndexOf("Terminal Server", StringComparison.OrdinalIgnoreCase) >= 0) return 4;
                if (RegistryKeyPath.IndexOf("Active Setup", StringComparison.OrdinalIgnoreCase) >= 0) return 5;
                if (RegistryKeyPath.IndexOf("Winlogon", StringComparison.OrdinalIgnoreCase) >= 0) return 6;
                if (RegistryKeyPath.IndexOf("SafeBoot", StringComparison.OrdinalIgnoreCase) >= 0) return 7;
                return 10;
            }
        }
        public string CategoryIcon => Category switch
        {
            "Run" => "\uE768",  // play/run
            "RunOnce" => "\uE72C",  // refresh/once
            "Startup Folder" => "\uE8B7",  // folder open
            "Alternate Shell" => "\uE756",  // command prompt
            "Installed Components" => "\uE9F5",  // components
            "Explorer" => "\uEC50",  // folder/explorer
            "Internet Explorer" => "\uE774",  // globe
            "Scheduled Tasks" => "\uE823",  // calendar
            "Services" => "\uECAA",  // settings gear
            "Drivers" => "\uE9D9",  // chip/driver
            "Image Hijacks" => "\uE7BA",  // warning
            "AppInit" => "\uECAA",
            "Winlogon" => "\uE8A5",  // person/logon
            "Winlogon Notify" => "\uE8A5",
            "Boot Execute" => "\uEDAB",  // power
            "LSA Providers" => "\uE8D7",  // lock
            "Winsock Providers" => "\uE968",  // network
            "Print Monitors" => "\uE749",  // printer
            "Network Providers" => "\uF6FA",  // network share
            "KnownDLLs" => "\uE8F4",  // library
            _ => "\uE9F5",
        };
        public string ImpactColor => ImpactLevel switch
        {
            "Low" => "#22C55E",
            "Medium" => "#F59E0B",
            "High" => "#EF4444",
            _ => "#8B949E"
        };
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}