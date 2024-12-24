using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Api
{
    /// <summary>
    /// 约束具有容器功能的节点应该有什么方法
    /// </summary>
    public interface INodeContainer
    {
        /// <summary>
        /// 放置一个节点
        /// </summary>
        /// <param name="nodeModel"></param>
        void PlaceNode(NodeModelBase nodeModel);

        /// <summary>
        /// 取出一个节点
        /// </summary>
        /// <param name="nodeModel"></param>
        void TakeOutNode(NodeModelBase nodeModel);

        /// <summary>
        /// 取出所有节点（用于删除容器）
        /// </summary>
        void TakeOutAll();
    }
}
