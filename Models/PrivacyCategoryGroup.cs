using System.Collections.ObjectModel;
using WinOptimizerHub.Services;

namespace WinOptimizerHub.Models
{
    public class PrivacyCategoryGroup
    {
        public string Category { get; set; }
        public ObservableCollection<PrivacyCleanerService.PrivacyItem> Items { get; set; }
    }
}