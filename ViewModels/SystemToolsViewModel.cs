using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Services;

namespace WinOptimizerHub.ViewModels
{
    public class SystemToolsViewModel : BaseViewModel
    {
        private readonly SystemToolsService _svc;
        private readonly MainViewModel _main;

        public ObservableCollection<OutputLine> OutputLines { get; }
            = new ObservableCollection<OutputLine>();

        private readonly System.Text.StringBuilder _outputText = new System.Text.StringBuilder();
        public string OutputTextForCopy => _outputText.ToString();

        private bool _toolsEnabled = true;
        public bool ToolsEnabled
        {
            get => _toolsEnabled;
            private set => SetProperty(ref _toolsEnabled, value);
        }

        private bool _canAbort;
        public bool CanAbort
        {
            get => _canAbort;
            private set => SetProperty(ref _canAbort, value);
        }

        public ICommand RunSfcCommand { get; }
        public ICommand RunDismCheckCommand { get; }
        public ICommand RunDismRestoreCommand { get; }
        public ICommand RunDefragCommand { get; }
        public ICommand CopyOutputCommand { get; }
        public ICommand AbortCommand { get; }
        public ICommand ClearOutputCommand { get; }

        public ICommand OpenTaskManagerCommand { get; }
        public ICommand OpenDeviceManagerCommand { get; }
        public ICommand OpenDiskMgmtCommand { get; }
        public ICommand OpenResourceMonitorCommand { get; }
        public ICommand OpenEventViewerCommand { get; }
        public ICommand OpenRegeditCommand { get; }
        public ICommand OpenGpeditCommand { get; }
        public ICommand OpenSysPropertiesCommand { get; }

        private CancellationTokenSource _toolCts;

        public SystemToolsViewModel(ObservableCollection<string> log,
                                    SystemToolsService svc, MainViewModel main) : base(log)
        {
            _svc = svc;
            _main = main;

            RunSfcCommand = new AsyncRelayCommand(RunSfcAsync, () => ToolsEnabled);
            RunDismCheckCommand = new AsyncRelayCommand(RunDismCheckAsync, () => ToolsEnabled);
            RunDismRestoreCommand = new AsyncRelayCommand(RunDismRestoreAsync, () => ToolsEnabled);
            RunDefragCommand = new AsyncRelayCommand(RunDefragAsync, () => ToolsEnabled);
            CopyOutputCommand = new RelayCommand(CopyOutput);
            ClearOutputCommand = new RelayCommand(ClearOutput);
            AbortCommand = new RelayCommand(Abort, () => CanAbort);

            OpenTaskManagerCommand = new RelayCommand(() => _svc.OpenTaskManager());
            OpenDeviceManagerCommand = new RelayCommand(() => _svc.OpenDeviceManager());
            OpenDiskMgmtCommand = new RelayCommand(() => _svc.OpenDiskManagement());
            OpenResourceMonitorCommand = new RelayCommand(() => _svc.OpenResourceMonitor());
            OpenEventViewerCommand = new RelayCommand(() => _svc.OpenEventViewer());
            OpenRegeditCommand = new RelayCommand(() => _svc.OpenRegedit());
            OpenGpeditCommand = new RelayCommand(() => _svc.OpenGroupPolicyEditor());
            OpenSysPropertiesCommand = new RelayCommand(() => _svc.OpenSystemProperties());
        }

        private static readonly System.Text.RegularExpressions.Regex _pctRx =
            new System.Text.RegularExpressions.Regex(
                @"\d{1,3}\s*%",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private IProgress<string> MakeToolProgress(string prefix = "")
        {
            return new Progress<string>(raw =>
            {
                if (raw == null) return;

                string line = System.Text.RegularExpressions.Regex
                    .Replace(raw, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "").Trim();
                if (string.IsNullOrEmpty(line)) return;

                if (!string.IsNullOrEmpty(prefix))
                    ReportStatus(line.Length > 90 ? line.Substring(0, 87) + "…" : line);

                bool isPct = _pctRx.IsMatch(line);

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    int last = OutputLines.Count - 1;
                    if (isPct && last >= 0 && OutputLines[last].IsPercent)
                    {
                        OutputLines[last] = new OutputLine { Text = line, IsPercent = true };

                        var text = _outputText.ToString();
                        int lastNl = text.LastIndexOf('\n',
                            text.Length > 0 ? text.Length - 1 : 0);
                        if (lastNl >= 0)
                        {
                            _outputText.Remove(lastNl + 1, _outputText.Length - lastNl - 1);
                            _outputText.AppendLine(line);
                        }
                    }
                    else
                    {
                        OutputLines.Add(new OutputLine { Text = line, IsPercent = isPct });
                        _outputText.AppendLine(line);
                    }
                });
            });
        }

