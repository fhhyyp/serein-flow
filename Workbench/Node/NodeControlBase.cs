﻿using Serein.Library;
using Serein.Library.Api;
using Serein.Workbench.Node.ViewModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Serein.Workbench.Node.View
{

    /// <summary>
    /// 节点控件基类（控件）
    /// </summary>
    public abstract class NodeControlBase : UserControl, IDynamicFlowNode
    {
        /// <summary>
        /// 节点所在的画布（以后需要将画布封装出来，实现多画布的功能）
        /// </summary>
        public Canvas NodeCanvas { get; set; }
        
        private INodeContainerControl nodeContainerControl;
        /// <summary>
        /// 如果该节点放置在了某个容器节点，就会记录这个容器节点
        /// </summary>
        private INodeContainerControl NodeContainerControl { get; }

        /// <summary>
        /// 记录与该节点控件有关的所有连接
        /// </summary>
        private readonly List<ConnectionControl> connectionControls = new List<ConnectionControl>();

        public NodeControlViewModelBase ViewModel { get; set; }


        protected NodeControlBase()
        {
            this.Background = Brushes.Transparent;
        }

        protected NodeControlBase(NodeControlViewModelBase viewModelBase)
        {
            ViewModel = viewModelBase;
            this.Background = Brushes.Transparent;
            this.DataContext = viewModelBase;
            SetBinding();
        }

        /// <summary>
        /// 放置在某个节点容器中
        /// </summary>
        public void PlaceToContainer(INodeContainerControl nodeContainerControl)
        {
            this.nodeContainerControl = nodeContainerControl;
            NodeCanvas.Children.Remove(this); // 临时从画布上移除
            var result = nodeContainerControl.PlaceNode(this);
            if (!result) // 检查是否放置成功，如果不成功，需要重新添加回来
            {
                NodeCanvas.Children.Add(this); // 从画布上移除

            }
        }

        /// <summary>
        /// 从某个节点容器取出
        /// </summary>
        public void TakeOutContainer()
        {
            var result = nodeContainerControl.TakeOutNode(this); // 从控件取出
            if (result) // 移除成功时才添加到画布上
            {
                NodeCanvas.Children.Add(this); // 重新添加到画布上
                if (nodeContainerControl is NodeControlBase containerControl)
                {
                    this.ViewModel.NodeModel.Position.X = containerControl.ViewModel.NodeModel.Position.X + containerControl.Width + 10;
                    this.ViewModel.NodeModel.Position.Y = containerControl.ViewModel.NodeModel.Position.Y;
                }
            }
            
        }

        /// <summary>
        /// 添加与该节点有关的连接后，记录下来
        /// </summary>
        /// <param name="connection"></param>
        public void AddCnnection(ConnectionControl connection)
        {
            connectionControls.Add(connection);
        }

        /// <summary>
        /// 删除了连接之后，还需要从节点中的记录移除
        /// </summary>
        /// <param name="connection"></param>
        public void RemoveConnection(ConnectionControl connection)
        {
            connectionControls.Remove(connection);
            connection.Remote();
        }

        /// <summary>
        /// 删除所有连接
        /// </summary>
        public void RemoveAllConection()
        {
            foreach (var connection in this.connectionControls)
            {
                connection.Remote(); 
            }
        }

        /// <summary>
        /// 更新与该节点有关的数据
        /// </summary>
        public void UpdateLocationConnections()
        {
            foreach (var connection in this.connectionControls)
            {
                connection.RefreshLine(); // 主动更新连线位置
            }
        }


        /// <summary>
        /// 设置绑定：
        /// Canvas.X and Y ： 画布位置
        /// </summary>
        public void SetBinding()
        {
            // 绑定 Canvas.Left
            Binding leftBinding = new Binding("X")
            {
                Source = ViewModel.NodeModel.Position, // 如果 X 属性在当前 DataContext 中
                Mode = BindingMode.TwoWay
            };
            BindingOperations.SetBinding(this, Canvas.LeftProperty, leftBinding);

            // 绑定 Canvas.Top
            Binding topBinding = new Binding("Y")
            {
                Source = ViewModel.NodeModel.Position, // 如果 Y 属性在当前 DataContext 中
                Mode = BindingMode.TwoWay
            };
            BindingOperations.SetBinding(this, Canvas.TopProperty, topBinding);
        }

        /// <summary>
        /// 穿透视觉树获取指定类型的第一个元素
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="parent"></param>
        /// <returns></returns>
        protected T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }



    }



 
    //public class FLowNodeObObservableCollection<T> : ObservableCollection<T>
    //{

    //    public void AddRange(IEnumerable<T> items)
    //    {
    //        foreach (var item in items)
    //        {
    //            this.Items.Add(item);
    //        }
    //        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add));
    //    }
    //}
}






