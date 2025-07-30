using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;

namespace Serein.Workbench.Converters
{
    /// <summary>
    /// 根据bool类型控制可见性
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    internal class InvertableBooleanToVisibilityConverter : IValueConverter
    {
        enum Parameters
        {
            /// <summary>
            /// True为可见，False为不可见
            /// </summary>
            Normal,
            /// <summary>
            /// False为可见，True为不可见
            /// </summary>
            Inverted
        }

        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            var boolValue = (bool)value;
            var direction = (Parameters)Enum.Parse(typeof(Parameters), (string)parameter);

            if (direction == Parameters.Inverted)
                return !boolValue ? Visibility.Visible : Visibility.Collapsed;

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object? ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
