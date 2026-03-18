using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;

namespace WinOptimizerHub.Services
{
    public class EventLogCleanerService
    {
        public async Task<List<EventLogInfo>> GetEventLogsAsync(CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var logs = new List<EventLogInfo>();
                try
                {
                    foreach (EventLog log in EventLog.GetEventLogs())
                    {
                        if (ct.IsCancellationRequested) break;
                        try
                        {
                            logs.Add(new EventLogInfo
                            {
                                LogName = log.Log,
                                Entries = log.Entries.Count,
                                SizeBytes = EstimateLogSize(log),
                                LastWriteTime = GetLastWriteTime(log),
                                IsSelected = true
                            });
                        }
                        catch (Exception ex) { AppLogger.Log(ex, "GetEventLogs.Inner"); }
                        finally { log.Dispose(); }
                    }
                }
                catch (Exception ex) { AppLogger.Log(ex, "GetEventLogs.Outer"); }
                return logs.OrderByDescending(l => l.SizeBytes).ToList();
            }, ct);
        }

        public async Task<(int cleared, int failed)> ClearLogsAsync(
            IEnumerable<EventLogInfo> logs, IProgress<string> progress = null, CancellationToken ct = default)
        {
            int cleared = 0, failed = 0;
            await Task.Run(() =>
            {
                foreach (var logInfo in logs.Where(l => l.IsSelected))
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        progress?.Report($"Clearing {logInfo.LogName}...");
                        using var log = new EventLog(logInfo.LogName);
                        log.Clear();
                        cleared++;
                    }
                    catch (Exception ex) { AppLogger.Log(ex, $"ClearLogs.{logInfo.LogName}"); failed++; }
                }
            }, ct);
            return (cleared, failed);
        }

        private long EstimateLogSize(EventLog log)
        {
            try
            {
                string regPath = $@"SYSTEM\CurrentControlSet\Services\EventLog\{log.Log}";
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath);
                string file = key?.GetValue("File")?.ToString();

                if (!string.IsNullOrEmpty(file))
                {
                    file = Environment.ExpandEnvironmentVariables(file);

                    if (System.IO.File.Exists(file))
                    {
                        return new System.IO.FileInfo(file).Length;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, "EstimateLogSize.RegistryLookup");
            }

            try
            {
                return (long)log.Entries.Count * 1500;
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, "EstimateLogSize.Fallback");
                return 0;
            }
        }

        private DateTime GetLastWriteTime(EventLog log)
        {
            try
            {
                if (log.Entries.Count > 0)
                    return log.Entries[log.Entries.Count - 1].TimeGenerated;
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(GetLastWriteTime)); }
            return DateTime.MinValue;
        }
    }
}