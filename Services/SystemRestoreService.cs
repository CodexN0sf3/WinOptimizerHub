using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;

namespace WinOptimizerHub.Services
{
    public class SystemRestoreService
    {
        [DllImport("srclient.dll", SetLastError = true)]
        private static extern uint SRRemoveRestorePoint(uint dwRPNum);

        public async Task<List<RestorePoint>> GetRestorePointsAsync()
        {
            return await Task.Run(() =>
            {
                var points = new List<RestorePoint>();
                try
                {
                    var scope = new ManagementScope(@"\\.\root\default");
                    scope.Connect();
                    using var searcher = new ManagementObjectSearcher(scope,
                        new ObjectQuery("SELECT * FROM SystemRestore"));
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        try
                        {
                            string rawDate = obj["CreationTime"]?.ToString();
                            DateTime dt = rawDate != null
                                ? ManagementDateTimeConverter.ToDateTime(rawDate)
                                : DateTime.MinValue;
                            points.Add(new RestorePoint
                            {
                                SequenceNumber = Convert.ToUInt32(obj["SequenceNumber"] ?? 0u),
                                Description = obj["Description"]?.ToString() ?? "",
                                CreationTime = dt,
                                RestorePointType = obj["RestorePointType"] != null
                                    ? Convert.ToUInt32(obj["RestorePointType"]).ToString()
                                    : ""
                            });
                        }
                        catch (Exception ex) { AppLogger.Log(ex); }
                    }
                }
                catch (Exception ex) { AppLogger.Log(ex); }
                return points.OrderByDescending(p => p.CreationTime).ToList();
            });
        }

        public async Task<(bool ok, string error)> CreateRestorePointAsync(string description)
        {
            return await Task.Run(() =>
            {
                try
                {
                    DisableRestorePointFrequencyLimit();

                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NonInteractive -NoProfile -Command \"Checkpoint-Computer -Description '" + description + "' -RestorePointType MODIFY_SETTINGS\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using var proc = System.Diagnostics.Process.Start(psi);
                    string stderr = proc?.StandardError.ReadToEnd() ?? "";
                    proc?.WaitForExit(60000);

                    if (proc?.ExitCode == 0)
                        return (true, null);

                    var scope = new ManagementScope(@"\.\root\default");
                    scope.Connect();
                    using var cls = new ManagementClass(scope,
                        new ManagementPath("SystemRestore"), null);
                    var inParams = cls.GetMethodParameters("CreateRestorePoint");
                    inParams["Description"] = description;
                    inParams["RestorePointType"] = 12u;
                    inParams["EventType"] = 100u;
                    var outParams = cls.InvokeMethod("CreateRestorePoint", inParams, null);
                    uint ret = Convert.ToUInt32(outParams?["returnValue"] ?? 1u);
                    if (ret == 0) return (true, null);

                    return (false, $"WMI returned error code {ret}. Ensure System Protection is enabled on drive C:");
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }
            });
        }

        private static void DisableRestorePointFrequencyLimit()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore");
                key?.SetValue("SystemRestorePointCreationFrequency", 0,
                    Microsoft.Win32.RegistryValueKind.DWord);
            }
            catch  { AppLogger.Log(new Exception("Unhandled"), nameof(AppLogger)); }
        }

        public async Task<(bool ok, string error)> DeleteRestorePointAsync(uint sequenceNumber)
        {
            return await Task.Run(() =>
            {
                try
                {
                    uint result = SRRemoveRestorePoint(sequenceNumber);
                    if (result == 0)
                        return (true, null);

                    return (false, $"SRRemoveRestorePoint returned error code: {result} (0x{result:X8})");
                }
                catch (Exception ex)
                {
                    return (false, $"{ex.GetType().Name}: {ex.Message}");
                }
            });
        }

    }
}