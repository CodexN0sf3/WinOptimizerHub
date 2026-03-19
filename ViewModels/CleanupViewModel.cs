using System;
using System.Collections.Generic;
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
    public class CleanupViewModel : BaseViewModel
    {
        private readonly JunkCleanerService _svc;
        private readonly MainViewModel _main;

        private List<CleanableFolder> _folders = new List<CleanableFolder>();
        public List<CleanableFolder> Folders
        {
            get => _folders;
            private set => SetProperty(ref _folders, value);
        }

        private string _totalSize = "0 B";
        public string TotalSize
        {
            get => _totalSize;
            private set => SetProperty(ref _totalSize, value);
        }

        private string _fileCount = "0";
        public string FileCount
        {
            get => _fileCount;
            private set => SetProperty(ref _fileCount, value);
        }

        private string _categoryCount = "0";
        public string CategoryCount
        {
            get => _categoryCount;
            private set => SetProperty(ref _categoryCount, value);
        }

        private bool _hasFolders;
        public bool HasFolders
        {
            get => _hasFolders;
            private set => SetProperty(ref _hasFolders, value);
        }

        private CleaningMode _mode = CleaningMode.Safe;
        public CleaningMode CurrentMode
        {
            get => _mode;
            set
            {
                SetProperty(ref _mode, value);
                OnPropertyChanged(nameof(ModeDescription));
            }
        }

        public string ModeDescription => CurrentMode switch
        {
            CleaningMode.Safe => "Safe — temp files, caches, crash reports. Zero risk.",
            CleaningMode.Normal => "Normal — adds Prefetch, logs, dev/app caches. Minor first-launch slowdown.",
            CleaningMode.Aggressive => "Aggressive — adds Windows.old, upgrade leftovers. Up to 30 GB extra.",
            _ => ""
        };

        public ICommand ScanCommand { get; }
        public ICommand CleanCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand DeselectAllCommand { get; }

        public CleanupViewModel(ObservableCollection<string> log, JunkCleanerService svc, MainViewModel main)
            : base(log)
        {
            _svc = svc;
            _main = main;
            ScanCommand = new AsyncRelayCommand(ScanAsync);
            CleanCommand = new AsyncRelayCommand(CleanAsync, () => HasFolders);
            SelectAllCommand = new RelayCommand(SelectAll, () => HasFolders);
            DeselectAllCommand = new RelayCommand(DeselectAll, () => HasFolders);
        }

        public async Task ScanAsync()
        {
            SetBusy(true, "Scanning for junk files...");
            var ct = ResetCts();
            try
            {
                var results = await _svc.ScanAsync(CurrentMode, MakeProgress(), ct);
                Folders = results;
                HasFolders = results.Any();
                InvalidateCommands();

                long totalBytes = results.Sum(r => r.Size);
                int totalFiles = results.Sum(r => r.FileCount);

                TotalSize = FormatHelper.FormatSize(totalBytes);
                FileCount = totalFiles.ToString("N0");
                CategoryCount = results.Count.ToString();

                Log($"Scan complete: {results.Count} categories, {FormatHelper.FormatSize(totalBytes)} found");
            }
            catch (OperationCanceledException) { StatusMessage = "Scan cancelled"; }
            catch (Exception ex) { AppLogger.Log(ex, nameof(ScanAsync)); }
            finally { SetBusy(false, "Scan complete"); }
        }

        public async Task<long> CleanAndGetFreedAsync()
        {
            var selected = Folders.Where(f => f.IsSelected).ToList();
            if (!selected.Any()) return 0;
            SetBusy(true, "One-Click: cleaning junk...");
            var ct = ResetCts();
            try
            {
                var (freed, deleted) = await _svc.CleanAsync(selected, MakeProgress(), ct);
                _main.SessionFreedBytes += freed;
                Log($"One-Click Junk: {FormatHelper.FormatSize(freed)} freed, {deleted:N0} files");
                await ScanAsync();
                return freed;
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(CleanAndGetFreedAsync)); return 0; }
            finally { SetBusy(false, "Junk clean complete"); }
        }

        private async Task CleanAsync()
        {
            var selected = Folders.Where(f => f.IsSelected).ToList();
            if (!selected.Any())
            {
                _main.Toast.ShowWarning("Junk Cleaner", "No items selected.");
                return;
            }

            if (!DialogService.ConfirmDanger(
                    "Confirm Clean",
                    $"Clean {selected.Count} selected categories?\nThis will permanently delete the files.",
                    "Clean", "Cancel"))
                return;

            SetBusy(true, "Cleaning...");
            var ct = ResetCts();
            try
            {
                var (freed, deleted) = await _svc.CleanAsync(selected, MakeProgress(), ct);
                Log($"Cleaned: {FormatHelper.FormatSize(freed)} freed, {deleted:N0} files deleted");

                _main.SessionFreedBytes += freed;
                _main.Toast.ShowCleanComplete("Junk Cleaner", freed);

                await ScanAsync();
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(CleanAsync)); }
            finally { SetBusy(false, "Clean complete"); }
        }

        private void SelectAll()
        {
            foreach (var f in Folders) f.IsSelected = true;
        }

        private void DeselectAll()
        {
            foreach (var f in Folders) f.IsSelected = false;
        }
    }
}