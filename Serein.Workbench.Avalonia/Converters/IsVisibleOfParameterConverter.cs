using Avalonia.Data;
using Avalonia.Data.Converters;
using Serein.Library;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Avalonia.Converters
{
    internal class IsVisibleOfParameterConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
           
            if(value is ParameterDetails pd)
            {
                
                if (pd.ExplicitTypeName == "Value")
                {
                    
                    return false;
                
                }
                else
                {
                    return true;
                }
            }
            
            // converter used for the wrong type
            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
