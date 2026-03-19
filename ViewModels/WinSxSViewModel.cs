using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Services;

namespace WinOptimizerHub.ViewModels
{
    public class WinSxSViewModel : BaseViewModel
    {
        private readonly WindowsUpdateCleanupService _svc;
        private readonly MainViewModel _main;

        private ObservableCollection<OutputLine> _outputLines
            = new ObservableCollection<OutputLine>();
        public ObservableCollection<OutputLine> OutputLines
        {
            get => _outputLines;
            private set => SetProperty(ref _outputLines, value);
        }

        private string _outputText = "";
        public string OutputText
        {
            get => _outputText;
            private set => SetProperty(ref _outputText, value);
        }

        private string _sizeText = "Click 'Analyze' to check component store size";
        public string SizeText
        {
            get => _sizeText;
            private set => SetProperty(ref _sizeText, value);
        }

        private string _totalReclaimable = "";
        public string TotalReclaimable
        {
            get => _totalReclaimable;
            private set => SetProperty(ref _totalReclaimable, value);
        }

        private bool _hasTotal;
        public bool HasTotal
        {
            get => _hasTotal;
            private set => SetProperty(ref _hasTotal, value);
        }

        private Dictionary<string, long> _cleanupSizes = new Dictionary<string, long>();

        public CleanupCard UpdateCacheCard { get; } = new CleanupCard("Windows Update Cache");
        public CleanupCard DeliveryOptCard { get; } = new CleanupCard("Delivery Optimization");
        public CleanupCard UpgradeLogsCard { get; } = new CleanupCard("Windows Upgrade Logs");
        public CleanupCard WerCard { get; } = new CleanupCard("Windows Error Reports");
        public CleanupCard DxCacheCard { get; } = new CleanupCard("DirectX Shader Cache");
        public CleanupCard ThumbCacheCard { get; } = new CleanupCard("Thumbnail Cache");

        private IEnumerable<CleanupCard> AllCards =>
            new[] { UpdateCacheCard, DeliveryOptCard, UpgradeLogsCard, WerCard, DxCacheCard, ThumbCacheCard };

        private bool _isAnalyzing;
        public bool IsAnalyzing
        {
            get => _isAnalyzing;
            private set { SetProperty(ref _isAnalyzing, value); InvalidateCommands(); }
        }

        public ICommand AnalyzeCommand { get; }
        public ICommand CleanCommand { get; }
        public ICommand CleanUpdateCacheCommand { get; }
        public ICommand CleanDeliveryOptCommand { get; }
        public ICommand CleanUpgradeLogsCommand { get; }
        public ICommand CleanWerCommand { get; }
        public ICommand CleanDxCacheCommand { get; }
        public ICommand CleanThumbCacheCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ClearOutputCommand { get; }
        public ICommand CleanWindowsUpdateDismCommand { get; }

        public WinSxSViewModel(ObservableCollection<string> log,
                                WindowsUpdateCleanupService svc, MainViewModel main)
            : base(log)
        {
            _svc = svc;
            _main = main;

            AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => !IsBusy);
            CleanCommand = new AsyncRelayCommand(CleanAsync, () => !IsBusy);
            CleanUpdateCacheCommand = new AsyncRelayCommand(CleanUpdateCacheAsync, () => !IsBusy);
            CleanDeliveryOptCommand = new AsyncRelayCommand(CleanDeliveryOptAsync, () => !IsBusy);
            CleanUpgradeLogsCommand = new AsyncRelayCommand(CleanUpgradeLogsAsync, () => !IsBusy);
            CleanWerCommand = new AsyncRelayCommand(CleanWerAsync, () => !IsBusy);
            CleanDxCacheCommand = new AsyncRelayCommand(CleanDxCacheAsync, () => !IsBusy);
            CleanThumbCacheCommand = new AsyncRelayCommand(CleanThumbCacheAsync, () => !IsBusy);
            CancelCommand = new RelayCommand(CancelCurrent, () => IsBusy);
            ClearOutputCommand = new RelayCommand(ClearOutput);
            CleanWindowsUpdateDismCommand = new AsyncRelayCommand(CleanWindowsUpdateDismAsync, () => !IsBusy);
        }

