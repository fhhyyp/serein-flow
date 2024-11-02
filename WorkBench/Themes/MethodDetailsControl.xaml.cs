using Serein.Library;
using Serein.Workbench.Node;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Serein.Workbench.Themes
{
    public class MultiConditionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] is Type valueType && values[1] is bool isEnabled)
            {
                if (isEnabled)
                {
                    // 返回文本框
                    if (valueType == typeof(string) || valueType == typeof(int) || valueType == typeof(double))
                    {
                        return "TextBoxTemplate";
                    }
                    // 返回可选列表框
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



    /// <summary>
    /// 方法参数控件
    /// </summary>
    public partial class MethodDetailsControl : UserControl
    {
        static MethodDetailsControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(MethodDetailsControl), new FrameworkPropertyMetadata(typeof(MethodDetailsControl)));

        }

        #region 绑定的方法信息
        public MethodDetails MethodDetails
        {
            get { return (MethodDetails)GetValue(MethodDetailsProperty); }
            set { SetValue(MethodDetailsProperty, value); }
        }

        public static readonly DependencyProperty MethodDetailsProperty = DependencyProperty.Register(nameof(MethodDetails), typeof(MethodDetails),
           typeof(MethodDetailsControl), new PropertyMetadata(null, new PropertyChangedCallback(OnPropertyChange)));

        #endregion


        static void OnPropertyChange(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            //var MethodDetails = (MethodDetails)args.NewValue;
            //MethodDetails.ExplicitDatas[0].
        }


        public ICommand CommandAddParams { get; }

        public MethodDetailsControl()
        {
            CommandAddParams = new RelayCommand(ExecuteAddParams);
        }

        private void ExecuteAddParams(object parameter)
        {
            // 方法逻辑
            this.MethodDetails.AddParamsArg();
        }



    }
}
