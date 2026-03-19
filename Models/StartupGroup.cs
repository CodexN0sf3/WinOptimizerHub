using System.Collections.ObjectModel;

namespace WinOptimizerHub.Models
{
    public class StartupGroup
    {
        public string Category { get; set; }
        public ObservableCollection<StartupKeyGroup> KeyGroups { get; } = new();
    }
}