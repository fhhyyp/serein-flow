using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Serein.Workbench.Converters
{
    internal class MethodDetailsSelectorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            bool isShareParam = (bool)values[0];
            var nodeDetails = values[1];
            var selectNodeDetails = values[2];

           return isShareParam ? nodeDetails : selectNodeDetails;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
