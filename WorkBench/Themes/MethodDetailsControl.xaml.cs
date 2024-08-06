using Serein.NodeFlow;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Serein.WorkBench.Themes
{
    public class MultiConditionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] is Type valueType && values[1] is bool isEnabled)
            {
                if (isEnabled)
                {
                    if (valueType == typeof(string) || valueType == typeof(int) || valueType == typeof(double))
                    {
                        return "TextBoxTemplate";
                    }
                    else if (typeof(IEnumerable).IsAssignableFrom(valueType))
                    {
                        return "ComboBoxTemplate";
                    }
                }
            }
            return DependencyProperty.UnsetValue;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }



    public partial class MethodDetailsControl : UserControl//,ItemsControl
    {
        static MethodDetailsControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(MethodDetailsControl), new FrameworkPropertyMetadata(typeof(MethodDetailsControl)));
        }


        public MethodDetails MethodDetails
        {
            get { return (MethodDetails)GetValue(MethodDetailsProperty); }
            set { SetValue(MethodDetailsProperty, value); }
        }

        public static readonly DependencyProperty MethodDetailsProperty = DependencyProperty.Register("MethodDetails", typeof(MethodDetails),
           typeof(MethodDetailsControl), new PropertyMetadata(null, new PropertyChangedCallback(OnPropertyChange)));

        static void OnPropertyChange(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            
            var MethodDetails = (MethodDetails)args.NewValue;
            //MethodDetails.ExplicitDatas[0].
        }
    }
}
