using Serein.Library.Enums;
using Serein.NodeFlow.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace Serein.WorkBench.Themes
{
    /// <summary>
    /// LazyTreeView.xaml 的交互逻辑
    /// </summary>
    public partial class LazyTreeView : UserControl
    {
        public ObservableCollection<NodeModelBase> RootNodes { get; set; }

        public LazyTreeView()
        {
            InitializeComponent();
            RootNodes = new ObservableCollection<NodeModelBase>();
            treeView.DataContext = this;
        }

        private void treeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is NodeModelBase node)
            {
                // 在这里设置 Expanded 事件
                var treeViewItem = (TreeViewItem)treeView.ItemContainerGenerator.ContainerFromItem(node);
                treeViewItem.Expanded += (s, args) => LoadChildren(node, treeViewItem);
            }
        }
        private void LoadChildren(NodeModelBase node, TreeViewItem treeViewItem)
        {
            // 懒加载逻辑
            if (node.SuccessorNodes.Count > 0)
            {
                treeViewItem.Items.Clear();
                foreach (var child in node.SuccessorNodes[ConnectionType.IsSucceed]) // 根据类型加载子项
                {
                    treeViewItem.Items.Add(child);
                }
            }
        }
    }
}
