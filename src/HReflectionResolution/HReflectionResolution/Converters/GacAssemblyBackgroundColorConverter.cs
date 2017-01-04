using System;
using System.Windows.Data;
using System.Windows.Media;

namespace HReflectionResolution.Converters
{
    internal class GacAssemblyBackgroundColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool gac = (bool)value;

            if (gac)
                return new SolidColorBrush(Color.FromArgb(100, 188, 221, 255));
            else
                return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
}
