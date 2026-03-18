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
    public class RestorePointsViewModel : BaseViewModel
    {
        private readonly SystemRestoreService _svc;
        private readonly MainViewModel _main;

        private List<RestorePoint> _points = new List<RestorePoint>();
        public List<RestorePoint> Points
        {
            get => _points;
            private set => SetProperty(ref _points, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand CreateCommand { get; }
        public ICommand DeleteCommand { get; }

        public RestorePointsViewModel(ObservableCollection<string> log, SystemRestoreService svc, MainViewModel main) : base(log)
        {
            _svc = svc;
            _main = main;
            RefreshCommand = new AsyncRelayCommand(LoadAsync);
            CreateCommand = new AsyncRelayCommand(CreateAsync);
            DeleteCommand = new AsyncRelayCommand(DeleteSelectedAsync);
        }

        public async Task LoadAsync()
        {
            SetBusy(true, "Loading restore points...");
            try
            {
                Points = await _svc.GetRestorePointsAsync();
                string lastDate = Points.Any()
                    ? Points.Max(p => p.CreationTime).ToString("dd MMM yyyy HH:mm")
                    : "none";
                SetBusy(false,
                    $"{Points.Count} restore point(s) — most recent: {lastDate} — refreshed at {DateTime.Now:HH:mm}");
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(LoadAsync)); SetBusy(false, "Error"); }
        }

        private async Task CreateAsync()
        {
            SetBusy(true, "Creating restore point — this may take 30-60 seconds...");
            try
            {
                var (ok, error) = await _svc.CreateRestorePointAsync("WinOptimizerHub Manual Restore Point");
                if (ok)
                {
                    Log("Restore point created successfully");
                    _main.Toast.ShowInfo("Restore Points", "Restore point created successfully!");
                    await LoadAsync();
                }
                else
                {
                    string msg = string.IsNullOrEmpty(error)
                        ? "Failed to create restore point.\n\nEnsure:\n• Application runs as Administrator\n• System Protection is enabled on drive C:"
                        : $"Failed: {error}\n\nEnsure System Protection is enabled on drive C:";
                    Log($"Restore point failed: {error}");
                    _main.Toast.ShowError("Restore Points", "Failed to create restore point");
                    _main.Toast.ShowWarning("Restore Points", msg);
                    SetBusy(false, "Failed to create restore point");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, nameof(CreateAsync));
                _main.Toast.ShowError("Restore Points", $"Error: {ex.Message}");
                SetBusy(false, "Error");
            }
        }

        private async Task DeleteSelectedAsync()
        {
            var selected = Points.Where(p => p.IsSelected).ToList();
            if (!selected.Any())
            {
                _main.Toast.ShowWarning("Restore Points", "Select at least one restore point to delete.");
                return;
            }

            if (!DialogService.ConfirmDanger(
                    "Delete Restore Points",
                    $"Delete {selected.Count} restore point(s)? This action is irreversible.",
                    "Delete", "Cancel")) return;

            SetBusy(true, "Deleting restore points...");
            var errors = new List<string>();
            foreach (var pt in selected)
            {
                StatusMessage = $"Deleting #{pt.SequenceNumber}: {pt.Description}...";
                var (ok, error) = await _svc.DeleteRestorePointAsync(pt.SequenceNumber);
                if (!ok) errors.Add($"#{pt.SequenceNumber} '{pt.Description}': {error}");
                await Task.Delay(300);
            }

            await LoadAsync();

            int succeeded = selected.Count - errors.Count;
            if (errors.Count == 0)
            {
                Log($"Deleted {selected.Count} restore point(s)");
                _main.Toast.ShowInfo("Restore Points", $"{succeeded} restore point(s) deleted successfully");
            }
            else
            {
                _main.Toast.ShowWarning("Restore Points",
                    $"{succeeded} deleted, {errors.Count} failed");
                _main.Toast.ShowWarning("Restore Points",
                    $"{succeeded} deleted, {errors.Count} failed — check Activity Log for details");
            }
        }
    }
}