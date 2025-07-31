using Serein.Library.Utils;
using Serein.Workbench.Extension;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Serein.Workbench.Converters
{

    /// <summary>
    /// 类型名称显示转换器
    /// </summary>
    internal class TypeNameDisplaynConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(value is Type type)
            {
                string typeName = type.GetFriendlyName(false);
                return typeName;
            }
            else
            {
                return "";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
