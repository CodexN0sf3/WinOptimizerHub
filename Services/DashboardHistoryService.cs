using System;
using System.Globalization;
using System.IO;
using WinOptimizerHub.Helpers;

namespace WinOptimizerHub.Services
{
    public class DashboardHistoryService
    {
        private static readonly string DataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinOptimizerHub_Data", "dashboard_history.csv");

        private const int MaxSnapshots = 1000;

        public class Snapshot
        {
            public DateTime Time { get; set; }
            public double RamUsedGb { get; set; }
            public double RamTotalGb { get; set; }
            public double DiskFreeGb { get; set; }
            public double DiskTotalGb { get; set; }
            public long FreedSession { get; set; }
        }

        private static readonly object _fileLock = new object();

        public void Record(double ramUsedGb, double ramTotalGb,
                           double diskFreeGb, double diskTotalGb,
                           long freedSession = 0)
        {
            try
            {
                lock (_fileLock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!);
                    string line = string.Format(CultureInfo.InvariantCulture,
                        "{0:O},{1:F2},{2:F2},{3:F2},{4:F2},{5}",
                        DateTime.Now, ramUsedGb, ramTotalGb, diskFreeGb, diskTotalGb, freedSession);

                    File.AppendAllText(DataPath, line + Environment.NewLine);
                    OptimizeFileIfNeeded();
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(Record)); }
        }

        private static void OptimizeFileIfNeeded()
        {
            if (!File.Exists(DataPath)) return;

            string[] lines;
            try { lines = File.ReadAllLines(DataPath); }
            catch (Exception ex)
            {AppLogger.Log(ex, nameof(OptimizeFileIfNeeded));
                return;
            }

            int cleanupThreshold = MaxSnapshots + Math.Max(10, MaxSnapshots / 10);
            if (lines.Length <= cleanupThreshold) return;

            int linesToKeep = MaxSnapshots;
            string tempPath = DataPath + ".tmp";
            try
            {
                File.WriteAllLines(tempPath,
                    new ArraySegment<string>(lines, lines.Length - linesToKeep, linesToKeep));
                File.Delete(DataPath);
                File.Move(tempPath, DataPath);
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(OptimizeFileIfNeeded)); }
        }

    }
}