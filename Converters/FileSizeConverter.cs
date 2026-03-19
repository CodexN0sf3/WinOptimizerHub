using System;
using System.Globalization;
using System.Windows.Data;

namespace WinOptimizerHub.Converters
{
    [ValueConversion(typeof(long), typeof(string))]
    public class FileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not long size) return "0 B";
            if (size >= 1_099_511_627_776) return $"{size / 1_099_511_627_776.0:F1} TB";
            if (size >= 1_073_741_824) return $"{size / 1_073_741_824.0:F1} GB";
            if (size >= 1_048_576) return $"{size / 1_048_576.0:F1} MB";
            if (size >= 1024) return $"{size / 1024.0:F1} KB";
            return $"{size} B";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();

        public static string FormatSize(long bytes)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB" };
            if (bytes <= 0) return "0 B";

            int mag = (int)Math.Log(bytes, 1024);

            decimal adjustedSize = (decimal)bytes / (decimal)Math.Pow(1024, mag);

            return $"{adjustedSize:n2} {suf[mag]}";
        }

    }
}
