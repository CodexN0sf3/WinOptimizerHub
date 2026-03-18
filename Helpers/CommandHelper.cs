using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WinOptimizerHub.Helpers
{
    public static class CommandHelper
    {
        public static async Task<string> RunAsync(
            string exe,
            string args,
            CancellationToken ct = default,
            bool resolveSysNative = false)
        {
            return await Task.Run(
                () => RunCore(exe, args, resolveSysNative, timeoutMs: -1),
                ct);
        }

        public static string RunSync(string exe, string args, int timeoutMs = -1)
            => RunCore(exe, args, resolveSysNative: false, timeoutMs);

        public static string RunWithProgress(
            string exe,
            string args,
            Action<string> onLine,
            int timeoutMs = 10 * 60 * 1000)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using var p = Process.Start(psi);
                if (p == null) return string.Empty;

                var sb = new StringBuilder();

                p.OutputDataReceived += (_, e) =>
                {
                    if (e.Data == null) return;
                    sb.AppendLine(e.Data);
                    onLine?.Invoke(e.Data);
                };
                p.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data == null) return;
                    sb.AppendLine(e.Data);
                    onLine?.Invoke(e.Data);
                };

                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                bool exited = p.WaitForExit(timeoutMs);

                if (!exited)
                {
                    try { if (!p.HasExited) p.Kill(); }
                    catch { /* ignore – process may have just exited */ }
                }

                p.WaitForExit();

                return sb.ToString().Trim();
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public static string RunWithProgressRaw(
            string exe,
            string args,
            Action<string> onLine,
            int timeoutMs = 15 * 60 * 1000)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.Unicode,
                    StandardErrorEncoding = Encoding.Unicode,
                };

                using var p = Process.Start(psi);
                if (p == null) return string.Empty;

                var sb = new StringBuilder();
                var buf = new StringBuilder();

                var reader = p.StandardOutput;
                int c;
                while ((c = reader.Read()) != -1)
                {
                    char ch = (char)c;
                    if (ch == '\r' || ch == '\n')
                    {
                        string line = buf.ToString().Trim();
                        buf.Clear();

                        line = System.Text.RegularExpressions.Regex
                                     .Replace(line, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            sb.AppendLine(line);
                            onLine?.Invoke(line);
                        }

                        if (ch == '\r' && reader.Peek() == '\n')
                            reader.Read();
                    }
                    else
                    {
                        buf.Append(ch);
                    }
                }

                if (buf.Length > 0)
                {
                    string last = buf.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(last)) { sb.AppendLine(last); onLine?.Invoke(last); }
                }

                bool exited = p.WaitForExit(timeoutMs);
                if (!exited) try { if (!p.HasExited) p.Kill(); } catch { }
                p.WaitForExit();

                return sb.ToString().Trim();
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        private static string RunCore(
            string exe, string args, bool resolveSysNative, int timeoutMs)
        {
            try
            {
                var fileName = resolveSysNative ? ResolveExe(exe) : exe;
                var psi = new ProcessStartInfo(fileName, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using var p = Process.Start(psi);
                if (p == null) return string.Empty;

                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();

                if (timeoutMs > 0)
                    p.WaitForExit(timeoutMs);
                else
                    p.WaitForExit();

                string result = string.IsNullOrWhiteSpace(output) ? error : output;
                return result.Trim();
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public static string ResolveSysNative(string exe) => ResolveExe(exe);

        private static string ResolveExe(string exe)
        {
            string sysNative = Path.Combine(
                Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows",
                "Sysnative",
                exe);
            return File.Exists(sysNative) ? sysNative : exe;
        }
    }
}
