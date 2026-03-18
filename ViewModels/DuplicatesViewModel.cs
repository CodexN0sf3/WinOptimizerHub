using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;
using WinOptimizerHub.Services;

namespace WinOptimizerHub.ViewModels
{
    public class DuplicatesViewModel : BaseViewModel
    {
        private readonly DuplicateFinderService _svc;
        private readonly MainViewModel _main;

        private List<List<DuplicateFile>> _groups = new List<List<DuplicateFile>>();
        public List<List<DuplicateFile>> Groups => _groups;

        private string _searchPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public string SearchPath
        {
            get => _searchPath;
            set => SetProperty(ref _searchPath, value);
        }

        private string _summaryText = "";
        public string SummaryText
        {
            get => _summaryText;
            private set => SetProperty(ref _summaryText, value);
        }

        private string _progressText = "";
        public string ProgressText
        {
            get => _progressText;
            private set => SetProperty(ref _progressText, value);
        }

        private bool _hasGroups;
        public bool HasGroups
        {
            get => _hasGroups;
            private set => SetProperty(ref _hasGroups, value);
        }

        private DuplicateFinderService.KeepStrategy _keepStrategy
            = DuplicateFinderService.KeepStrategy.KeepOldest;

        public DuplicateFinderService.KeepStrategy KeepStrategy
        {
            get => _keepStrategy;
            set
            {
                SetProperty(ref _keepStrategy, value);
                if (_groups.Any())
                {
                    DuplicateFinderService.ApplyKeepStrategy(_groups, _keepStrategy);
                    GroupsChanged?.Invoke();
                }
                OnPropertyChanged(nameof(StrategyKeepOldest));
                OnPropertyChanged(nameof(StrategyKeepNewest));
                OnPropertyChanged(nameof(StrategyKeepFirst));
            }
        }

        public bool StrategyKeepOldest
        {
            get => _keepStrategy == DuplicateFinderService.KeepStrategy.KeepOldest;
            set { if (value) KeepStrategy = DuplicateFinderService.KeepStrategy.KeepOldest; }
        }
        public bool StrategyKeepNewest
        {
            get => _keepStrategy == DuplicateFinderService.KeepStrategy.KeepNewest;
            set { if (value) KeepStrategy = DuplicateFinderService.KeepStrategy.KeepNewest; }
        }
        public bool StrategyKeepFirst
        {
            get => _keepStrategy == DuplicateFinderService.KeepStrategy.KeepFirst;
            set { if (value) KeepStrategy = DuplicateFinderService.KeepStrategy.KeepFirst; }
        }

        public ICommand ScanCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand BrowseCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action GroupsChanged;

        public DuplicatesViewModel(ObservableCollection<string> log, DuplicateFinderService svc, MainViewModel main) : base(log)
        {
            _svc = svc;
            _main = main;
            ScanCommand = new AsyncRelayCommand(ScanAsync);
            DeleteCommand = new AsyncRelayCommand(DeleteMarkedAsync, () => HasGroups);
            BrowseCommand = new RelayCommand(BrowsePath);
            CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        }

