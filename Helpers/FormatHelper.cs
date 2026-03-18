namespace WinOptimizerHub.Helpers
{
    public static class FormatHelper
    {
        public static string FormatSize(long bytes)
        {
            if (bytes >= 1_099_511_627_776) return $"{bytes / 1_099_511_627_776.0:F1} TB";
            if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
            if (bytes >= 1_024) return $"{bytes / 1_024.0:F1} KB";
            return $"{bytes} B";
        }
    }
}
