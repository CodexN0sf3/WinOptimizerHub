using System;
using System.Collections.Generic;
using System.IO;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;

namespace WinOptimizerHub.Services.Startup
{
    internal static class StartupFolderScanner
    {
        internal static void Scan(List<StartupItem> items)
        {
            var folders = new[]
            {
                (Environment.GetFolderPath(Environment.SpecialFolder.Startup),       "Startup Folder"),
                (Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Startup Folder"),
            };

            foreach (var (folder, category) in folders)
            {
                if (!Directory.Exists(folder)) continue;

                foreach (var file in Directory.GetFiles(folder))
                {
                    try
                    {
                        string ext = Path.GetExtension(file).ToLowerInvariant();
                        if (ext == ".ini") continue;

                        bool isEnabled = !file.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                        string cleanFile = isEnabled ? file : file.Substring(0, file.Length - 9);

                        string exePath = StartupRegistryScanner.ExtractExePath(cleanFile);
                        string infoPath = File.Exists(exePath) ? exePath : cleanFile;
                        var (pub, desc) = StartupRegistryScanner.GetFileInfo(infoPath);

                        items.Add(new StartupItem
                        {
                            Name = Path.GetFileNameWithoutExtension(cleanFile),
                            FileName = Path.GetFileName(cleanFile),
                            Command = cleanFile,
                            Description = string.IsNullOrEmpty(desc) ? Path.GetFileName(cleanFile) : desc,
                            Publisher = pub,
                            Source = category,
                            RegistryKey = file,
                            RegistryKeyPath = folder,
                            Category = category,
                            IsEnabled = isEnabled,
                            ImpactLevel = "Medium"
                        });
                    }
                    catch (Exception ex) { AppLogger.Log(ex, "StartupFolderScanner"); }
                }
            }
        }

        internal static void Toggle(StartupItem item, bool enable)
        {
            if (enable && item.Command.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
            {
                string target = item.Command.Substring(0, item.Command.Length - 9);
                if (File.Exists(item.Command)) File.Move(item.Command, target);
            }
            else if (!enable && !item.Command.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(item.Command)) File.Move(item.Command, item.Command + ".disabled");
            }
        }
    }
}