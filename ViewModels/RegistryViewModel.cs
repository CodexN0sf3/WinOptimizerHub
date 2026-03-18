using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;
using WinOptimizerHub.Services;

namespace WinOptimizerHub.ViewModels
{
    public class RegistryViewModel : BaseViewModel
    {
        private readonly RegistryCleanerService _svc;
        private readonly MainViewModel _main;

        private ObservableCollection<RegistryIssue> _issues = new ObservableCollection<RegistryIssue>();
        public ObservableCollection<RegistryIssue> Issues
        {
            get => _issues;
            private set
            {
                SetProperty(ref _issues, value);
                OnPropertyChanged(nameof(HasIssues));
                OnPropertyChanged(nameof(IssueCount));
                OnPropertyChanged(nameof(SummaryText));
                RebuildGroups();
            }
        }

        private ObservableCollection<RegistryCategoryGroup> _groupedIssues = new ObservableCollection<RegistryCategoryGroup>();
        public ObservableCollection<RegistryCategoryGroup> GroupedIssues
        {
            get => _groupedIssues;
            private set => SetProperty(ref _groupedIssues, value);
        }

        public bool HasIssues => _issues.Any();
        public string IssueCount => _issues.Any() ? $"{_issues.Count} issues found" : string.Empty;

        private RegistryIssue _selectedIssue;
        public RegistryIssue SelectedIssue
        {
            get => _selectedIssue;
            set => SetProperty(ref _selectedIssue, value);
        }
        public string SummaryText
        {
            get
            {
                int sel = _issues.Count(i => i.IsSelected);
                return sel == 0 ? "No items selected" : $"{sel} of {_issues.Count} selected";
            }
        }

        public bool DeepScan { get; set; } = false;
        public ICommand ScanCommand { get; }
        public ICommand CleanCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand SelectNoneCommand { get; }

        public RegistryViewModel(ObservableCollection<string> log, RegistryCleanerService svc, MainViewModel main) : base(log)
        {
            _svc = svc;
            _main = main;
            ScanCommand = new AsyncRelayCommand(ScanAsync);
            CleanCommand = new AsyncRelayCommand(CleanAsync, () => HasIssues);
            SelectAllCommand = new RelayCommand(SelectAll);
            SelectNoneCommand = new RelayCommand(SelectNone);
        }

        public async Task ScanAsync()
        {
            SetBusy(true, "Scanning registry...");
            var ct = ResetCts();
            try
            {
                var issues = await _svc.ScanAsync(DeepScan, MakeProgress(), ct);
                Issues = new ObservableCollection<RegistryIssue>(issues);
                InvalidateCommands();
                SetBusy(false, issues.Any()
                    ? $"Found {issues.Count} issues"
                    : "No issues found — registry is clean");
                Log($"Registry scan: {issues.Count} issues found");
            }
            catch (OperationCanceledException) { SetBusy(false, "Scan cancelled"); }
            catch (Exception ex) { AppLogger.Log(ex, nameof(ScanAsync)); SetBusy(false, "Error scanning"); }
        }

        private async Task CleanAsync()
        {
            var selected = _issues.Where(i => i.IsSelected).ToList();
            if (!selected.Any()) { _main.Toast.ShowWarning("Registry Cleaner", "No items selected."); return; }

            if (!DialogService.ConfirmWarning(
                    "Confirm Fix",
                    $"Fix {selected.Count} registry issues?\n\nA backup will be created automatically before any changes.",
                    "Fix", "Cancel")) return;

            SetBusy(true, "Creating backup and fixing registry...");
            var ct = ResetCts();
            try
            {
                var (fixed_, failed) = await _svc.CleanAsync(selected, MakeProgress(), ct);
                Log($"Registry: {fixed_} fixed, {failed} failed");

                if (RegistryCleanerService.LastBackupPath != null)
                    Log($"Backup: {System.IO.Path.GetFileName(RegistryCleanerService.LastBackupPath)}");

                _main.Toast.ShowInfo("Registry Cleaner",
                    $"{fixed_} issues fixed{(failed > 0 ? $", {failed} failed" : "")} — backup saved");

                SetBusy(false, $"Fixed {fixed_} issues");
                await ScanAsync();
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(CleanAsync)); SetBusy(false, "Error fixing registry"); }
        }

        public void SelectAll()
        {
            foreach (var i in _issues) i.IsSelected = true;
            OnPropertyChanged(nameof(SummaryText));
        }

        public void SelectNone()
        {
            foreach (var i in _issues) i.IsSelected = false;
            OnPropertyChanged(nameof(SummaryText));
        }

        private void RebuildGroups()
        {
            var col = new ObservableCollection<RegistryCategoryGroup>();
            foreach (var grp in _issues.GroupBy(i => i.IssueType).OrderBy(g => g.Key))
            {
                col.Add(new RegistryCategoryGroup
                {
                    IssueType = grp.Key,
                    Count = grp.Count(),
                    Issues = new ObservableCollection<RegistryIssue>(grp)
                });
            }
            GroupedIssues = col;
        }
    }
}