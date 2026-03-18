using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;

namespace WinOptimizerHub.Services
{
    public class UninstallManagerService
    {
        public async Task<List<InstalledProgram>> GetInstalledProgramsAsync(
            CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var programs = new List<InstalledProgram>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                ScanUninstallKey(Registry.LocalMachine,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", programs, seen, ct);
                ScanUninstallKey(Registry.LocalMachine,
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", programs, seen, ct);
                ScanUninstallKey(Registry.CurrentUser,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", programs, seen, ct);

                return programs.OrderBy(p => p.Name).ToList();
            }, ct);
        }

        public async Task<List<InstalledProgram>> GetWindowsAppsAsync(
            CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var apps = new List<InstalledProgram>();
                try
                {
                    string script = @"
Get-AppxPackage -AllUsers | ForEach-Object {
    $pkg = $_
    $hasInstalledUser = $false
    if ($pkg.PackageUserInformation) {
        foreach ($u in $pkg.PackageUserInformation) {
            if ($u.InstallState -eq 'Installed') { $hasInstalledUser = $true; break }
        }
    } else {
        $hasInstalledUser = $true
    }
    if ($hasInstalledUser) {
        ""$($pkg.Name)|$($pkg.Publisher)|$($pkg.Version)|$($pkg.PackageFullName)|$($pkg.SignatureKind)""
    }
}";
                    string output = RunPowerShell(script, ct);
                    apps.AddRange(ParseAppxList(output));
                }
                catch (Exception ex) { AppLogger.Log(ex, "GetWindowsApps"); }

                return apps.OrderBy(a => a.Name).ToList();
            }, ct);
        }

        public async Task<(bool ok, string error)> UninstallProgramAsync(
            InstalledProgram program, IProgress<string> progress = null)
        {
            if (program.IsWindowsApp)
                return await UninstallWindowsAppAsync(program, progress);

            return await UninstallWin32Async(program, progress);
        }

        private static async Task<(bool ok, string error)> UninstallWin32Async(
            InstalledProgram program, IProgress<string> progress)
        {
            return await Task.Run(() =>
            {
                try
                {
                    progress?.Report($"Launching uninstaller for {program.Name}...");
                    string raw = program.UninstallString ?? "";
                    if (string.IsNullOrEmpty(raw))
                        return (false, "Uninstall command not found in registry.");

                    string exe, args;
                    if (raw.StartsWith("\""))
                    {
                        int q = raw.IndexOf('"', 1);
                        exe = q > 0 ? raw.Substring(1, q - 1) : raw.Trim('"');
                        args = q > 0 ? raw.Substring(q + 1).Trim() : "";
                    }
                    else
                    {
                        int sp = raw.IndexOf(' ');
                        exe = sp > 0 ? raw.Substring(0, sp) : raw;
                        args = sp > 0 ? raw.Substring(sp + 1) : "";
                    }

                    var psi = new ProcessStartInfo(exe, args)
                    {
                        UseShellExecute = true,
                    };

                    using var p = Process.Start(psi);
                    if (p == null) return (false, "Could not start uninstallation process.");

                    p.WaitForExit();
                    progress?.Report("Process finished.");

                    bool ok = p.ExitCode == 0 || p.ExitCode == 3010;
                    return (ok, ok ? "" : $"Exit code: {p.ExitCode}");
                }
                catch (Exception ex)
                {
                    AppLogger.Log(ex, nameof(UninstallWin32Async));
                    return (false, ex.Message);
                }
            });
        }

        public async Task<(bool ok, string error)> UninstallWindowsAppAsync(
            InstalledProgram app, IProgress<string> progress = null)
        {
            if (string.IsNullOrEmpty(app.PackageFullName))
                return (false, "PackageFullName este gol.");

            progress?.Report($"Se elimină aplicația: {app.Name}...");

            string pfn = EscapePs(app.PackageFullName);

            string cmd = $"$pkg = Get-AppxPackage -AllUsers | Where-Object {{$_.PackageFullName -eq '{pfn}'}}; " +
                         "if ($pkg) { " +
                         "  $pkg | Remove-AppxPackage -ErrorAction SilentlyContinue; " +
                         "  if ($?) { Write-Output 'SUCCESS' } else { " +
                         "    $pkg | Remove-AppxPackage -AllUsers -ErrorAction Stop; " +
                         "    Write-Output 'SUCCESS' " +
                         "  } " +
                         "} else { Write-Error 'Package not found' }";

            return await Task.Run(() =>
            {
                try
                {
                    var (stdout, stderr) = RunPowerShellWithStreams(cmd);

                    bool isOk = stdout.Contains("SUCCESS");
                    bool hasError = !isOk && !string.IsNullOrWhiteSpace(stderr) &&
                                   (stderr.Contains("Exception") || stderr.Contains("Error"));

                    if (hasError)
                    {
                        AppLogger.Log(new Exception(stderr), "UninstallWindowsApp");
                        return (false, stderr.Trim());
                    }

                    progress?.Report("Eliminare reușită.");
                    return (true, "");
                }
                catch (Exception ex)
                {
                    AppLogger.Log(ex, "UninstallWindowsApp");
                    return (false, ex.Message);
                }
            });
        }

