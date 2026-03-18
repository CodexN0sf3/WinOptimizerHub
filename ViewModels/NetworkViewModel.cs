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
    public class NetworkViewModel : BaseViewModel
    {
        private readonly NetworkOptimizerService _svc;
        private readonly MainViewModel _main;

        private List<NetworkAdapterInfo> _adapters = new List<NetworkAdapterInfo>();
        public List<NetworkAdapterInfo> Adapters
        {
            get => _adapters;
            private set => SetProperty(ref _adapters, value);
        }

        private string _outputText = "";
        public string OutputText
        {
            get => _outputText;
            private set => SetProperty(ref _outputText, value);
        }

        public ICommand FlushDnsCommand { get; }
        public ICommand ResetWinsockCommand { get; }
        public ICommand ResetTcpIpCommand { get; }
        public ICommand ReleaseRenewCommand { get; }
        public ICommand ResetFirewallCommand { get; }
        public ICommand RefreshAdaptersCommand { get; }

        public NetworkViewModel(ObservableCollection<string> log, NetworkOptimizerService svc, MainViewModel main) : base(log)
        {
            _svc = svc;
            _main = main;
            FlushDnsCommand = new AsyncRelayCommand(FlushDnsAsync);
            ResetWinsockCommand = new AsyncRelayCommand(ResetWinsockAsync);
            ResetTcpIpCommand = new AsyncRelayCommand(ResetTcpIpAsync);
            ReleaseRenewCommand = new AsyncRelayCommand(ReleaseRenewAsync);
            ResetFirewallCommand = new AsyncRelayCommand(ResetFirewallAsync);
            RefreshAdaptersCommand = new AsyncRelayCommand(LoadAdaptersAsync);
        }

        public async Task LoadAdaptersAsync()
        {
            try { Adapters = await _svc.GetAdaptersAsync(); }
            catch (Exception ex) { AppLogger.Log(ex, nameof(LoadAdaptersAsync)); }
        }

        private async Task FlushDnsAsync()
        {
            SetBusy(true, "Flushing DNS...");
            try
            {
                OutputText = await _svc.FlushDnsAsync(MakeProgress());
                Log("DNS cache flushed");
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(FlushDnsAsync)); }
            finally { SetBusy(false, "DNS flushed"); }
        }

        private async Task ResetWinsockAsync()
        {
            if (!DialogService.ConfirmWarning("Reset Winsock",
                    "Reset Winsock? A restart may be required.",
                    "Reset", "Cancel")) return;
            SetBusy(true, "Resetting Winsock...");
            try
            {
                OutputText = await _svc.ResetWinsockAsync(MakeProgress());
                Log("Winsock reset");
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(ResetWinsockAsync)); }
            finally { SetBusy(false, "Winsock reset"); }
        }

        private async Task ResetTcpIpAsync()
        {
            if (!DialogService.ConfirmWarning("Reset TCP/IP",
                    "Reset TCP/IP stack? A restart may be required.",
                    "Reset", "Cancel")) return;
            SetBusy(true, "Resetting TCP/IP...");
            try
            {
                OutputText = await _svc.ResetTcpIpAsync(MakeProgress());
                Log("TCP/IP stack reset");
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(ResetTcpIpAsync)); }
            finally { SetBusy(false, "TCP/IP reset"); }
        }

        private async Task ReleaseRenewAsync()
        {
            SetBusy(true, "Releasing/Renewing IP...");
            try
            {
                OutputText = await _svc.ReleaseRenewIpAsync(MakeProgress());
                Log("IP released and renewed");
                await LoadAdaptersAsync();
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(ReleaseRenewAsync)); }
            finally { SetBusy(false, "IP renewed"); }
        }

        private async Task ResetFirewallAsync()
        {
            if (!DialogService.ConfirmWarning("Reset Firewall",
                    "Reset Windows Firewall to defaults?",
                    "Reset", "Cancel")) return;
            SetBusy(true, "Resetting Firewall...");
            try
            {
                OutputText = await _svc.ResetFirewallAsync(MakeProgress());
                Log("Firewall reset to defaults");
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(ResetFirewallAsync)); }
            finally { SetBusy(false, "Firewall reset"); }
        }
    }
}