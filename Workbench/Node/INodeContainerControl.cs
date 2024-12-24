using Serein.Workbench.Node.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Node
{

    /// <summary>
    /// 约束具有容器功能的节点控件应该有什么方法
    /// </summary>
    public interface INodeContainerControl
    {
        /// <summary>
        /// 放置一个节点
        /// </summary>
        /// <param name="nodeControl"></param>
        void PlaceNode(NodeControlBase nodeControl);

        /// <summary>
        /// 取出一个节点
        /// </summary>
        /// <param name="nodeControl"></param>
        void TakeOutNode(NodeControlBase nodeControl);

        /// <summary>
        /// 取出所有节点（用于删除容器）
        /// </summary>
        void TakeOutAll();
    }
}
