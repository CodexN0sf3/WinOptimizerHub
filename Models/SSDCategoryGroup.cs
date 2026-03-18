using System.Collections.ObjectModel;

namespace WinOptimizerHub.Models
{
    public class SSDCategoryGroup
    {
        public string Category { get; set; }
        public int NeedCount { get; set; }
        public ObservableCollection<SSDTweak> Items { get; set; }
        public string Header => NeedCount > 0 ? $"{Category}  ({NeedCount} to optimize)" : Category;
        public string HeaderColor => NeedCount > 0 ? "#F59E0B" : "#22C55E";
    }
}