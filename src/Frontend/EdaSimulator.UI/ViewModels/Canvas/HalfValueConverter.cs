using System;
using System.Globalization;
using System.Windows.Data;

namespace EdaSimulator.UI.ViewModels.Canvas
{
    /// <summary>
    /// Returns half of a double-precision numeric value.
    /// Used to offset Path origin to the center of its containing Canvas,
    /// so symbol PathData coordinates treat (0,0) as the component center.
    /// </summary>
    public class HalfValueConverter : IValueConverter
    {
        /// <summary>Singleton instance for use in {x:Static} XAML markup.</summary>
        public static readonly HalfValueConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is double d ? d / 2.0 : 0.0;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is double d ? d * 2.0 : 0.0;
    }
}
