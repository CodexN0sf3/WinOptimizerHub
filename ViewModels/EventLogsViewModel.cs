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
    public class EventLogsViewModel : BaseViewModel
    {
        private readonly EventLogCleanerService _svc;
        private readonly MainViewModel _main;

        private List<EventLogInfo> _logs = new List<EventLogInfo>();
        public List<EventLogInfo> Logs
        {
            get => _logs;
            private set => SetProperty(ref _logs, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand ClearCommand { get; }

        public EventLogsViewModel(ObservableCollection<string> log, EventLogCleanerService svc, MainViewModel main) : base(log)
        {
            _svc = svc;
            _main = main;
            RefreshCommand = new AsyncRelayCommand(LoadAsync);
            ClearCommand = new AsyncRelayCommand(ClearSelectedAsync);
        }

        public async Task LoadAsync()
        {
            var unchecked_ = new System.Collections.Generic.HashSet<string>(
                _logs.Where(l => !l.IsSelected).Select(l => l.LogName),
                StringComparer.OrdinalIgnoreCase);

            SetBusy(true, "Loading event logs...");
            try
            {
                var logs = await _svc.GetEventLogsAsync();

                foreach (var log in logs)
                    if (unchecked_.Contains(log.LogName))
                        log.IsSelected = false;

                Logs = logs;
                long totalEntries = logs.Sum(l => l.Entries);
                SetBusy(false, $"{logs.Count} event logs — {totalEntries:N0} total entries — refreshed at {DateTime.Now:HH:mm}");
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(LoadAsync)); SetBusy(false, "Error"); }
        }

        private async Task ClearSelectedAsync()
        {
            var selected = Logs.Where(l => l.IsSelected).ToList();
            if (!selected.Any()) { _main.Toast.ShowWarning("Event Log Cleaner", "Select at least one log to clear."); return; }

            if (!DialogService.ConfirmDanger("Clear Event Logs",
                    $"Clear {selected.Count} event logs?",
                    "Clear", "Cancel")) return;

            SetBusy(true, "Clearing event logs...");
            try
            {
                var (cleared, failed) = await _svc.ClearLogsAsync(selected, MakeProgress());
                Log($"Event logs: {cleared} cleared, {failed} failed");
                _main.Toast.ShowInfo("Event Log Cleaner", $"Cleared: {cleared}  Failed: {failed}");
                await LoadAsync();
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(ClearSelectedAsync)); }
            finally { SetBusy(false, "Event logs cleared"); }
        }
    }
}