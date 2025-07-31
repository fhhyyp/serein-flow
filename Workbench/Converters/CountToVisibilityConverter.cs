using System;
using System.Collections;
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
    /// 将集合的元素数量转换为可见性。
    /// </summary>
    internal class CountToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 可选：是否反转逻辑。
        /// </summary>
        public bool Inverse { get; set; } = false; 

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable collection)
            {
                int count = 0;
                foreach (var item in collection) count++;
                bool visible = count > 0;
                if (Inverse) visible = !visible;
                return visible ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

}
