using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Serein.Library;
using Serein.Workbench.Avalonia.Api;
using Serein.Workbench.Avalonia.Custom.Node.Views;
using Serein.Workbench.Avalonia.Custom.Views;
using Serein.Workbench.Avalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Avalonia.Custom.Node.ViewModels
{
    /// <summary>
    /// 节点ViewModel基类
    /// </summary>
    internal abstract class NodeViewModelBase : ViewModelBase
    {
        internal abstract NodeModelBase NodeModelBase { get; set; }

        private Canvas NodeCanvas;

        /// <summary>
        /// 如果该节点放置在了某个容器节点，就会记录这个容器节点
        /// </summary>
        private INodeContainerControl NodeContainerControl { get; }

        public NodeModelBase NodeModel { get; set; }

        /// <summary>
        /// 记录与该节点控件有关的所有连接
        /// </summary>
        private readonly List<NodeConnectionLineView> connectionControls = new List<NodeConnectionLineView>();

        //public NodeControlViewModelBase ViewModel { get; set; }



        public void SetNodeModel(NodeModelBase nodeModel) => this.NodeModel = nodeModel;
        

        /// <summary>
        /// 添加与该节点有关的连接后，记录下来
        /// </summary>
        /// <param name="connection"></param>
        public void AddCnnection(NodeConnectionLineView connection)
        {
            connectionControls.Add(connection);
        }

        /// <summary>
        /// 删除了连接之后，还需要从节点中的记录移除
        /// </summary>
        /// <param name="connection"></param>
        public void RemoveConnection(NodeConnectionLineView connection)
        {
            connectionControls.Remove(connection);
            //connection.Remote();
        }

        /// <summary>
        /// 删除所有连接
        /// </summary>
        public void RemoveAllConection()
        {
            foreach (var connection in this.connectionControls)
            {
                //connection.Remote();
            }
        }

        /// <summary>
        /// 更新与该节点有关的数据
        /// </summary>
        public void UpdateLocationConnections()
        {
            foreach (var connection in this.connectionControls)
            {
                //connection.RefreshLine(); // 主动更新连线位置
            }
        }


        /// <summary>
        /// 设置绑定：
        /// Canvas.X and Y ： 画布位置
        /// </summary>
        public void SetBinding()
        {
            /* // 绑定 Canvas.Left
             Binding leftBinding = new Binding("X")
             {
                 Source = ViewModel.NodeModel.Position, // 如果 X 属性在当前 DataContext 中
                 Mode = BindingMode.TwoWay
             };
             BindingOperations.Apply(this, Canvas.LeftProperty, leftBinding);

             // 绑定 Canvas.Top
             Binding topBinding = new Binding("Y")
             {
                 Source = ViewModel.NodeModel.Position, // 如果 Y 属性在当前 DataContext 中
                 Mode = BindingMode.TwoWay
             };
             BindingOperations.SetBinding(this, Canvas.TopProperty, topBinding);*/
        }

        /// <summary>
        /// 穿透视觉树获取指定类型的第一个元素
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="parent"></param>
        /// <returns></returns>
        //protected T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        //{
        //    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        //    {
        //        var child = VisualTreeHelper.GetChild(parent, i);
        //        if (child is T typedChild)
        //        {
        //            return typedChild;
        //        }

        //        var childOfChild = FindVisualChild<T>(child);
        //        if (childOfChild != null)
        //        {
        //            return childOfChild;
        //        }
        //    }
        //    return null;
        //}

    }
}
