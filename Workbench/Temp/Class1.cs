using Serein.Library;
using Serein.Script;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;


namespace CXLims.Software.WPFTemplate
{


    [ContentProperty(nameof(XamlScript.ScriptContent))]
    public class XamlScript : DependencyObject
    {
        /// <summary>
        /// 脚本节点内容
        /// </summary>
        public string? ScriptContent { get; set; }

        public override string ToString() => ScriptContent ?? string.Empty;
    }

    public enum XamlScriptTriggerType
    {
        /// <summary>
        /// 初始化时触发
        /// </summary>
        Loaded,
        /// <summary>
        /// 属性变更触发
        /// </summary>
        Property,
        /// <summary>
        /// 指定事件触发
        /// </summary>
        Event,
        /// <summary>
        /// 获得/失去焦点触发
        /// </summary>
        Focus
    }

    /*public enum FindPriorityType
    {
        /// <summary>
        /// 不查找
        /// </summary>
        None,
        /// <summary>
        /// 视觉优先
        /// </summary>
        Visual,
        /// <summary>
        /// 逻辑优先
        /// </summary>
        Logical,
        /// <summary>
        /// 仅视觉
        /// </summary>
        VisualOnly,
        /// <summary>
        /// 仅逻辑
        /// </summary>
        LogicalOnly,
    }*/



    public static class ScriptMethod
    {
        public static void debug(object value)
        {
            Debug.WriteLine(value);
        }
    }


    public static class XScript
    {
        static XScript()
        {
            LoadType(typeof(ScriptBaseFunc));
            LoadType(typeof(ScriptMethod));
        }

        private static void LoadType(Type type)
        {
            // 获取方法
            var tempMethods = type.GetMethods().Where(method =>
                    method.IsStatic &&
                    !(method.Name.Equals("GetHashCode")
                    || method.Name.Equals("Equals")
                    || method.Name.Equals("ToString")
                    || method.Name.Equals("GetType")
            )).Select(method => (method.Name, method)).ToArray();
            // 挂在方法
            foreach ((string name, MethodInfo method) item in tempMethods)
            {
                SereinScript.AddStaticFunction(item.name, item.method);
            }
        }

        #region 附加属性定义


        public static readonly DependencyProperty XamlScriptProperty =
            DependencyProperty.RegisterAttached(
                "XamlScript",
                typeof(XamlScript),
                typeof(XScript),
                new PropertyMetadata(null, OnXamlScriptChanged));

        /// <summary>触发类型</summary>
        public static readonly DependencyProperty TriggerTypeProperty =
            DependencyProperty.RegisterAttached(
                "TriggerType",
                typeof(XamlScriptTriggerType),
                typeof(XScript),
                new PropertyMetadata(XamlScriptTriggerType.Loaded));

        /// <summary>触发属性名称</summary>
        public static readonly DependencyProperty TriggerNameProperty =
            DependencyProperty.RegisterAttached(
                "TriggerName",
                typeof(string),
                typeof(XScript),
                new PropertyMetadata(""));



        /// <summary>脚本返回结果，可用于外部绑定</summary>
        public static readonly DependencyProperty ReturnProperty =
            DependencyProperty.RegisterAttached(
                "Return",
                typeof(object),
                typeof(XScript),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));




        public static void SetXamlScript(DependencyObject element, XamlScript value) => element.SetValue(XamlScriptProperty, value);
        public static XamlScript GetXamlScript(DependencyObject element) => (XamlScript)element.GetValue(XamlScriptProperty);

        public static void SetTriggerType(DependencyObject element, XamlScriptTriggerType value) => element.SetValue(TriggerTypeProperty, value);
        public static XamlScriptTriggerType GetTriggerType(DependencyObject element) => (XamlScriptTriggerType)element.GetValue(TriggerTypeProperty);

        public static void SetTriggerName(DependencyObject element, string value) => element.SetValue(TriggerNameProperty, value);
        public static string GetTriggerName(DependencyObject element) => (string)element.GetValue(TriggerNameProperty);

        public static void SetReturn(DependencyObject element, object value) => element.SetValue(ReturnProperty, value);
        public static object GetReturn(DependencyObject element) => element.GetValue(ReturnProperty);



