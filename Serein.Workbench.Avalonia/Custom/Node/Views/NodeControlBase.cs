using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Serein.Library;
using Serein.Workbench.Avalonia.Api;
using Serein.Workbench.Avalonia.Custom.Views;
using Serein.Workbench.Avalonia.Model;
using Serein.Workbench.Avalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Avalonia.Custom.Node.Views
{
    public abstract class NodeControlBase : UserControl
    {
        /// <summary>
        /// 记录与该节点控件有关的所有连接
        /// </summary>
        private readonly List<NodeConnectionLineControl> connectionControls = new List<NodeConnectionLineControl>();

        protected NodeControlBase()
        {
            this.Background = Brushes.Transparent;
        }


        /// <summary>
        /// 添加与该节点有关的连接后，记录下来
        /// </summary>
        /// <param name="connection"></param>
        public void AddConnection(NodeConnectionLineControl connection)
        {
            connectionControls.Add(connection);
        }

        /// <summary>
        /// 删除了连接之后，还需要从节点中的记录移除
        /// </summary>
        /// <param name="connection"></param>
        public void RemoveConnection(NodeConnectionLineControl connection)
        {
            connectionControls.Remove(connection);
            connection.Remove();
        }

        /// <summary>
        /// 删除所有连接
        /// </summary>
        public void RemoveAllConection()
        {
            foreach (var connection in this.connectionControls)
            {
                connection.Remove();
            }
        }

        /// <summary>
        /// 更新与该节点有关的数据
        /// </summary>
        public void UpdateLocationConnections()
        {
            foreach (var connection in this.connectionControls)
            {
                connection.RefreshLineDsiplay(); // 主动更新连线位置
            }
        }



        /// <summary>
        /// 放置在某个节点容器中
        /// </summary>
        public void PlaceToContainer(INodeContainerControl nodeContainerControl)
        {
            //this.nodeContainerControl = nodeContainerControl;
            //NodeCanvas.Children.Remove(this); // 临时从画布上移除
            //var result = nodeContainerControl.PlaceNode(this);
            //if (!result) // 检查是否放置成功，如果不成功，需要重新添加回来
            //{
            //    NodeCanvas.Children.Add(this); // 从画布上移除

            //}
        }

        /// <summary>
        /// 从某个节点容器取出
        /// </summary>
        public void TakeOutContainer()
        {
            //var result = nodeContainerControl.TakeOutNode(this); // 从控件取出
            //if (result) // 移除成功时才添加到画布上
            //{
            //    NodeCanvas.Children.Add(this); // 重新添加到画布上
            //    if (nodeContainerControl is NodeControlBase containerControl)
            //    {
            //        NodeModel.Position.X = NodeModel.Position.X + containerControl.Width + 10;
            //        NodeModel.Position.Y = NodeModel.Position.Y;
            //    }
            //}

        }
    }
}
