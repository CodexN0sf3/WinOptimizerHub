using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;

namespace WinOptimizerHub.Services
{
    public class NetworkOptimizerService
    {
        public async Task<string> FlushDnsAsync(IProgress<string> progress = null)
        {
            progress?.Report("Flushing DNS cache...");
            return await CommandHelper.RunAsync("ipconfig", "/flushdns");
        }

        public async Task<string> ResetWinsockAsync(IProgress<string> progress = null)
        {
            progress?.Report("Resetting Winsock catalog...");
            return await CommandHelper.RunAsync("netsh", "winsock reset");
        }

        public async Task<string> ResetTcpIpAsync(IProgress<string> progress = null)
        {
            progress?.Report("Resetting TCP/IP stack...");
            return await CommandHelper.RunAsync("netsh", "int ip reset");
        }

        public async Task<string> ReleaseRenewIpAsync(IProgress<string> progress = null)
        {
            progress?.Report("Releasing IP address...");
            await CommandHelper.RunAsync("ipconfig", "/release");
            progress?.Report("Renewing IP address...");
            return await CommandHelper.RunAsync("ipconfig", "/renew");
        }

        public async Task<string> ResetFirewallAsync(IProgress<string> progress = null)
        {
            progress?.Report("Resetting Windows Firewall...");
            return await CommandHelper.RunAsync("netsh", "advfirewall reset");
        }

        public async Task<List<NetworkAdapterInfo>> GetAdaptersAsync()
        {
            return await Task.Run(() =>
            {
                var adapters = new List<NetworkAdapterInfo>();
                try
                {
                    foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (nic.OperationalStatus != OperationalStatus.Up) continue;
                        if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                        if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                        try
                        {
                            var props = nic.GetIPProperties();

                            string ip = "";
                            string subnet = "";
                            foreach (var ua in props.UnicastAddresses)
                            {
                                if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                                {
                                    ip = ua.Address.ToString();
                                    subnet = ua.IPv4Mask?.ToString() ?? "";
                                    break;
                                }
                            }

                            string gateway = props.GatewayAddresses
                                .Select(g => g.Address)
                                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                                ?.ToString() ?? "";

                            string dns = string.Join(", ", props.DnsAddresses
                                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                                .Select(a => a.ToString()));

                            string mac = string.Join(":",
                                nic.GetPhysicalAddress().GetAddressBytes()
                                   .Select(b => b.ToString("X2")));

                            adapters.Add(new NetworkAdapterInfo
                            {
                                Name = nic.Name,
                                MacAddress = mac,
                                IpAddress = ip,
                                SubnetMask = subnet,
                                Gateway = gateway,
                                DnsServers = dns
                            });
                        }
                        catch (Exception ex) { AppLogger.Log(ex, "GetAdapters.Inner"); }
                    }
                }
                catch (Exception ex) { AppLogger.Log(ex, "GetAdapters.Outer"); }
                return adapters;
            });
        }

        public async Task<bool> SetDnsServersAsync(
            string adapterName, string primary, string secondary)
        {
            return await Task.Run(() =>
            {
                try
                {
                    CommandHelper.RunSync("netsh",
                        $"interface ip set dns \"{adapterName}\" static {primary}");
                    if (!string.IsNullOrEmpty(secondary))
                        CommandHelper.RunSync("netsh",
                            $"interface ip add dns \"{adapterName}\" {secondary} index=2");
                    return true;
                }
                catch (Exception ex) { AppLogger.Log(ex, nameof(SetDnsServersAsync)); return false; }
            });
        }
    }
}