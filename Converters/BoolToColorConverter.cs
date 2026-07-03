using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace RoverExplorer1NodoMandoPC.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool pressed && pressed)
                return new SolidColorBrush(Color.Parse("#3fb950"));
            return new SolidColorBrush(Color.Parse("#30363d"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
