using System;
using System.Windows.Data;
using System.Windows.Media;

namespace HReflectionResolution.Converters
{
    internal class NotFoundAssemblyForegroundColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string location = string.Empty;
            if (value != null)
                location = value.ToString();

            if (string.IsNullOrWhiteSpace(location))
                return new SolidColorBrush(Colors.Red);
            else
                return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
}