        public static readonly DependencyProperty ParamData1Property = DependencyProperty.RegisterAttached("ParamData1", typeof(object), typeof(XScript), new PropertyMetadata(null));
        public static readonly DependencyProperty ParamData2Property = DependencyProperty.RegisterAttached("ParamData2", typeof(object), typeof(XScript), new PropertyMetadata(null));
        public static readonly DependencyProperty ParamData3Property = DependencyProperty.RegisterAttached("ParamData3", typeof(object), typeof(XScript), new PropertyMetadata(null));
        public static readonly DependencyProperty ParamData4Property = DependencyProperty.RegisterAttached("ParamData4", typeof(object), typeof(XScript), new PropertyMetadata(null));
        public static readonly DependencyProperty ParamData5Property = DependencyProperty.RegisterAttached("ParamData5", typeof(object), typeof(XScript), new PropertyMetadata(null));
        public static readonly DependencyProperty ParamData6Property = DependencyProperty.RegisterAttached("ParamData6", typeof(object), typeof(XScript), new PropertyMetadata(null));
        public static readonly DependencyProperty ParamData7Property = DependencyProperty.RegisterAttached("ParamData7", typeof(object), typeof(XScript), new PropertyMetadata(null));
        public static readonly DependencyProperty ParamData8Property = DependencyProperty.RegisterAttached("ParamData8", typeof(object), typeof(XScript), new PropertyMetadata(null));
        public static void SetParamData1(DependencyObject element, object value) => element.SetValue(ParamData1Property, value); public static object GetParamData1(DependencyObject element) => element.GetValue(ParamData1Property);
        public static void SetParamData2(DependencyObject element, object value) => element.SetValue(ParamData2Property, value); public static object GetParamData2(DependencyObject element) => element.GetValue(ParamData2Property);
        public static void SetParamData3(DependencyObject element, object value) => element.SetValue(ParamData3Property, value); public static object GetParamData3(DependencyObject element) => element.GetValue(ParamData3Property);
        public static void SetParamData4(DependencyObject element, object value) => element.SetValue(ParamData4Property, value); public static object GetParamData4(DependencyObject element) => element.GetValue(ParamData4Property);
        public static void SetParamData5(DependencyObject element, object value) => element.SetValue(ParamData5Property, value); public static object GetParamData5(DependencyObject element) => element.GetValue(ParamData5Property);
        public static void SetParamData6(DependencyObject element, object value) => element.SetValue(ParamData6Property, value); public static object GetParamData6(DependencyObject element) => element.GetValue(ParamData6Property);
        public static void SetParamData7(DependencyObject element, object value) => element.SetValue(ParamData7Property, value); public static object GetParamData7(DependencyObject element) => element.GetValue(ParamData7Property);
        public static void SetParamData8(DependencyObject element, object value) => element.SetValue(ParamData8Property, value); public static object GetParamData8(DependencyObject element) => element.GetValue(ParamData8Property);


        public static readonly DependencyProperty ParamName1Property = DependencyProperty.RegisterAttached("ParamName1", typeof(string), typeof(XScript), new PropertyMetadata(""));
        public static readonly DependencyProperty ParamName2Property = DependencyProperty.RegisterAttached("ParamName2", typeof(string), typeof(XScript), new PropertyMetadata(""));
        public static readonly DependencyProperty ParamName3Property = DependencyProperty.RegisterAttached("ParamName3", typeof(string), typeof(XScript), new PropertyMetadata(""));
        public static readonly DependencyProperty ParamName4Property = DependencyProperty.RegisterAttached("ParamName4", typeof(string), typeof(XScript), new PropertyMetadata(""));
        public static readonly DependencyProperty ParamName5Property = DependencyProperty.RegisterAttached("ParamName5", typeof(string), typeof(XScript), new PropertyMetadata(""));
        public static readonly DependencyProperty ParamName6Property = DependencyProperty.RegisterAttached("ParamName6", typeof(string), typeof(XScript), new PropertyMetadata(""));
        public static readonly DependencyProperty ParamName7Property = DependencyProperty.RegisterAttached("ParamName7", typeof(string), typeof(XScript), new PropertyMetadata(""));
        public static readonly DependencyProperty ParamName8Property = DependencyProperty.RegisterAttached("ParamName8", typeof(string), typeof(XScript), new PropertyMetadata(""));
        public static void SetParamName1(DependencyObject element, string value) => element.SetValue(ParamName1Property, value); public static string GetParamName1(DependencyObject element) => (string)element.GetValue(ParamName1Property);
        public static void SetParamName2(DependencyObject element, string value) => element.SetValue(ParamName2Property, value); public static string GetParamName2(DependencyObject element) => (string)element.GetValue(ParamName2Property);
        public static void SetParamName3(DependencyObject element, string value) => element.SetValue(ParamName3Property, value); public static string GetParamName3(DependencyObject element) => (string)element.GetValue(ParamName3Property);
        public static void SetParamName4(DependencyObject element, string value) => element.SetValue(ParamName4Property, value); public static string GetParamName4(DependencyObject element) => (string)element.GetValue(ParamName4Property);
        public static void SetParamName5(DependencyObject element, string value) => element.SetValue(ParamName5Property, value); public static string GetParamName5(DependencyObject element) => (string)element.GetValue(ParamName5Property);
        public static void SetParamName6(DependencyObject element, string value) => element.SetValue(ParamName6Property, value); public static string GetParamName6(DependencyObject element) => (string)element.GetValue(ParamName6Property);
        public static void SetParamName7(DependencyObject element, string value) => element.SetValue(ParamName7Property, value); public static string GetParamName7(DependencyObject element) => (string)element.GetValue(ParamName7Property);
        public static void SetParamName8(DependencyObject element, string value) => element.SetValue(ParamName8Property, value); public static string GetParamName8(DependencyObject element) => (string)element.GetValue(ParamName8Property);


