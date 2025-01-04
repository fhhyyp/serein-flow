﻿using Avalonia.Controls;
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
    public class NodeControlBase : UserControl
    {
        protected NodeControlBase()
        {
            this.Background = Brushes.Transparent;
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