        private async Task AnalyzeAsync()
        {
            IsAnalyzing = true;
            SetBusy(true, "Starting DISM analysis...");
            ClearOutput();
            AppendLine("Sending command to DISM — this may take a minute...", LineType.Info);
            var ct = ResetCts();

            var dismProgress = new Progress<string>(line =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                    AppendLine(line, ClassifyLine(line));
            });

            try
            {
                var dismTask = _svc.GetWinSxSSizeAsync(dismProgress, ct);
                var sizesTask = _svc.GetCleanupSizesAsync(ct);
                await Task.WhenAll(dismTask, sizesTask);

                var (size, _) = await dismTask;
                _cleanupSizes = await sizesTask;

                SizeText = size > 0
                    ? $"Component Store: {FormatHelper.FormatSize(size)}"
                    : "Component store size not available — run as Administrator";

                UpdateAllCards();
                UpdateTotal();

                AppendLine($"── Component Store: {FormatHelper.FormatSize(size)} ──", LineType.Success);
                SetBusy(false, $"Analysis complete — WinSxS: {FormatHelper.FormatSize(size)}");
                Log($"WinSxS analyzed: {FormatHelper.FormatSize(size)}");
            }
            catch (OperationCanceledException)
            {
                AppendLine("[Cancelled]", LineType.Warning);
                SetBusy(false, "Analysis cancelled");
            }
            catch (Exception ex)
            {
                AppendLine($"Error: {ex.Message}", LineType.Error);
                AppLogger.Log(ex, nameof(AnalyzeAsync));
                SetBusy(false, "Analysis failed");
            }
            finally { IsAnalyzing = false; }
        }

        private async Task CleanAsync()
        {
            if (!DialogService.ConfirmWarning(
                    "Confirm Full Cleanup",
                    "Run full cleanup? This includes:\n\n"
                  + "• DISM /StartComponentCleanup /ResetBase\n"
                  + "• Windows Update download cache\n"
                  + "• Delivery Optimization cache\n"
                  + "• Windows upgrade logs\n"
                  + "• Windows Error Reports\n"
                  + "• DirectX Shader Cache\n"
                  + "• Thumbnail cache\n\n"
                  + "⚠ DISM /ResetBase is irreversible — you will no longer be able\n"
                  + "to uninstall Windows updates after this step.\n\n"
                  + "This may take 5-15 minutes. Continue?",
                    "Continue", "Cancel")) return;

            SetBusy(true, "Full cleanup running...");
            ClearOutput();
            var ct = ResetCts();
            try
            {
                await _svc.CleanComponentStoreAsync(MakeColoredProgress(), ct);

                foreach (var card in AllCards) card.SetCleaned();
                await RefreshSizesAsync(ct);

                Log("WinSxS full cleanup completed.");
                _main.Toast.ShowSuccess("WinSxS Cleanup", "Full cleanup completed successfully");
            }
            catch (OperationCanceledException) { AppendLine("[Cancelled]", LineType.Warning); }
            catch (Exception ex)
            {
                AppendLine($"Error: {ex.Message}", LineType.Error);
                HandleError(ex, nameof(CleanAsync));
            }
            finally { SetBusy(false, "Cleanup complete"); }
        }

        private async Task CleanUpdateCacheAsync()
        {
            if (!Confirm("Clean Windows Update download cache?\n\nServices will be stopped temporarily.")) return;
            SetBusy(true, "Cleaning Windows Update cache...");
            var ct = ResetCts();
            try
            {
                var (freed, _) = await _svc.CleanUpdateCacheAsync(MakeColoredProgress(), ct);
                AppendLine($"Windows Update Cache: freed {FormatHelper.FormatSize(freed)}", LineType.Success);
                Log($"Update cache: {FormatHelper.FormatSize(freed)} freed");
                UpdateCacheCard.SetCleaned(freed);
                await RefreshSizesAsync(ct);
                _main.Toast.ShowSuccess("WinSxS", $"Update cache: {FormatHelper.FormatSize(freed)} freed");
            }
            catch (Exception ex) { HandleError(ex, nameof(CleanUpdateCacheAsync)); }
            finally { SetBusy(false, "Done"); }
        }

        private async Task CleanDeliveryOptAsync()
        {
            if (!Confirm("Clean Delivery Optimization cache?\n\nThe DoSvc service will be stopped temporarily.")) return;
            SetBusy(true, "Cleaning Delivery Optimization cache...");
            var ct = ResetCts();
            try
            {
                await _svc.CleanComponentStoreAsync(MakeColoredProgress(), ct, stepsToRun: new[] { 3 });
                DeliveryOptCard.SetCleaned();
                await RefreshSizesAsync(ct);
                _main.Toast.ShowSuccess("WinSxS", "Delivery Optimization cache cleaned");
            }
            catch (Exception ex) { HandleError(ex, nameof(CleanDeliveryOptAsync)); }
            finally { SetBusy(false, "Done"); }
        }

        private async Task CleanUpgradeLogsAsync()
        {
            if (!Confirm("Remove Windows upgrade logs?\n\nIncludes CBS logs, Panther logs and $Windows.~BT folders.")) return;
            SetBusy(true, "Removing upgrade logs...");
            var ct = ResetCts();
            try
            {
                await _svc.CleanComponentStoreAsync(MakeColoredProgress(), ct, stepsToRun: new[] { 4 });
                UpgradeLogsCard.SetCleaned();
                await RefreshSizesAsync(ct);
                _main.Toast.ShowSuccess("WinSxS", "Upgrade logs removed");
            }
            catch (Exception ex) { HandleError(ex, nameof(CleanUpgradeLogsAsync)); }
            finally { SetBusy(false, "Done"); }
        }

        private async Task CleanWerAsync()
        {
            if (!Confirm("Clean Windows Error Reports?")) return;
            SetBusy(true, "Cleaning Windows Error Reports...");
            var ct = ResetCts();
            try
            {
                await _svc.CleanComponentStoreAsync(MakeColoredProgress(), ct, stepsToRun: new[] { 5 });
                WerCard.SetCleaned();
                await RefreshSizesAsync(ct);
                _main.Toast.ShowSuccess("WinSxS", "Windows Error Reports cleaned");
            }
            catch (Exception ex) { HandleError(ex, nameof(CleanWerAsync)); }
            finally { SetBusy(false, "Done"); }
        }

        private async Task CleanDxCacheAsync()
        {
            if (!Confirm("Clean DirectX Shader Cache?")) return;
            SetBusy(true, "Cleaning DirectX Shader Cache...");
            var ct = ResetCts();
            try
            {
                await _svc.CleanComponentStoreAsync(MakeColoredProgress(), ct, stepsToRun: new[] { 6 });
                DxCacheCard.SetCleaned();
                await RefreshSizesAsync(ct);
                _main.Toast.ShowSuccess("WinSxS", "DirectX Shader Cache cleaned");
            }
            catch (Exception ex) { HandleError(ex, nameof(CleanDxCacheAsync)); }
            finally { SetBusy(false, "Done"); }
        }

        private async Task CleanThumbCacheAsync()
        {
            if (!Confirm("Clean Thumbnail Cache?\n\nThumbnails will be regenerated automatically on next folder access.")) return;
            SetBusy(true, "Cleaning Thumbnail Cache...");
            var ct = ResetCts();
            try
            {
                await _svc.CleanComponentStoreAsync(MakeColoredProgress(), ct, stepsToRun: new[] { 7 });
                ThumbCacheCard.SetCleaned();
                await RefreshSizesAsync(ct);
                _main.Toast.ShowSuccess("WinSxS", "Thumbnail Cache cleaned");
            }
            catch (Exception ex) { HandleError(ex, nameof(CleanThumbCacheAsync)); }
            finally { SetBusy(false, "Done"); }
        }

        private async Task CleanWindowsUpdateDismAsync()
        {
            if (!DialogService.ConfirmWarning(
                    "Windows Update DISM Cleanup",
                    "Clean Windows Update files using DISM?\n\n"
                  + "This will:\n"
                  + "  • Stop Windows Update services temporarily\n"
                  + "  • Delete SoftwareDistribution\\Download cache\n"
                  + "  • Run DISM /SPSuperseded\n"
                  + "  • Run DISM /StartComponentCleanup",
                    "Continue", "Cancel")) return;

            bool resetBase = DialogService.ConfirmDanger(
                    "Include /ResetBase?",
                    "Also run DISM /ResetBase?\n\nThis removes the ability to uninstall updates — irreversible.\n\nChoose Cancel for safe cleanup without /ResetBase.",
                    "Yes, include /ResetBase", "No, safe cleanup only");

            SetBusy(true, "Running DISM Windows Update cleanup...");
            ClearOutput();
            var ct = ResetCts();
            try
            {
                await _svc.RunWindowsUpdateDismCleanupAsync(resetBase, MakeColoredProgress(), ct);
                UpdateCacheCard.SetCleaned();
                await RefreshSizesAsync(ct);
                Log("Windows Update DISM cleanup completed.");
                _main.Toast.ShowSuccess("WinSxS", "Windows Update cleanup complete");
            }
            catch (OperationCanceledException) { AppendLine("[Cancelled]", LineType.Warning); }
            catch (Exception ex)
            {
                AppendLine($"Error: {ex.Message}", LineType.Error);
                HandleError(ex, nameof(CleanWindowsUpdateDismAsync));
            }
            finally { SetBusy(false, "Done"); }
        }

        private async Task RefreshSizesAsync(System.Threading.CancellationToken ct)
        {
            try
            {
                _cleanupSizes = await _svc.GetCleanupSizesAsync(ct);
                UpdateAllCards();
                UpdateTotal();
            }
            catch  { AppLogger.Log(new Exception("Unhandled"), nameof(AppLogger)); }
        }

        private void UpdateAllCards()
        {
            foreach (var card in AllCards)
            {
                _cleanupSizes.TryGetValue(card.Key, out long size);
                card.UpdateSize(size);
            }
        }

        private void UpdateTotal()
        {
            long total = _cleanupSizes.Values.Sum();
            HasTotal = total > 0;
            TotalReclaimable = total > 0
                ? $"Up to {FormatHelper.FormatSize(total)} reclaimable"
                : "";
        }

        private void ClearOutput()
        {
            Application.Current?.Dispatcher?.Invoke(() => OutputLines.Clear());
            OutputText = "";
        }

        private void AppendLine(string text, LineType type)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                OutputLines.Add(new OutputLine(text, type));
                OutputText = text;
                StatusMessage = text.Length > 100 ? text.Substring(0, 97) + "…" : text;
            });
        }

        private static LineType ClassifyLine(string line)
        {
            if (line == null) return LineType.Info;
            if (line.Contains("✔") || line.Contains("Success") ||
                line.IndexOf("complete", StringComparison.OrdinalIgnoreCase) >= 0)
                return LineType.Success;
            if (line.Contains("►") || (line.Contains("[") && line.Contains("/")))
                return LineType.Step;
            if (line.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("Failed", StringComparison.OrdinalIgnoreCase) >= 0)
                return LineType.Error;
            if (line.Contains("Warning") || line.Contains("⚠"))
                return LineType.Warning;
            return LineType.Info;
        }

        private IProgress<string> MakeColoredProgress()
            => new Progress<string>(s => AppendLine(s, ClassifyLine(s)));

        private static bool Confirm(string message)
            => DialogService.Confirm("Confirm", message);
    }

    public class CleanupCard : System.ComponentModel.INotifyPropertyChanged
    {
        public string Key { get; }

        private string _sizeText = "Run Analyze to see size";
        public string SizeText
        {
            get => _sizeText;
            private set { _sizeText = value; OnPropertyChanged(nameof(SizeText)); }
        }

        private bool _isCleaned;
        public bool IsCleaned
        {
            get => _isCleaned;
            private set { _isCleaned = value; OnPropertyChanged(nameof(IsCleaned)); }
        }

        public CleanupCard(string key) { Key = key; }

        public void UpdateSize(long bytes)
        {
            SizeText = bytes > 0 ? FormatHelper.FormatSize(bytes) : "0 B";
            if (bytes > 0) IsCleaned = false;
        }

        public void SetCleaned(long freed = 0)
        {
            IsCleaned = true;
            SizeText = freed > 0 ? $"Freed {FormatHelper.FormatSize(freed)}" : "Cleaned ✓";
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string n)
            => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(n));
    }
}