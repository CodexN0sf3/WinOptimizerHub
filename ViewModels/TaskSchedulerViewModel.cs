using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;
using WinOptimizerHub.Services;

namespace WinOptimizerHub.ViewModels
{
    public class TaskSchedulerViewModel : BaseViewModel
    {
        private readonly TaskSchedulerService _svc;
        private readonly MainViewModel _main;

        private List<ScheduledTaskInfo> _items = new List<ScheduledTaskInfo>();
        public List<ScheduledTaskInfo> Items
        {
            get => _items;
            private set => SetProperty(ref _items, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand ToggleCommand { get; }

        public TaskSchedulerViewModel(ObservableCollection<string> log, TaskSchedulerService svc, MainViewModel main) : base(log)
        {
            _svc = svc;
            _main = main;
            RefreshCommand = new AsyncRelayCommand(LoadAsync);
            ToggleCommand = new AsyncRelayCommand(ToggleTaskAsync);
        }

        public async Task LoadAsync()
        {
            SetBusy(true, "Loading scheduled tasks...");
            try
            {
                var all = await _svc.GetTasksAsync();

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var dedup = new List<ScheduledTaskInfo>();
                foreach (var t in all)
                    if (seen.Add(t.Path))
                        dedup.Add(t);

                Items = dedup;
                SetBusy(false, $"{dedup.Count} scheduled tasks — refreshed at {DateTime.Now:HH:mm}");
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(LoadAsync)); SetBusy(false, "Error"); }
        }

        private async Task ToggleTaskAsync(object parameter)
        {
            if (parameter is not ScheduledTaskInfo task) return;
            bool enable = !task.IsEnabled;
            try
            {
                bool ok = await _svc.EnableDisableTaskAsync(task.Path, enable);
                Log($"Task '{task.Name}' {(enable ? "enabled" : "disabled")}: {(ok ? "OK" : "Failed")}");
                await LoadAsync();
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(ToggleTaskAsync)); }
        }
    }
}