using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WinOptimizerHub.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool bVal = value is true;
            bool invert = parameter?.ToString()?.ToLower() == "invert";
            if (invert) bVal = !bVal;
            return bVal ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility.Visible;
    }
}