        #endregion




        private static void OnXamlScriptChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement fe)
                return;

            //if (e.NewValue is not Xaml xaml)
            //    return;

            // 绑定上下文
            //BindLetsToContext(fe, xaml);
            var trigger = GetTriggerType(fe);
            fe.Loaded += (_, _) =>
            {
                switch (trigger)
                {
                    case XamlScriptTriggerType.Focus:
                        fe.GotFocus += (_, _) => RunScript(fe);
                        fe.LostFocus += (_, _) => RunScript(fe);
                        break;

                    case XamlScriptTriggerType.Property:
                        AttachPropertyTrigger(fe);
                        break;

                    case XamlScriptTriggerType.Event:
                        HookEvent(fe);
                        break;
                }

                // 动态绑定 Let 到 DataContent
                //BindLetsToDataContent(fe, xaml);
                if (GetTriggerType(fe) == XamlScriptTriggerType.Loaded)
                    RunScript(fe);
            };


        }





        private static void AttachPropertyTrigger(FrameworkElement fe)
        {
            var propName = GetTriggerName(fe);
            if (string.IsNullOrWhiteSpace(propName))
                return;

            DependencyPropertyDescriptor dpd =
                DependencyPropertyDescriptor.FromName(propName, fe.GetType(), fe.GetType());

            if (dpd != null)
            {
                dpd.AddValueChanged(fe, (_, _) => RunScript(fe));
            }
            else
            {
                // 非 DependencyProperty，监听普通属性
                var prop = fe.GetType().GetProperty(propName);
                if (prop != null)
                {
                    if (fe is INotifyPropertyChanged notifier)
                    {
                        notifier.PropertyChanged += (s, ev) =>
                        {
                            if (ev.PropertyName == propName)
                                RunScript(fe);
                        };
                    }
                }
            }
        }

        private static void HookEvent(FrameworkElement fe)
        {
            var eventName = GetTriggerName(fe);
            if (string.IsNullOrEmpty(eventName))
            {
                return;
            }
            var evt = fe.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
            if (evt == null)
            {
                Debug.WriteLine($"找不到事件 {eventName} 于 {fe.GetType().Name}");
                return;
            }

            // 动态创建一个与事件签名匹配的委托
            var handlerMethod = typeof(XScript).GetMethod(nameof(OnEventRaised),
                BindingFlags.NonPublic | BindingFlags.Static);

            if (handlerMethod == null)
                return;

            var handler = Delegate.CreateDelegate(evt.EventHandlerType, handlerMethod);
            evt.AddEventHandler(fe, handler);

            // 将 fe 和 xaml 映射保存，方便在回调时取出上下文
            //_eventContext[fe] = xaml;
        }
        //private static readonly Dictionary<object, object> _eventContext = new();

        private static void OnEventRaised(object sender, EventArgs e)
        {
            if (sender is FrameworkElement fe)
            {
                //&& _eventContext.TryGetValue(fe, out var xaml)
                RunScript(fe);
            }
        }

        private static async void RunScript(FrameworkElement fe)
        {
            try
            {
                var result = await ExecuteScriptAsync(fe);
                fe.SetValue(ReturnProperty, result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        private static (string?, object?) GetGetParamFunc(FrameworkElement fe, int index)
        {
            Func<DependencyObject, string> getNamefunc = index switch
            {
                1 => GetParamName1,
                2 => GetParamName2,
                3 => GetParamName3,
                4 => GetParamName4,
                5 => GetParamName5,
                6 => GetParamName6,
                7 => GetParamName7,
                8 => GetParamName8,
                _ => throw new NotImplementedException()
            };

            Func<DependencyObject, object> getDatafunc = index switch
            {
                1 => GetParamData1,
                2 => GetParamData2,
                3 => GetParamData3,
                4 => GetParamData4,
                5 => GetParamData5,
                6 => GetParamData6,
                7 => GetParamData7,
                8 => GetParamData8,
                _ => throw new NotImplementedException()
            };
            DependencyProperty paramDataProperty = index switch
            {
                1 => ParamData1Property,
                2 => ParamData2Property,
                3 => ParamData3Property,
                4 => ParamData4Property,
                5 => ParamData5Property,
                6 => ParamData6Property,
                7 => ParamData7Property,
                8 => ParamData8Property,
                _ => throw new NotImplementedException()
            };
            var d1 = GetParamData1(fe);
            var userControl = FindVisualParent(fe, typeof(UserControl));
            var value = GetBindingValue(fe, paramDataProperty, fe.DataContext);
            var name = getNamefunc?.Invoke(fe);
            var data = getDatafunc?.Invoke(fe);
            var letData = fe.GetValue(paramDataProperty);
            var datacontext = fe.DataContext;
            var d = fe.GetValue(paramDataProperty);
            return (name, data);
        }

        /// <summary>
        /// 获取绑定值
        /// </summary>
        /// <param name="fe"></param>
        /// <param name="let"></param>
        /// <param name="dataContext"></param>
        /// <returns></returns>
        private static object? GetBindingValue(FrameworkElement fe, DependencyProperty paramDataProperty, object dataContext)
        {
            
            //return fe.GetValue(paramDataProperty);

            var bindingExpr = BindingOperations.GetBindingExpression(fe, paramDataProperty);
            if(bindingExpr is not  null)
            {
                // 取原始绑定
                Binding oldBinding = bindingExpr.ParentBinding;

                //var rs = oldBinding.RelativeSource;
                // 创建新绑定，复制原绑定设置
                var newBinding = new Binding
                {
                    Path = oldBinding.Path,
                    Mode = oldBinding.Mode,
                    UpdateSourceTrigger = oldBinding.UpdateSourceTrigger,
                    Converter = oldBinding.Converter,
                    ConverterParameter = oldBinding.ConverterParameter,
                    ConverterCulture = oldBinding.ConverterCulture,
                    Source = dataContext
                };

                // 重新应用
                BindingOperations.SetBinding(fe,paramDataProperty, newBinding);
                var data = fe.GetValue(paramDataProperty);
                return data;
            }
            return null;
        }


        private static async Task<object?> ExecuteScriptAsync(FrameworkElement fe)
        {
            var script = GetXamlScript(fe);
            if (script is null || string.IsNullOrWhiteSpace(script.ScriptContent))
            {
                Debug.WriteLine($"[{fe.Name}] 脚本为空跳过执行 ({DateTime.Now:T})");
                return null;
            }
            Debug.WriteLine($"[{fe.Name}] 执行脚本 ({DateTime.Now:T})");




            IScriptInvokeContext invokeContext = new ScriptInvokeContext();
            Dictionary<string, Type> letTypes = new Dictionary<string, Type>();

            for (var i = 1; i <= 8; i++)
            {
                (var name, var data) = GetGetParamFunc(fe, i);
                if (name is null || data is null
                    || string.IsNullOrEmpty(name))
                {
                    continue;
                }
                var type = data.GetType();
                letTypes[name] = type;
                invokeContext.SetVarValue(name, data);

            }



            SereinScript sereinScript = new SereinScript();
            sereinScript.ParserScript(script.ScriptContent, letTypes);
            var result = await sereinScript.InterpreterAsync(invokeContext);

            Debug.WriteLine("脚本执行结束\n");
            return result;

        }
        /// <summary>
        /// 视觉树查找控件
        /// </summary>
        /// <param name="child"></param>
        /// <param name="targetType"></param>
        /// <returns></returns>
        private static DependencyObject? FindVisualParent(DependencyObject child, Type targetType)
        {
            DependencyObject? current = child;

            while (current != null)
            {
                current = VisualTreeHelper.GetParent(current);
                if (current != null && targetType.IsAssignableFrom(current.GetType()))
                    return current;
            }
            return null;
        }
    }

}