        private static string RunPowerShell(string script, CancellationToken ct = default)
        {
            var (stdout, _) = RunPowerShellWithStreams(script, ct);
            return stdout;
        }

        private static (string stdout, string stderr) RunPowerShellWithStreams(
            string script, CancellationToken ct = default)
        {
            byte[] encoded = Encoding.Unicode.GetBytes(script);
            string b64 = Convert.ToBase64String(encoded);

            var psi = new ProcessStartInfo(
                "powershell.exe",
                $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {b64}")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using var p = Process.Start(psi);
            if (p == null) return ("", "Eșec la pornirea PowerShell");

            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit(120_000);
            return (stdout.Trim(), stderr.Trim());
        }

        private static List<InstalledProgram> ParseAppxList(string raw)
        {
            var result = new List<InstalledProgram>();
            var lines = raw.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length < 4) continue;

                string name = parts[0];
                string pub = parts[1];
                string ver = parts[2];
                string pkg = parts[3];
                string sig = parts.Length > 4 ? parts[4] : "";

                if (IsFrameworkPackage(name)) continue;

                bool isSystem = sig.Equals("System", StringComparison.OrdinalIgnoreCase)
                             || sig.Equals("Developer", StringComparison.OrdinalIgnoreCase);

                result.Add(new InstalledProgram
                {
                    Name = FormatAppName(name),
                    Publisher = pub,
                    Version = ver,
                    PackageFullName = pkg,
                    IsWindowsApp = true,
                    IsSystemApp = isSystem,
                });
            }
            return result;
        }

        private static bool IsFrameworkPackage(string name) =>
            name.StartsWith("Microsoft.VCLibs", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Microsoft.NET.Native", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Microsoft.DirectX", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Microsoft.Services.Store", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Windows.CBSPreview", StringComparison.OrdinalIgnoreCase);

        private static string FormatAppName(string raw)
        {
            raw = Regex.Replace(raw, @"^(Microsoft|Windows)\.", "");
            raw = Regex.Replace(raw, @"\.(Stable|Beta|Dev|Canary)$", "", RegexOptions.IgnoreCase);
            raw = Regex.Replace(raw, @"([a-z])([A-Z])", "$1 $2");
            return raw.Trim();
        }

        private static string EscapePs(string s) => s?.Replace("'", "''") ?? "";

        private void ScanUninstallKey(RegistryKey hive, string subKeyPath,
            List<InstalledProgram> programs, HashSet<string> seen, CancellationToken ct)
        {
            try
            {
                using var key = hive.OpenSubKey(subKeyPath);
                if (key == null) return;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    if (ct.IsCancellationRequested) return;
                    try
                    {
                        using var appKey = key.OpenSubKey(subKeyName);
                        if (appKey == null) continue;

                        string displayName = appKey.GetValue("DisplayName")?.ToString();
                        if (string.IsNullOrEmpty(displayName)) continue;
                        if (!seen.Add(displayName)) continue;
                        if (appKey.GetValue("SystemComponent")?.ToString() == "1") continue;

                        programs.Add(new InstalledProgram
                        {
                            Name = displayName,
                            Publisher = appKey.GetValue("Publisher")?.ToString() ?? "",
                            Version = appKey.GetValue("DisplayVersion")?.ToString() ?? "",
                            InstallDate = appKey.GetValue("InstallDate")?.ToString() ?? "",
                            EstimatedSize = GetLong(appKey.GetValue("EstimatedSize")),
                            UninstallString = appKey.GetValue("UninstallString")?.ToString() ?? "",
                            InstallLocation = appKey.GetValue("InstallLocation")?.ToString() ?? "",
                            DisplayIcon = appKey.GetValue("DisplayIcon")?.ToString() ?? "",
                            RegistryKey = subKeyName,
                        });
                    }
                    catch (Exception ex) { AppLogger.Log(ex); }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex); }
        }

        private static long GetLong(object val)
            => val != null && long.TryParse(val.ToString(), out long l) ? l : 0;
    }
}