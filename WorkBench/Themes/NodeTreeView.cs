using Serein.NodeFlow.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using Serein.Library.Enums;

namespace Serein.WorkBench.Themes
{
    public class NodeTreeView : TreeView
    {
        public NodeTreeView()
        {
            this.ItemContainerGenerator.StatusChanged += OnStatusChanged;
        }

        private void OnStatusChanged(object sender, EventArgs e)
        {
            if (this.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                foreach (var item in Items)
                {
                    var treeViewItem = (TreeViewItem)this.ItemContainerGenerator.ContainerFromItem(item);
                    if (treeViewItem != null)
                    {
                        treeViewItem.Expanded += TreeViewItem_Expanded;
                        ApplyColor(treeViewItem, item as NodeModelBase);
                    }
                }
            }
        }

        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (sender is TreeViewItem item && item.DataContext is NodeModelBase node)
            {
                if (item.Items.Count == 0) // 懒加载
                {
                    foreach (var childNode in node.SuccessorNodes[ConnectionType.Upstream])
                    {
                        item.Items.Add(childNode);
                    }
                }
            }
        }

        private void ApplyColor(TreeViewItem item, NodeModelBase node)
        {
            // 根据 ControlType 设置颜色
            switch (node.ControlType)
            {
                case NodeControlType.Flipflop:
                    item.Background = Brushes.LightGreen;
                    break;
                case NodeControlType.Action:
                    item.Background = Brushes.LightCoral;
                    break;
                // 添加更多条件
                default:
                    item.Background = Brushes.Transparent;
                    break;
            }
        }
    }

}
