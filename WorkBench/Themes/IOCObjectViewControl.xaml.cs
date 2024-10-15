using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace Serein.WorkBench.Themes
{
    /// <summary>
    /// IOCObjectViewControl.xaml 的交互逻辑
    /// </summary>
    public partial class IOCObjectViewControl : UserControl
    {
        public Action<string,object> SelectObj { get; set; }

        public IOCObjectViewControl()
        {
            InitializeComponent();
        }

        private class IOCObj
        {
            public string Key { get; set; }
            public object Instance { get; set; }
        }

        /// <summary>
        /// 运行环境
        /// </summary>
        public IFlowEnvironment FlowEnvironment { get; set; }

        /// <summary>
        /// 添加一个实例
        /// </summary>
        /// <param name="key"></param>
        /// <param name="instance"></param>
        public void AddDependenciesInstance(string key,object instance)
        {
            IOCObj iOCObj = new IOCObj
            {
                Key = key,
                Instance = instance,
            };
            Application.Current.Dispatcher.Invoke(() =>
            {
                TextBlock textBlock = new TextBlock();
                textBlock.Text = key;
                textBlock.Tag = iOCObj;
                textBlock.MouseDown += (s, e) =>
                {
                    if (s is TextBlock block && block.Tag is IOCObj iocObj)
                    {
                        SelectObj?.Invoke(iocObj.Key, iocObj.Instance);
                        //FlowEnvironment.SetMonitorObjState(iocObj.Instance, true); // 通知环境，该节点的数据更新后需要传到UI
                    }
                };
                DependenciesListBox.Items.Add(textBlock);
                SortLisbox(DependenciesListBox);
            });
           
        }

        /// <summary>
        /// 刷新一个实例
        /// </summary>
        /// <param name="key"></param>
        /// <param name="instance"></param>
        public void RefreshDependenciesInstance(string key, object instance) 
        {
            foreach (var item in DependenciesListBox.Items)
            {
                if (item is TextBlock block && block.Tag is IOCObj iocObj && iocObj.Key.Equals(key))
                {
                    iocObj.Instance = instance;
                }
            }
        }

        public void ClearObjItem()
        {
            DependenciesListBox.Dispatcher.Invoke(() =>
            {
                DependenciesListBox.Items.Clear();
            });

        }

        private static void SortLisbox(ListBox listBox)
        {
            var sortedItems = listBox.Items.Cast<TextBlock>().OrderBy(x => x.Text).ToList();
            listBox.Items.Clear();
            foreach (var item in sortedItems)
            {
                listBox.Items.Add(item);
            }
        }

        public void RemoveDependenciesInstance(string key)
        {
            object? itemControl = null;
            foreach (var item in DependenciesListBox.Items)
            {
                if (item is TextBlock block && block.Tag is IOCObj iocObj && iocObj.Key.Equals(key))
                {
                    itemControl = item;
                }
            }
            if (itemControl is not null)
            {
                DependenciesListBox.Items.Remove(itemControl);
            }
        }

    }
}
