using System.Collections.ObjectModel;

namespace WinOptimizerHub.Models
{
    public class RegistryCategoryGroup
    {
        public string IssueType { get; set; }
        public int Count { get; set; }
        public ObservableCollection<RegistryIssue> Issues { get; set; }
        public string Header => $"{IssueType}  ({Count})";
    }
}