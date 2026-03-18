using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;

namespace WinOptimizerHub.Services
{
    public class ServiceOptimizerService
    {
        private static readonly Dictionary<string, (string rec, string cat, string reason)> ServiceAdvice = new()
        {
            // ── Telemetry & Diagnostics ───────────────────────────────────
            ["DiagTrack"] = ("Disabled", "Telemetry", "Windows telemetry — sends usage data to Microsoft"),
            ["dmwappushservice"] = ("Disabled", "Telemetry", "WAP push routing — used for telemetry and device management"),
            ["diagnosticshub.standardcollector.service"] = ("Manual", "Telemetry", "Diagnostics Hub collector — only needed for VS profiling"),
            ["WerSvc"] = ("Manual", "Telemetry", "Windows Error Reporting — safe to set Manual"),
            ["wercplsupport"] = ("Manual", "Telemetry", "Windows Error Reporting control panel support"),
            ["PcaSvc"] = ("Manual", "Telemetry", "Program Compatibility Assistant — safe to Manual"),
            ["DPS"] = ("Manual", "Telemetry", "Diagnostic Policy Service — safe to Manual"),

            // ── Xbox / Gaming ─────────────────────────────────────────────
            ["XblAuthManager"] = ("Manual", "Xbox", "Xbox Live authentication — only needed for Xbox games"),
            ["XblGameSave"] = ("Manual", "Xbox", "Xbox Game Save sync — only needed for Xbox games"),
            ["XboxGipSvc"] = ("Manual", "Xbox", "Xbox accessory management — only needed for Xbox controllers"),
            ["XboxNetApiSvc"] = ("Manual", "Xbox", "Xbox Live network API — only needed for Xbox online features"),
            ["GamingServices"] = ("Manual", "Xbox", "Xbox Gaming Services — needed only for Xbox app or Game Pass"),

            // ── Search & Indexing ────────────────────────────────────────
            ["WSearch"] = ("Manual", "Search", "Windows Search indexer — disable if you don't use Windows Search"),
            ["SharedAccess"] = ("Manual", "Search", "Internet Connection Sharing — disable if not sharing internet"),

            // ── Print ────────────────────────────────────────────────────
            ["Spooler"] = ("Manual", "Print", "Print spooler — set Manual if you have no printer"),
            ["PrintNotify"] = ("Manual", "Print", "Printer notification service — disable if no printer"),
            ["PrintWorkflowUserSvc"] = ("Manual", "Print", "Print workflow user service — only needed with printers"),

            // ── Remote Access ─────────────────────────────────────────────
            ["RemoteRegistry"] = ("Disabled", "Remote", "Remote registry access — security risk, disable unless needed"),
            ["TermService"] = ("Manual", "Remote", "Remote Desktop — set Manual if you never use RDP"),
            ["SessionEnv"] = ("Manual", "Remote", "Remote Desktop Config — set Manual if RDP not used"),
            ["UmRdpService"] = ("Manual", "Remote", "Remote Desktop Device Redirector — set Manual if RDP not used"),
            ["RemoteAccess"] = ("Disabled", "Remote", "Routing and Remote Access — disable unless VPN/router"),
            ["RasMan"] = ("Manual", "Remote", "Remote Access Connection Manager — Manual if no VPN"),

            // ── Maps & Location ───────────────────────────────────────────
            ["MapsBroker"] = ("Disabled", "Location", "Downloaded Maps Manager — rarely needed on desktop"),
            ["lfsvc"] = ("Manual", "Location", "Geolocation service — disable if location not needed"),
            ["WMPNetworkSvc"] = ("Manual", "Location", "Windows Media Player network sharing"),

            // ── Performance ───────────────────────────────────────────────
            ["SysMain"] = ("Manual", "Performance", "Superfetch/SysMain — disable on SSD for less disk writes"),
            ["defragsvc"] = ("Manual", "Performance", "Disk Defragmenter — Manual is fine; auto-schedule handles it"),
            ["BITS"] = ("Manual", "Performance", "Background Intelligent Transfer — set Manual to save bandwidth"),
            ["wuauserv"] = ("Manual", "Performance", "Windows Update — keep Manual; disable with care"),
            ["UsoSvc"] = ("Manual", "Performance", "Update Orchestrator — manages Windows Updates"),

            // ── Miscellaneous safe to disable ────────────────────────────
            ["Fax"] = ("Disabled", "Misc", "Fax service — not needed on modern PCs"),
            ["RetailDemo"] = ("Disabled", "Misc", "Retail Demo mode — not needed outside store environments"),
            ["wisvc"] = ("Disabled", "Misc", "Windows Insider Service — disable if not an Insider"),
            ["WbioSrvc"] = ("Manual", "Misc", "Windows Biometrics (fingerprint/face) — Manual if not used"),
            ["icssvc"] = ("Manual", "Misc", "Mobile Hotspot — Manual if you never use hotspot"),
            ["WpnService"] = ("Manual", "Misc", "Windows Push Notifications — Manual if not needed"),
            ["WpnUserService"] = ("Manual", "Misc", "Push notifications user service — Manual if not needed"),
            ["TabletInputService"] = ("Manual", "Misc", "Touch keyboard/handwriting — disable on desktop without touch"),
            ["TapiSrv"] = ("Manual", "Misc", "Telephony API — Manual if no modem/VoIP apps"),
            ["Telephony"] = ("Manual", "Misc", "Telephony — Manual if no dial-up/modem"),
            ["PhoneSvc"] = ("Manual", "Misc", "Phone Service — Manual if not using phone link"),
            ["stisvc"] = ("Manual", "Misc", "Windows Image Acquisition — Manual if no scanner/camera"),
            ["WiaRpc"] = ("Manual", "Misc", "Still Image devices — Manual if no scanner"),
            ["CDPSvc"] = ("Manual", "Misc", "Connected Devices Platform — Manual if no cross-device features"),
            ["cbdhsvc"] = ("Manual", "Misc", "Clipboard User Service (history) — Manual if not using cloud clipboard"),
            ["MessagingService"] = ("Manual", "Misc", "Messaging Service — Manual if not using SMS/messaging"),
            ["MixedRealityOpenXRSvc"] = ("Disabled", "Misc", "Mixed Reality OpenXR — disable if no VR headset"),
            ["perceptionsimulation"] = ("Disabled", "Misc", "Perception Simulation — only needed for HoloLens dev"),

            // ── Network ───────────────────────────────────────────────────
            ["LanmanServer"] = ("Manual", "Network", "File and Printer Sharing server — Manual if not sharing files"),
            ["FDResPub"] = ("Manual", "Network", "Function Discovery Resource Publication — Manual if not sharing"),
            ["SSDPSRV"] = ("Manual", "Network", "SSDP Discovery (UPnP) — Manual if no UPnP devices"),
            ["upnphost"] = ("Manual", "Network", "UPnP Device Host — Manual if no UPnP hosting"),
            ["p2psvc"] = ("Manual", "Network", "Peer Networking Grouping — Manual if no P2P apps"),
            ["PNRPsvc"] = ("Manual", "Network", "Peer Name Resolution — Manual if no P2P apps"),
        };

