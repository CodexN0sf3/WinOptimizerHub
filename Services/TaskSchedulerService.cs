using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;

namespace WinOptimizerHub.Services
{
    public class TaskSchedulerService
    {
        public async Task<List<ScheduledTaskInfo>> GetTasksAsync(string folder = null, CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var tasks = new List<ScheduledTaskInfo>();
                try
                {
                    var result = CommandHelper.RunSync("schtasks.exe", "/Query /FO CSV /V /NH");

                    foreach (var line in result.Split('\n'))
                    {
                        if (ct.IsCancellationRequested) break;
                        var parts = ParseCsvLine(line);
                        if (parts.Length < 6) continue;

                        string taskPath = parts[1].Trim().Trim('"');
                        if (string.IsNullOrWhiteSpace(taskPath) || taskPath == "TaskName") continue;

                        try
                        {
                            string status = parts[3].Trim().Trim('"');
                            tasks.Add(new ScheduledTaskInfo
                            {
                                Path = taskPath,
                                Name = Path.GetFileName(taskPath.TrimEnd('\\')),
                                NextRunTime = parts[2].Trim().Trim('"'),
                                LastRunTime = parts[5].Trim().Trim('"'),
                                Status = status,
                                Author = parts.Length > 7 ? parts[7].Trim().Trim('"') : "",
                                IsEnabled = status != "Disabled"
                            });
                        }
                        catch (Exception ex) { AppLogger.Log(ex); }
                    }
                }
                catch (Exception ex) { AppLogger.Log(ex); }
                return tasks.OrderBy(t => t.Path).ToList();
            }, ct);
        }

        public async Task<bool> EnableDisableTaskAsync(string taskPath, bool enable)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!taskPath.StartsWith("\\"))
                        taskPath = "\\" + taskPath;

                    string flag = enable ? "/ENABLE" : "/DISABLE";
                    var psi = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Change /TN \"{taskPath}\" {flag}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(15000);
                    return p?.ExitCode == 0;
                }
                catch { return false; }
            });
        }

        private static string[] ParseCsvLine(string line)
        {
            var parts = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();
            foreach (char c in line)
            {
                if (c == '"') inQuotes = !inQuotes;
                else if (c == ',' && !inQuotes) { parts.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
            parts.Add(current.ToString());
            return parts.ToArray();
        }
    }
}