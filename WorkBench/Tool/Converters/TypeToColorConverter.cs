using Serein.Library.Enums;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Serein.Workbench.Tool.Converters
{
    /// <summary>
    /// 根据控件类型切换颜色
    /// </summary>
    public class TypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 根据 ControlType 返回颜色
            return value switch
            {
                NodeControlType.Action => Brushes.Blue,
                NodeControlType.Flipflop => Brushes.Green,
                _ => Brushes.Black,
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
