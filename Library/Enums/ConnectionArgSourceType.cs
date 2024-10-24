using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library
{

    /// <summary>
    /// 节点对应方法的入参来源
    /// </summary>
    public enum ConnectionArgSourceType
    {
        /// <summary>
        /// （连接自身）从上一节点获取数据
        /// </summary>
        GetPreviousNodeData,
        /// <summary>
        /// 从指定节点获取数据
        /// </summary>
        GetOtherNodeData,
        /// <summary>
        /// 立刻执行某个节点获取其数据
        /// </summary>
        GetOtherNodeDataOfInvoke,
    }
}
