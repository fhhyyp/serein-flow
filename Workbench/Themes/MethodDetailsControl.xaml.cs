using Serein.Library;
using Serein.Workbench.Node;
using Serein.Workbench.Node.View;
using Serein.Workbench.Node.ViewModel;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Serein.Workbench.Themes
{
    /// <summary>
    /// 描述或名称转换器
    /// </summary>
    public class DescriptionOrNameConverter : IMultiValueConverter
    {
        /// <summary>
        /// 将源类型的数组转换为目标类型的值。
        /// </summary>
        /// <param name="values"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string description = values[0] as string;
            string name = values[1] as string;
            return string.IsNullOrWhiteSpace(description) ? name : description;
        }

        /// <summary>
        /// 将值从目标类型转换回源类型的数组。
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetTypes"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    ///  多条件转换器，根据参数类型和是否启用来决定返回的模板
    /// </summary>
    public class MultiConditionConverter : IMultiValueConverter
    {
        /// <summary>
        /// 将源类型的数组转换为目标类型的值。
        /// </summary>
        /// <param name="values"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 将值从目标类型转换回源类型的数组。
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetTypes"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 数据上下文代理类，用于绑定到DataContextProperty
    /// </summary>
    public class DataContextProxy : Freezable
    {
        /// <summary>
        /// 数据上下文代理类构造函数
        /// </summary>
        public DataContextProxy()
        {
            BindingOperations.SetBinding(this, DataContextProperty, new Binding());
        }

        /// <summary>
        /// 数据上下文属性
        /// </summary>
        public ParameterDetails DataContext
        {
            get { return (ParameterDetails)GetValue(DataContextProperty); }
            set { SetValue(DataContextProperty, value); }
        }

        /// <summary>
        /// 数据上下文依赖属性
        /// </summary>
        public static readonly DependencyProperty DataContextProperty = FrameworkElement
            .DataContextProperty.AddOwner(typeof(DataContextProxy));

        /// <summary>
        /// 创建一个新的实例，重写Freezable的CreateInstanceCore方法。
        /// </summary>
        /// <returns></returns>
        protected override Freezable CreateInstanceCore()
        {
            return new DataContextProxy();
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

        public NodeControlViewModelBase NodeViewModel
        {
            get { return (NodeControlViewModelBase)GetValue(NodeViewModelProperty); }
            set { SetValue(NodeViewModelProperty, value); }
        }

        public static readonly DependencyProperty NodeViewModelProperty = DependencyProperty.Register(nameof(NodeViewModel), typeof(NodeControlViewModelBase),
              typeof(MethodDetailsControl), new PropertyMetadata(null, new PropertyChangedCallback(OnPropertyChange)));



        #endregion


        static void OnPropertyChange(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            //var MethodDetails = (MethodDetails)args.NewValue;
            //MethodDetails.ExplicitDatas[0].
        }
        /// <summary>
        /// 添加参数命令
        /// </summary>

        public ICommand CommandAddParams { get; }

        /// <summary>
        /// 方法参数控件构造函数
        /// </summary>
        public MethodDetailsControl()
        {
            CommandAddParams = new RelayCommand(ExecuteAddParams);
        }

        

        private void ExecuteAddParams(object parameter)
        {
            // 方法逻辑
            this.MethodDetails.AddParamsArg(0);
        }

       
    }



}