        private async Task ScanAsync()
        {
            string path = SearchPath.Trim();
            if (!Directory.Exists(path))
            {
                _main.Toast.ShowWarning("Duplicate Finder", "Path does not exist.");
                return;
            }

            bool isRisky = path.Length <= 3
                || path.Equals(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    StringComparison.OrdinalIgnoreCase);

            if (isRisky)
            {
                string msg = "Scanning a root drive or Windows folder may take a very long time "
                           + "and system/locked files will be skipped automatically.\n\n"
                           + "It is recommended to scan a specific folder like your Documents or Downloads.\n\n"
                           + "Continue anyway?";
                if (!DialogService.ConfirmWarning("Large Scan Warning", msg,
                        "Continue", "Cancel")) return;
            }

            SetBusy(true, "Scanning for duplicates...");
            HasGroups = false;
            _groups.Clear();
            GroupsChanged?.Invoke();
            var ct = ResetCts();

            var progress = new Progress<(int scanned, int hashing, string current)>(p =>
            {
                ProgressText = p.hashing > 0
                    ? $"Hashing {p.hashing} candidates — {p.current}"
                    : $"Scanned {p.scanned} files — {p.current}";
                StatusMessage = ProgressText;
            });

            try
            {
                var groups = await _svc.FindDuplicatesAsync(path, null, 4096, progress, ct);

                DuplicateFinderService.ApplyKeepStrategy(groups, _keepStrategy);

                _groups = groups;
                long waste = groups.Sum(g => g.Where(f => f.IsMarkedForDeletion).Sum(f => f.Size));
                int dupes = groups.Sum(g => g.Count - 1);

                SummaryText = groups.Any()
                    ? $"{groups.Count} duplicate groups — {dupes} files — {FormatHelper.FormatSize(waste)} reclaimable"
                    : "No duplicates found";

                HasGroups = groups.Any();
                InvalidateCommands();
                Log($"Duplicates: {groups.Count} groups, {FormatHelper.FormatSize(waste)} waste");
                GroupsChanged?.Invoke();
                SetBusy(false, SummaryText);
            }
            catch (OperationCanceledException)
            {
                SetBusy(false, "Scan cancelled");
                GroupsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, nameof(ScanAsync));
                SetBusy(false, "Error during scan");
            }
        }

        private async Task DeleteMarkedAsync()
        {
            var toDelete = _groups.SelectMany(g => g).Where(f => f.IsMarkedForDeletion).ToList();
            if (!toDelete.Any())
            {
                _main.Toast.ShowWarning("Duplicate Finder", "No files marked for deletion.");
                return;
            }

            long totalSize = toDelete.Sum(f => f.Size);
            string confirmMsg = $"Delete {toDelete.Count} files ({FormatHelper.FormatSize(totalSize)})?\n\n"
                              + "One copy of each group will be kept.\n"
                              + "Locked or in-use files will be silently skipped.";

            if (!DialogService.ConfirmDanger("Confirm Delete", confirmMsg,
                    "Delete", "Cancel")) return;

            SetBusy(true, "Deleting duplicates...");
            int deleted = 0, failed = 0;
            long freed = 0;

            await Task.Run(() =>
            {
                foreach (var file in toDelete)
                {
                    StatusMessage = $"Deleting: {file.FileName}";
                    try
                    {
                        if (File.Exists(file.FullPath))
                        {
                            File.Delete(file.FullPath);
                            freed += file.Size;
                            deleted++;
                        }
                    }
                    catch (UnauthorizedAccessException) { failed++; }
                    catch (IOException) { failed++; }
                    catch (Exception ex) { AppLogger.Log(ex, "DeleteDuplicates"); failed++; }
                }
            });

            foreach (var group in _groups)
                group.RemoveAll(f => f.IsMarkedForDeletion && !File.Exists(f.FullPath));
            _groups.RemoveAll(g => g.Count < 2);

            long remaining = _groups.Sum(g => g.Where(f => f.IsMarkedForDeletion).Sum(f => f.Size));
            SummaryText = _groups.Any()
                ? $"{_groups.Count} groups remaining — {FormatHelper.FormatSize(remaining)} reclaimable"
                : "All duplicates cleaned!";

            HasGroups = _groups.Any();
            GroupsChanged?.Invoke();

            Log($"Duplicates: {deleted} deleted, {failed} skipped, {FormatHelper.FormatSize(freed)} freed");
            SetBusy(false, $"Deleted {deleted} files — {FormatHelper.FormatSize(freed)} freed");

            if (failed > 0)
                _main.Toast.ShowWarning("Duplicate Finder",
                    $"Deleted: {deleted}  Skipped (locked): {failed}  Freed: {FormatHelper.FormatSize(freed)}");
        }

        private void BrowsePath()
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder to scan for duplicates",
                SelectedPath = SearchPath,
                ShowNewFolderButton = false
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                SearchPath = dlg.SelectedPath;
        }

        private void Cancel() => CancelCurrent();
    }
}