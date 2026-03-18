using System.Collections.ObjectModel;

namespace WinOptimizerHub.Models
{
    public class TelemetryCategoryGroup
    {
        public string Category { get; set; }
        public int ActiveCount { get; set; }
        public ObservableCollection<TelemetryItem> Items { get; set; }
        public string Header => ActiveCount > 0
            ? $"{Category}  ({ActiveCount} active)"
            : Category;
        public string HeaderColor => ActiveCount > 0 ? "#EF4444" : "#22C55E";
    }
}