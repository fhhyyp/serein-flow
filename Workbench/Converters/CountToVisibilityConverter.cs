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
    public class CountToVisibilityConverter : IValueConverter
    {
        public bool Inverse { get; set; } = false; // 可选：反转逻辑

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
