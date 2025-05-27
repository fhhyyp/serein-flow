using Serein.Library;
using Serein.Workbench.Node.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Api
{
    /// <summary>
    /// 流程画布
    /// </summary>
    public interface IFlowCanvas
    {
        /// <summary>
        /// 画布标识
        /// </summary>
        string Guid { get; }

        /// <summary>
        /// 画布名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 数据
        /// </summary>
        FlowCanvasDetails Model { get; }

        /// <summary>
        /// 移除节点
        /// </summary>
        void Remove(NodeControlBase nodeControl);

        /// <summary>
        /// 添加节点
        /// </summary>
        void Add(NodeControlBase nodeControl);

        /// <summary>
        /// 创建节点之间方法调用关系
        /// </summary>
        /// <param name="fromNodeControl">调用顺序中的起始节点</param>
        /// <param name="toNodeControl">下一节点</param>
        /// <param name="type">调用类型</param>
        void CreateInvokeConnection(NodeControlBase fromNodeControl, NodeControlBase toNodeControl, ConnectionInvokeType type);

        /// <summary>
        /// 移除节点之间的调用关系
        /// </summary>
        /// <param name="fromNodeControl">调用顺序中的起始节点</param>
        /// <param name="toNodeControl">下一节点</param>
        void RemoveInvokeConnection(NodeControlBase fromNodeControl, NodeControlBase toNodeControl);

        /// <summary>
        /// 创建节点之间的参数传递关系
        /// </summary>
        /// <param name="fromNodeControl">参数来源节点</param>
        /// <param name="toNodeControl">获取参数的节点</param>
        /// <param name="type">指示参数是如何获取的</param>
        /// <param name="index">作用在节点的第几个入参</param>
        void CreateArgConnection(NodeControlBase fromNodeControl, NodeControlBase toNodeControl, ConnectionArgSourceType type, int index);

        /// <summary>
        /// 移除节点之间的参数传递关系
        /// </summary>
        /// <param name="fromNodeControl">参数来源节点</param>
        /// <param name="toNodeControl">获取参数的节点</param>
        /// <param name="index">移除节点第几个入参</param>
        void RemoveArgConnection(NodeControlBase fromNodeControl, NodeControlBase toNodeControl, int index);
    }
}
