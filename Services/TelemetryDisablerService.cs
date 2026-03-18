using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;

namespace WinOptimizerHub.Services
{
    public partial class TelemetryDisablerService
    {

        public async Task<List<TelemetryItem>> GetTelemetryStatusAsync()
        {
            return await Task.Run(() =>
            {
                var items = new List<TelemetryItem>();

                items.Add(Check("DiagTrack Service",
                    "Connected User Experiences and Telemetry — sends usage data to Microsoft",
                    "Services", IsServiceRunning("DiagTrack")));

                items.Add(Check("WAP Push Service",
                    "dmwappushservice — used for telemetry routing and device management",
                    "Services", IsServiceRunning("dmwappushservice")));

                items.Add(Check("AutoLogger-Diagtrack",
                    "Diagnostic tracking ETW session that starts at boot",
                    "Services", GetRegDword(@"SYSTEM\CurrentControlSet\Control\WMI\Autologger\AutoLogger-Diagtrack-Listener", "Start", Registry.LocalMachine) != 0));

                items.Add(Check("Telemetry Level",
                    "AllowTelemetry — set to 0 (Security) to send minimal data",
                    "Data Collection", GetRegDword(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry") != 0));

                items.Add(Check("Diagnostic Data Viewer",
                    "Allows viewing collected diagnostic data — disabling also reduces collection",
                    "Data Collection", GetRegDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Diagnostics\DiagTrack", "ShowedToastAtLevel") != 0 ||
                                       GetRegDword(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DisableEnterpriseAuthProxy") == 0));

                items.Add(Check("CEIP",
                    "Customer Experience Improvement Program — sends usage statistics",
                    "Data Collection", GetRegDword(@"SOFTWARE\Microsoft\SQMClient\Windows", "CEIPEnable") != 0));

                items.Add(Check("App Error Reporting",
                    "Windows Error Reporting — sends crash reports to Microsoft",
                    "Data Collection", GetRegDword(@"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled") == 0));

                items.Add(Check("Feedback Frequency",
                    "Windows asking for feedback — set to Never",
                    "Data Collection", GetRegDword(@"SOFTWARE\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod") != 0 ||
                                       GetRegDword(@"SOFTWARE\Microsoft\Siuf\Rules", "PeriodInNanoSeconds") != 0));

                items.Add(Check("Advertising ID",
                    "Unique ad ID used for targeted ads in apps",
                    "Privacy", GetRegDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled") != 0));

                items.Add(Check("Activity History",
                    "Windows Timeline — tracks app launches and file opens",
                    "Privacy", GetRegDword(@"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities") != 0 ||
                               GetRegDword(@"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed") != 0));

                items.Add(Check("App Launch Tracking",
                    "Tracks which apps you launch to improve Start and Search",
                    "Privacy", GetRegDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs") != 0));

                items.Add(Check("Inking & Typing",
                    "Sends typing and handwriting data to Microsoft for personalization",
                    "Privacy", GetRegDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CPSS\Store\InkingAndTypingPersonalization", "Value") != 0));

                items.Add(Check("Tailored Experiences",
                    "Uses diagnostic data to show personalized tips, ads and recommendations",
                    "Privacy", GetRegDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled") != 0));

                items.Add(Check("Suggested Content",
                    "Shows suggested apps and content in Settings",
                    "Privacy", GetRegDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338393Enabled") != 0 ||
                               GetRegDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353694Enabled") != 0));

                items.Add(Check("Start Menu Recommendations",
                    "Recommended apps and files in Start Menu (Windows 11)",
                    "Privacy", GetRegDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_IrisRecommendations") != 0));

                items.Add(Check("Tips & Tricks Notifications",
                    "Windows showing tips and suggestions on lock screen and desktop",
                    "Privacy", GetRegDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled") != 0 ||
                               GetRegDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled") != 0));

                items.Add(Check("Location Tracking",
                    "Windows location service — tracks device location",
                    "Location", GetRegDword(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Sensor\Overrides\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}", "SensorPermissionState", Registry.LocalMachine) != 0));

                items.Add(Check("Find My Device",
                    "Allows Microsoft to locate this device remotely",
                    "Location", GetRegDword(@"SOFTWARE\Policies\Microsoft\FindMyDevice", "AllowFindMyDevice") != 0 ||
                                GetRegDword(@"SOFTWARE\Microsoft\MdmCommon\SettingValues", "LocationSyncEnabled") != 0));

                items.Add(Check("Cortana",
                    "Cortana assistant — collects and uploads search history",
                    "Search", GetRegDword(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana") != 0));

                items.Add(Check("Web Search in Start",
                    "Sends Start Menu search queries to Bing",
                    "Search", GetRegDword(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "DisableWebSearch") == 0));

                items.Add(Check("Search Highlights",
                    "Shows trending news and content in Windows Search",
                    "Search", GetRegDword(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "EnableDynamicContentInWSB") != 0));

                items.Add(Check("Consumer Features",
                    "Spotlight, app suggestions, third-party app promotions on Start",
                    "Consumer", GetRegDword(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures") == 0));

                items.Add(Check("Cloud Content",
                    "Lock screen spotlight and cloud-based content suggestions",
                    "Consumer", GetRegDword(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableCloudOptimizedContent") == 0));

                items.Add(Check("Microsoft Account Ads",
                    "Promotions shown after signing in with Microsoft account",
                    "Consumer", GetRegDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled") != 0));

                return items;
            });
        }

        public async Task<(int applied, int failed)> ApplyAllAsync(
            IEnumerable<TelemetryItem> items, IProgress<string> progress = null,
            CancellationToken ct = default)
        {
            int applied = 0, failed = 0;
            await Task.Run(() =>
            {
                foreach (var item in items)
                {
                    if (ct.IsCancellationRequested) break;
                    if (!item.IsSelected) continue;
                    progress?.Report($"Disabling: {item.Name}...");
                    try { ApplyFix(item, disable: true); applied++; item.IsApplied = true; }
                    catch (Exception ex) { AppLogger.Log(ex, $"Telemetry.Apply:{item.Name}"); failed++; }
                }
            }, ct);
            return (applied, failed);
        }

        public async Task<(int applied, int failed)> RevertAllAsync(
            IEnumerable<TelemetryItem> items, IProgress<string> progress = null,
            CancellationToken ct = default)
        {
            int applied = 0, failed = 0;
            await Task.Run(() =>
            {
                foreach (var item in items)
                {
                    if (ct.IsCancellationRequested) break;
                    progress?.Report($"Restoring: {item.Name}...");
                    try { ApplyFix(item, disable: false); applied++; }
                    catch (Exception ex) { AppLogger.Log(ex, $"Telemetry.Revert:{item.Name}"); failed++; }
                }
            }, ct);
            return (applied, failed);
        }

        public async Task ApplyItemAsync(TelemetryItem item, bool disable)
            => await Task.Run(() => ApplyFix(item, disable));

        private void ApplyFix(TelemetryItem item, bool disable)
        {
            switch (item.Name)
            {
                case "DiagTrack Service":
                    if (disable) { StopAndDisable("DiagTrack"); }
                    else { EnableAndStart("DiagTrack"); }
                    break;

                case "WAP Push Service":
                    if (disable) { StopAndDisable("dmwappushservice"); }
                    else { EnableAndStart("dmwappushservice"); }
                    break;

                case "AutoLogger-Diagtrack":
                    SetReg(@"SYSTEM\CurrentControlSet\Control\WMI\Autologger\AutoLogger-Diagtrack-Listener",
                        "Start", disable ? 0 : 1, Registry.LocalMachine);
                    break;

                case "Telemetry Level":
                    SetReg(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", disable ? 0 : 3);
                    SetReg(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", disable ? 0 : 3);
                    break;

                case "Diagnostic Data Viewer":
                    SetReg(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DisableEnterpriseAuthProxy", disable ? 1 : 0);
                    break;

                case "CEIP":
                    SetReg(@"SOFTWARE\Microsoft\SQMClient\Windows", "CEIPEnable", disable ? 0 : 1);
                    break;

                case "App Error Reporting":
                    SetReg(@"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", disable ? 1 : 0);
                    SetReg(@"SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting", "Disabled", disable ? 1 : 0);
                    break;

                case "Feedback Frequency":
                    SetReg(@"SOFTWARE\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", disable ? 0 : 1);
                    SetReg(@"SOFTWARE\Microsoft\Siuf\Rules", "PeriodInNanoSeconds", disable ? 0 : 0);
                    break;

                case "Advertising ID":
                    SetReg(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", disable ? 0 : 1, Registry.CurrentUser);
                    SetReg(@"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", disable ? 1 : 0);
                    break;

                case "Activity History":
                    SetReg(@"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", disable ? 0 : 1);
                    SetReg(@"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", disable ? 0 : 1);
                    SetReg(@"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", disable ? 0 : 1);
                    break;

                case "App Launch Tracking":
                    SetReg(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs", disable ? 0 : 1, Registry.CurrentUser);
                    break;

                case "Inking & Typing":
                    SetReg(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CPSS\Store\InkingAndTypingPersonalization", "Value", disable ? 0 : 1, Registry.CurrentUser);
                    SetReg(@"SOFTWARE\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", disable ? 1 : 0, Registry.CurrentUser);
                    SetReg(@"SOFTWARE\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", disable ? 1 : 0, Registry.CurrentUser);
                    break;

                case "Tailored Experiences":
                    SetReg(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", disable ? 0 : 1, Registry.CurrentUser);
                    SetReg(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableTailoredExperiencesWithDiagnosticData", disable ? 1 : 0);
                    break;

                case "Suggested Content":
                    SetReg(@"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338393Enabled", disable ? 0 : 1, Registry.CurrentUser);
                    SetReg(@"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353694Enabled", disable ? 0 : 1, Registry.CurrentUser);
                    SetReg(@"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353696Enabled", disable ? 0 : 1, Registry.CurrentUser);
                    break;

                case "Start Menu Recommendations":
                    SetReg(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_IrisRecommendations", disable ? 0 : 1, Registry.CurrentUser);
                    SetReg(@"SOFTWARE\Policies\Microsoft\Windows\Explorer", "HideRecentlyAddedApps", disable ? 1 : 0);
                    break;

                case "Tips & Tricks Notifications":
                    SetReg(@"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled", disable ? 0 : 1, Registry.CurrentUser);
                    SetReg(@"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", disable ? 0 : 1, Registry.CurrentUser);
                    SetReg(@"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "RotatingLockScreenEnabled", disable ? 0 : 1, Registry.CurrentUser);
                    break;

                case "Location Tracking":
                    SetReg(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Sensor\Overrides\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}",
                        "SensorPermissionState", disable ? 0 : 1, Registry.LocalMachine);
                    SetReg(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation", disable ? 1 : 0);
                    break;

                case "Find My Device":
                    SetReg(@"SOFTWARE\Policies\Microsoft\FindMyDevice", "AllowFindMyDevice", disable ? 0 : 1);
                    break;

                case "Cortana":
                    SetReg(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", disable ? 0 : 1);
                    SetReg(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Search", "CortanaEnabled", disable ? 0 : 1, Registry.CurrentUser);
                    break;

                case "Web Search in Start":
                    SetReg(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "DisableWebSearch", disable ? 1 : 0);
                    SetReg(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "ConnectedSearchUseWeb", disable ? 0 : 1);
                    break;

                case "Search Highlights":
                    SetReg(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "EnableDynamicContentInWSB", disable ? 0 : 1);
                    break;

                case "Consumer Features":
                    SetReg(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", disable ? 1 : 0);
                    break;

                case "Cloud Content":
                    SetReg(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableCloudOptimizedContent", disable ? 1 : 0);
                    SetReg(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsSpotlightFeatures", disable ? 1 : 0);
                    break;

                case "Microsoft Account Ads":
                    SetReg(@"SOFTWARE\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", disable ? 0 : 1, Registry.CurrentUser);
                    break;
            }
        }

        private static TelemetryItem Check(string name, string desc, string cat, bool enabled)
            => new TelemetryItem
            {
                Name = name,
                Description = desc,
                Category = cat,
                IsCurrentlyEnabled = enabled,
                IsSelected = true
            };

        private static bool IsServiceRunning(string name)
        {
            try
            {
                using var sc = new ServiceController(name);
                return sc.Status == ServiceControllerStatus.Running;
            }
            catch { return false; }
        }

        private static void StopAndDisable(string name)
        {
            try
            {
                using var sc = new ServiceController(name);
                if (sc.Status == ServiceControllerStatus.Running)
                    sc.Stop();
            }
            catch (Exception ex) { AppLogger.Log(ex, $"Telemetry.Stop:{name}"); }
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Services\{name}", writable: true);
                key?.SetValue("Start", 4, RegistryValueKind.DWord);
            }
            catch (Exception ex) { AppLogger.Log(ex, $"Telemetry.Disable:{name}"); }
        }

        private static void EnableAndStart(string name)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Services\{name}", writable: true);
                key?.SetValue("Start", 2, RegistryValueKind.DWord);
            }
            catch (Exception ex) { AppLogger.Log(ex, $"Telemetry.Enable:{name}"); }
            try
            {
                using var sc = new ServiceController(name);
                if (sc.Status != ServiceControllerStatus.Running) sc.Start();
            }
            catch (Exception ex) { AppLogger.Log(ex, $"Telemetry.Start:{name}"); }
        }

        private static int GetRegDword(string subKey, string valueName, RegistryKey hive = null)
        {
            hive ??= Registry.LocalMachine;
            try
            {
                using var key = hive.OpenSubKey(subKey);
                return key?.GetValue(valueName) is int v ? v : 0;
            }
            catch { return 0; }
        }

        private static void SetReg(string subKey, string valueName, int value, RegistryKey hive = null)
        {
            hive ??= Registry.LocalMachine;
            try
            {
                using var key = hive.CreateSubKey(subKey);
                key?.SetValue(valueName, value, RegistryValueKind.DWord);
            }
            catch (Exception ex) { AppLogger.Log(ex, $"Telemetry.SetReg:{valueName}"); }
        }
    }
}