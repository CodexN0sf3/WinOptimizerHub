using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace WinOptimizerHub.Helpers
{
    public static class AppLogger
    {
        private static readonly string AppDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinOptimizerHub_Data");

        private static readonly string LogPath = Path.Combine(AppDataRoot, "Logs", "app.log");
        private static readonly object _lock = new object();
        private const long MaxLogSize = 2 * 1024 * 1024; // 2 MB

        public static string BackupRoot => Path.Combine(AppDataRoot, "Backups");

        static AppLogger()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                Directory.CreateDirectory(BackupRoot);
            }
            catch { }
        }

        public static void Log(Exception ex, string context = null)
        {
            if (ex == null) return;
            Write(LogLevel.Error, context ?? ex.TargetSite?.DeclaringType?.Name, BuildExceptionMessage(ex));
        }

        public static void LogInfo(string context, string message)
            => Write(LogLevel.Info, context, message);

        public static void LogWarning(string context, string message)
            => Write(LogLevel.Warning, context, message);

        public static void LogError(string context, string message)
            => Write(LogLevel.Error, context, message);

        private static void Write(LogLevel level, string context, string message)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level,-7}] " +
                          $"{context ?? "?"} | {message}";

            Debug.WriteLine(line);

            try
            {
                lock (_lock)
                {
                    RotateIfNeeded();
                    File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }

        private static string BuildExceptionMessage(Exception ex)
        {
            var sb = new StringBuilder();
            sb.Append($"{ex.GetType().Name}: {ex.Message}");

            var inner = ex.InnerException;
            int depth = 0;
            while (inner != null && depth < 3)
            {
                sb.Append($" → {inner.GetType().Name}: {inner.Message}");
                inner = inner.InnerException;
                depth++;
            }

            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                var frames = ex.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                int take = Math.Min(3, frames.Length);
                sb.AppendLine();
                for (int i = 0; i < take; i++)
                    sb.AppendLine("  " + frames[i].Trim());
            }

            return sb.ToString().TrimEnd();
        }

        private static void RotateIfNeeded()
        {
            try
            {
                var fi = new FileInfo(LogPath);
                if (!fi.Exists || fi.Length <= MaxLogSize) return;

                string backup = LogPath + ".bak";
                if (File.Exists(backup)) File.Delete(backup);
                File.Move(LogPath, backup);
            }
            catch { }
        }

        private enum LogLevel { Info, Warning, Error }
    }
}