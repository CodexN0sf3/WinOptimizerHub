using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;
using WinOptimizerHub.Services;

namespace WinOptimizerHub.ViewModels
{
    public class DiskViewModel : BaseViewModel
    {
        private readonly DiskAnalyzerService _svc;
        private readonly MainViewModel _main;

        private List<DiskInfo> _drives = new List<DiskInfo>();
        public List<DiskInfo> Drives
        {
            get => _drives;
            private set => SetProperty(ref _drives, value);
        }

        private List<FolderData> _folders = new List<FolderData>();
        public List<FolderData> Folders
        {
            get => _folders;
            private set { SetProperty(ref _folders, value); OnPropertyChanged(nameof(HasFolders)); }
        }

        public bool HasFolders => _folders.Any();

        private string _analyzePath = @"C:\";
        public string AnalyzePath
        {
            get => _analyzePath;
            set => SetProperty(ref _analyzePath, value);
        }

        private bool _isAnalyzing;
        public bool IsAnalyzing
        {
            get => _isAnalyzing;
            private set => SetProperty(ref _isAnalyzing, value);
        }

        public ICommand AnalyzeCommand { get; }
        public ICommand RefreshDrives { get; }

        public DiskViewModel(ObservableCollection<string> log, DiskAnalyzerService svc, MainViewModel main) : base(log)
        {
            _svc = svc;
            _main = main;
            AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync);
            RefreshDrives = new RelayCommand(LoadDrives);
        }

        public void LoadDrives()
        {
            try { Drives = _svc.GetAllDrives(); }
            catch (Exception ex) { AppLogger.Log(ex, nameof(LoadDrives)); }
        }

        private async Task AnalyzeAsync()
        {
            string path = AnalyzePath.Trim();
            if (!System.IO.Directory.Exists(path))
            {
                _main.Toast.ShowWarning("Disk Analyzer", "Path not found or not accessible.");
                return;
            }

            IsAnalyzing = true;
            SetBusy(true, $"Analyzing {path} — please wait...");
            Folders = new List<FolderData>();
            var ct = ResetCts();

            int scanned = 0;
            var progress = new Progress<string>(msg =>
            {
                scanned++;
                ReportStatus($"Analyzing... {scanned} folders scanned — {msg}");
            });

            try
            {
                var folders = await _svc.AnalyzeFolderAsync(path, 2, progress, ct);
                Folders = folders;
                Log($"Disk analysis complete: {folders.Count} top-level items in {path}");
                SetBusy(false, $"Analysis complete — {folders.Count} items found in {path}");
            }
            catch (OperationCanceledException) { SetBusy(false, "Analysis cancelled"); }
            catch (Exception ex) { AppLogger.Log(ex, nameof(AnalyzeAsync)); SetBusy(false, "Error during analysis"); }
            finally { IsAnalyzing = false; }
        }
    }
}