        public async Task<List<ServiceInfo>> GetServicesAsync(CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var result = new List<ServiceInfo>();
                try
                {
                    foreach (var sc in ServiceController.GetServices())
                    {
                        if (ct.IsCancellationRequested) break;
                        try
                        {
                            string startType = GetStartType(sc.ServiceName);
                            ServiceAdvice.TryGetValue(sc.ServiceName, out var advice);

                            result.Add(new ServiceInfo
                            {
                                ServiceName = sc.ServiceName,
                                DisplayName = sc.DisplayName,
                                Status = sc.Status.ToString(),
                                StartType = startType,
                                RecommendedStartType = advice.rec ?? startType,
                                Category = advice.cat ?? "Other",
                                Recommendation = advice.reason ?? string.Empty,
                                IsSafeToDisable = advice.rec != null && advice.rec != startType,
                                Description = GetServiceDescription(sc.ServiceName)
                            });
                        }
                        catch (Exception ex) { AppLogger.Log(ex); }
                        finally { sc.Dispose(); }
                    }
                }
                catch (Exception ex) { AppLogger.Log(ex); }
                return result.OrderBy(s => s.DisplayName).ToList();
            }, ct);
        }

        public async Task<bool> SetServiceStartTypeAsync(string serviceName, string startType)
        {
            return await Task.Run(() =>
            {
                try
                {
                    uint regValue = startType switch
                    {
                        "Automatic" => 2u,
                        "AutomaticDelayed" => 2u,
                        "Manual" => 3u,
                        "Disabled" or "Disable" => 4u,
                        _ => 3u
                    };

                    string keyPath = $@"SYSTEM\CurrentControlSet\Services\{serviceName}";
                    using var key = Registry.LocalMachine
                        .OpenSubKey(keyPath, writable: true);

                    if (key == null) return false;

                    key.SetValue("Start", (int)regValue,
                        RegistryValueKind.DWord);

                    if (startType == "AutomaticDelayed")
                        key.SetValue("DelayedAutostart", 1,
                            RegistryValueKind.DWord);
                    else
                        key.DeleteValue("DelayedAutostart", throwOnMissingValue: false);

                    return true;
                }
                catch { return false; }
            });
        }

        public async Task<bool> StartStopServiceAsync(string serviceName, bool start)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var sc = new ServiceController(serviceName);
                    if (start)
                    {
                        if (sc.Status != ServiceControllerStatus.Running) sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                    }
                    else
                    {
                        if (sc.Status == ServiceControllerStatus.Running) sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                    }
                    return true;
                }
                catch { return false; }
            });
        }

        private static string GetStartType(string serviceName)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Services\{serviceName}");
                int val = (int)(key?.GetValue("Start") ?? 3);
                return val switch { 2 => "Automatic", 3 => "Manual", 4 => "Disabled", _ => "Unknown" };
            }
            catch { return "Unknown"; }
        }

        private static string GetServiceDescription(string serviceName)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Services\{serviceName}");
                string desc = key?.GetValue("Description")?.ToString() ?? "";
                
                if (desc.StartsWith("@")) return string.Empty;
                return desc;
            }
            catch { return string.Empty; }
        }
    }
}