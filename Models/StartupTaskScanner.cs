using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;

namespace WinOptimizerHub.Services.Startup
{
    internal static class StartupTaskScanner
    {
        internal static void Scan(List<StartupItem> items)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/Query /FO CSV /V /NH",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    // Force English output so CSV column positions are predictable
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                };
                using var proc = Process.Start(psi);
                if (proc == null) return;

                // Read output with a hard 5-second timeout.
                // schtasks on a clean system finishes in <1s; 5s is generous.
                var readTask = proc.StandardOutput.ReadToEndAsync();
                bool finished = readTask.Wait(5000);
                if (!finished) { try { proc.Kill(); } catch { } return; }
                proc.WaitForExit(1000);
                string csv = readTask.Result ?? "";

                foreach (var line in csv.Split('\n'))
                {
                    try
                    {
                        var parts = ParseCsvLine(line);
                        if (parts.Length < 18) continue;

                        string taskPath = parts[1].Trim().Trim('"');
                        string schedType = parts[17].Trim().Trim('"');
                        string taskToRun = parts[8].Trim().Trim('"');
                        string status = parts[3].Trim().Trim('"');

                        if (string.IsNullOrWhiteSpace(taskPath)) continue;

                        bool isLogon = schedType.IndexOf("At logon", StringComparison.OrdinalIgnoreCase) >= 0
                                    || schedType.IndexOf("At startup", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!isLogon) continue;

                        string exePath = StartupRegistryScanner.ExtractExePath(taskToRun);
                        var (pub, desc) = StartupRegistryScanner.GetFileInfo(exePath);

                        items.Add(new StartupItem
                        {
                            Name = Path.GetFileName(taskPath.TrimEnd('\\')),
                            FileName = Path.GetFileName(exePath),
                            Command = exePath,
                            Description = string.IsNullOrEmpty(desc)
                                                ? taskToRun : desc,
                            Publisher = pub,
                            Source = "TaskScheduler",
                            RegistryKey = taskPath,
                            RegistryKeyPath = "Task Scheduler",
                            Category = "Scheduled Tasks",
                            IsEnabled = status != "Disabled",
                            ImpactLevel = "Medium",
                        });
                    }
                    catch (Exception ex) { AppLogger.Log(ex, "StartupTaskScanner.Line"); }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, "StartupTaskScanner"); }
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