        public async Task RunSfcAsync()
        {
            if (!BeginTool("── SFC /scannow ──────────────────────────────────")) return;
            try
            {
                await _svc.RunSfcAsync(MakeToolProgress("SFC"), _toolCts.Token);
                Log("SFC scan complete");
                AppendDone();
                _main.Toast.ShowSuccess("SFC", "System File Checker completed");
            }
            catch (OperationCanceledException) { AppendLine("── Aborted by user ──"); }
            catch (Exception ex) { HandleError(ex, nameof(RunSfcAsync)); AppendLine($"Error: {ex.Message}"); }
            finally { EndTool("SFC complete"); }
        }

        private async Task RunDismCheckAsync()
        {
            if (!BeginTool("── DISM /CheckHealth ─────────────────────────────")) return;
            try
            {
                await _svc.RunDismHealthCheckAsync(MakeToolProgress("DISM"), _toolCts.Token);
                Log("DISM health check complete");
                AppendDone();
                _main.Toast.ShowSuccess("DISM", "Health check completed");
            }
            catch (OperationCanceledException) { AppendLine("── Aborted by user ──"); }
            catch (Exception ex) { HandleError(ex, nameof(RunDismCheckAsync)); AppendLine($"Error: {ex.Message}"); }
            finally { EndTool("DISM check done"); }
        }

        private async Task RunDismRestoreAsync()
        {
            if (!DialogService.ConfirmWarning(
                    "DISM RestoreHealth",
                    "DISM RestoreHealth requires internet access and may take 15-30 minutes. Continue?",
                    "Continue", "Cancel")) return;

            if (!BeginTool("── DISM /RestoreHealth ───────────────────────────")) return;
            try
            {
                await _svc.RunDismRestoreHealthAsync(MakeToolProgress("DISM"), _toolCts.Token);
                Log("DISM RestoreHealth complete");
                AppendDone();
                _main.Toast.ShowSuccess("DISM", "RestoreHealth completed");
            }
            catch (OperationCanceledException) { AppendLine("── Aborted by user ──"); }
            catch (Exception ex) { HandleError(ex, nameof(RunDismRestoreAsync)); AppendLine($"Error: {ex.Message}"); }
            finally { EndTool("DISM restore done"); }
        }

        private async Task RunDefragAsync()
        {
            if (!BeginTool("── Defrag / Optimize C: ──────────────────────────")) return;
            try
            {
                await _svc.RunDefragAsync("C:", MakeToolProgress("Defrag"), _toolCts.Token);
                Log("Defrag complete");
                AppendDone();
                _main.Toast.ShowSuccess("Defrag", "Drive optimization completed");
            }
            catch (OperationCanceledException) { AppendLine("── Aborted by user ──"); }
            catch (Exception ex) { HandleError(ex, nameof(RunDefragAsync)); AppendLine($"Error: {ex.Message}"); }
            finally { EndTool("Defrag done"); }
        }

        private bool BeginTool(string header)
        {
            if (!ToolsEnabled) return false;

            _toolCts?.Dispose();
            _toolCts = new CancellationTokenSource();

            ClearOutput();
            AppendLine(header);

            ToolsEnabled = false;
            CanAbort = true;
            SetBusy(true, "Running...");
            InvalidateCommands();
            return true;
        }

        private void EndTool(string status)
        {
            ToolsEnabled = false;
            CanAbort = false;
            _toolCts?.Dispose();
            _toolCts = null;
            SetBusy(false, status);

            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                ToolsEnabled = true;
                InvalidateCommands();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void AppendLine(string text)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                OutputLines.Add(new OutputLine { Text = text });
                _outputText.AppendLine(text);
            });
        }

        private void AppendDone() => AppendLine("\n── Done ──");

        private void Abort()
        {
            _toolCts?.Cancel();
            CanAbort = false;
            AppendLine("── Aborting... ──");
        }

        private void ClearOutput()
        {
            OutputLines.Clear();
            _outputText.Clear();
        }

        private void CopyOutput()
        {
            string text = _outputText.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                Clipboard.SetText(text);
                _main.Toast.ShowInfo("Copied", "Output copied to clipboard");
            }
        }
    }
}