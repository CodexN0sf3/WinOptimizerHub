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
    public class ServicesViewModel : BaseViewModel
    {
        private readonly ServiceOptimizerService _svc;
        private readonly MainViewModel _main;

        private List<ServiceInfo> _allServices = new List<ServiceInfo>();

        private ObservableCollection<ServiceInfo> _items = new ObservableCollection<ServiceInfo>();
        public ObservableCollection<ServiceInfo> Items
        {
            get => _items;
            private set { SetProperty(ref _items, value); OnPropertyChanged(nameof(SummaryText)); }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { SetProperty(ref _searchText, value); ApplyFilter(); }
        }

        private bool _showOnlyOptimizable;
        public bool ShowOnlyOptimizable
        {
            get => _showOnlyOptimizable;
            set { SetProperty(ref _showOnlyOptimizable, value); ApplyFilter(); }
        }

        public string SummaryText
        {
            get
            {
                int optimizable = _allServices.Count(s => s.NeedsOptimization);
                return optimizable > 0
                    ? $"{_allServices.Count} services — {optimizable} can be optimized"
                    : $"{_allServices.Count} services — all optimized";
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand ApplyRecommendationsCommand { get; }
        public ICommand StartStopCommand { get; }
        public ICommand SetStartTypeCommand { get; }
        public ICommand ApplyCommand { get; }

        public ServicesViewModel(ObservableCollection<string> log, ServiceOptimizerService svc, MainViewModel main) : base(log)
        {
            _svc = svc;
            _main = main;
            RefreshCommand = new AsyncRelayCommand(LoadAsync);
            ApplyRecommendationsCommand = new AsyncRelayCommand(ApplyRecommendationsAsync);
            StartStopCommand = new AsyncRelayCommand(StartStopServiceAsync);
            SetStartTypeCommand = new AsyncRelayCommand(SetStartTypeAsync);
            ApplyCommand = new AsyncRelayCommand(ApplyServiceAsync);
        }

        public async Task LoadAsync()
        {
            SetBusy(true, "Loading services...");
            try
            {
                _allServices = await _svc.GetServicesAsync();
                ApplyFilter();
                Log($"Services: {_allServices.Count} loaded");
                SetBusy(false, SummaryText);
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(LoadAsync)); SetBusy(false, "Error loading services"); }
        }

        private void ApplyFilter()
        {
            var filtered = _allServices.AsEnumerable();

            if (ShowOnlyOptimizable)
                filtered = filtered.Where(s => s.NeedsOptimization);

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string q = SearchText.Trim().ToLowerInvariant();
                filtered = filtered.Where(s =>
                    s.DisplayName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.ServiceName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.Description.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.Recommendation.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            Items = new ObservableCollection<ServiceInfo>(filtered);
            OnPropertyChanged(nameof(SummaryText));
        }

        private async Task ApplyServiceAsync(object parameter)
        {
            if (parameter is not ServiceInfo svc) return;
            bool ok = await _svc.SetServiceStartTypeAsync(svc.ServiceName, svc.RecommendedStartType);
            Log($"Service '{svc.DisplayName}' → {svc.RecommendedStartType}: {(ok ? "OK" : "Failed")}");
            if (!ok)
                _main.Toast.ShowError("Service Optimizer", $"Failed to change '{svc.DisplayName}'. Run as Administrator.");
            await LoadAsync();
        }

        private async Task ApplyRecommendationsAsync()
        {
            var optimizable = _allServices.Where(s => s.NeedsOptimization).ToList();
            if (!optimizable.Any())
            {
                _main.Toast.ShowInfo("Service Optimizer", "All recommended services are already optimal.");
                return;
            }

            if (!DialogService.ConfirmWarning(
                    "Apply Recommendations",
                    $"Apply recommendations to {optimizable.Count} services?\n\nThis will change their startup type as suggested.",
                    "Apply", "Cancel")) return;

            SetBusy(true, "Applying recommendations...");
            int applied = 0, failed = 0;
            try
            {
                foreach (var svc in optimizable)
                {
                    StatusMessage = $"Setting {svc.DisplayName} → {svc.RecommendedStartType}...";
                    if (await _svc.SetServiceStartTypeAsync(svc.ServiceName, svc.RecommendedStartType))
                        applied++;
                    else
                        failed++;
                }
                Log($"Services optimized: {applied}/{optimizable.Count}");
                await LoadAsync();
                _main.Toast.ShowInfo("Service Optimizer", $"Applied: {applied}  Failed: {failed}");
            }
            catch (Exception ex) { HandleError(ex, nameof(ApplyRecommendationsAsync)); }
            finally { SetBusy(false); }
        }

        private async Task StartStopServiceAsync(object parameter)
        {
            if (parameter is not ValueTuple<ServiceInfo, bool> t) return;
            var (svc, start) = t;
            SetBusy(true, $"{(start ? "Starting" : "Stopping")} {svc.DisplayName}...");
            try
            {
                bool ok = await _svc.StartStopServiceAsync(svc.ServiceName, start);
                Log($"Service '{svc.DisplayName}' {(start ? "started" : "stopped")}: {(ok ? "OK" : "Failed")}");
                if (!ok)
                    _main.Toast.ShowError("Service Optimizer", $"Failed to {(start ? "start" : "stop")} '{svc.DisplayName}'.");
                await LoadAsync();
            }
            catch (Exception ex) { HandleError(ex, nameof(StartStopServiceAsync)); }
            finally { SetBusy(false); }
        }

        private async Task SetStartTypeAsync(object parameter)
        {
            if (parameter is not ValueTuple<ServiceInfo, string> t) return;
            var (svc, startType) = t;
            try
            {
                bool ok = await _svc.SetServiceStartTypeAsync(svc.ServiceName, startType);
                Log($"Service '{svc.DisplayName}' → {startType}: {(ok ? "OK" : "Failed")}");
                if (!ok)
                    _main.Toast.ShowError("Service Optimizer", $"Failed to set '{svc.DisplayName}' to {startType}. Run as Administrator.");
                await LoadAsync();
            }
            catch (Exception ex) { HandleError(ex, nameof(SetStartTypeAsync)); }
        }
    }
}