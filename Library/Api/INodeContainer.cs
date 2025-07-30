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
        /// 容器节点的Guid，与 IFlowNode.Guid 相同
        /// </summary>
        string Guid { get; }
        /// <summary>
        /// 放置一个节点
        /// </summary>
        /// <param name="nodeModel"></param>
        bool PlaceNode(IFlowNode nodeModel);

        /// <summary>
        /// 取出一个节点
        /// </summary>
        /// <param name="nodeModel"></param>
        bool TakeOutNode(IFlowNode nodeModel);

        /// <summary>
        /// 取出所有节点（用于删除容器）
        /// </summary>
        void TakeOutAll();
    }
}
