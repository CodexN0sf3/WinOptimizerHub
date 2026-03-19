using System.Collections.ObjectModel;

namespace WinOptimizerHub.Models
{
    public class StartupKeyGroup
    {
        public string KeyPath { get; set; }
        public ObservableCollection<StartupItem> Items { get; } = new();
    }
}