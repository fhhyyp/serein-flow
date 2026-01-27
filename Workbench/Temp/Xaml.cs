using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using System.Windows;

namespace Serein.Workbench.Temp
{
#if false
    /// <summary>
    /// 
    /// </summary>
    [ContentProperty(nameof(Script))]
    public class Xaml : FrameworkElement
    {
        public Xaml()
        {
            Lets = new ObservableCollection<XamlLet>();
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public ObservableCollection<XamlLet> Lets { get; }

        public XamlScript Script { get; set; }

        /// <summary>查找优先级</summary>
        public static readonly DependencyProperty TriggerTargetProperty =
                            DependencyProperty.RegisterAttached(
                                "TriggerTarget",
                                typeof(string),
                                typeof(XamlScript),
                                new PropertyMetadata(""));


        /// <summary>
        /// 当 TriggerType = Event 时, 指定事件名称
        /// 当 TriggerType = PropertyChanged 时, 指定属性名称
        /// </summary>
        public static void SetTriggerTarget(DependencyObject element, string value) => element.SetValue(TriggerTargetProperty, value);

        /// <summary>
        /// 当 TriggerType = Event 时, 获取事件名称
        /// 当 TriggerType = PropertyChanged 时, 获取属性名称
        /// </summary>
        public static string GetTriggerTarget(DependencyObject element) => (string)element.GetValue(TriggerTargetProperty);



        /// <summary>脚本返回结果，可用于外部绑定</summary>
        public static readonly DependencyProperty ParamDatasProperty =
            DependencyProperty.RegisterAttached(
                "ParamDatas",
                typeof(ObservableCollection<object>),
                typeof(XamlScript),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));


        public static void SetParamDatas(DependencyObject element, ObservableCollection<object> value) => element.SetValue(ParamDatasProperty, value);
        public static ObservableCollection<object> GetParamDatas(DependencyObject element) => (ObservableCollection<object>)element.GetValue(ParamDatasProperty);


        public static readonly DependencyProperty ParamNamesProperty =
            DependencyProperty.RegisterAttached(
                "ParamNames",
                typeof(ObservableCollection<object>),
                typeof(XamlScript),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));


        public static void SetParamNames(DependencyObject element, ObservableCollection<string> value) => element.SetValue(ParamNamesProperty, value);
        public static ObservableCollection<string> GetParamNames(DependencyObject element) => (ObservableCollection<string>)element.GetValue(ParamNamesProperty);
    }


    public class XamlLet : FrameworkElement
    {

        public new string? Name
        {
            get => (string?)GetValue(NameProperty);
            set => SetValue(NameProperty, value);
        }

        public new static readonly DependencyProperty NameProperty =
            DependencyProperty.Register(nameof(Name), typeof(string), typeof(XamlLet), new PropertyMetadata(null));

        /// <summary>
        /// 可选类型限定
        /// </summary>
        public Type? Type
        {
            get => (Type?)GetValue(TypeProperty);
            set => SetValue(TypeProperty, value);
        }

        public static readonly DependencyProperty TypeProperty =
            DependencyProperty.Register(nameof(Type), typeof(Type), typeof(XamlLet), new PropertyMetadata(null));

        /// <summary>查找优先级</summary>
        public static readonly DependencyProperty ValueProperty =
                            DependencyProperty.RegisterAttached(
                                "Value",
                                typeof(object),
                                typeof(XamlScript),
                                new PropertyMetadata(null));

        public static void SetValue(DependencyObject element, object value) => element.SetValue(ValueProperty, value);

        public static object GetValue(DependencyObject element) => (object)element.GetValue(ValueProperty);


        ///// <summary>
        ///// 可以是 BindingExpression、字符串、任意对象
        ///// </summary>
        //public object Value
        //{
        //    get => (object)GetValue(ValueProperty);
        //    set => SetValue(ValueProperty, value);
        //}

        //public static readonly DependencyProperty ValueProperty =
        //    DependencyProperty.Register(nameof(Value), typeof(object), typeof(XamlLet), new PropertyMetadata(null));

        //public override string ToString() => $"Let {Name} = {Value}";


    }


    [ContentProperty(nameof(Content))]
    public class XamlScript : FrameworkElement
    {
        /// <summary>
        /// 脚本节点内容
        /// </summary>
        public string? Content { get; set; }

        public override string ToString() => Content ?? string.Empty;
    }
#endif
    
}
