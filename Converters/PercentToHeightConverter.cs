using System;
using System.Globalization;
using System.Windows.Data;

namespace WinOptimizerHub.Converters
{
    public class PercentToHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double pct = value is double d ? d : 0;
            double maxH = parameter != null && double.TryParse(parameter.ToString(), out double p) ? p : 80;
            return Math.Max(2, pct / 100.0 * maxH);
        }
        public object ConvertBack(object value, Type t, object par, CultureInfo c)
            => throw new NotImplementedException();
    }
}