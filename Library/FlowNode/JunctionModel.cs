using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.FlowNode
{


    /*
     * 有1个Execute
     * 有1个NextStep
     * 有0~65535个入参 ushort
     * 有1个ReturnData(void方法返回null)
     * 
     * Execute： // 执行这个方法
     *   只接受 NextStep 的连接
     * ArgData：
     *   互相之间不能连接，只能接受 Execute、ReturnData 的连接
     *      Execute：表示从 Execute所在节点 获取数据
     *      ReturnData： 表示从对应节点获取数据
     * ReturnData:
     *     只能发起主动连接，且只能连接到 ArgData
     * NextStep
     *      只能连接连接 Execute
     *      
     */

    /// <summary>
    /// 依附于节点的连接点
    /// </summary>
    public class JunctionModel
    {
        public JunctionModel(NodeModelBase NodeModel, JunctionType JunctionType)
        {
            Guid = System.Guid.NewGuid().ToString();
            this.NodeModel = NodeModel;
            this.JunctionType = JunctionType;
        }
        /// <summary>
        /// 用于标识连接点
        /// </summary>
        public string Guid { get; }

        /// <summary>
        /// 标识连接点的类型
        /// </summary>
        public JunctionType JunctionType { get; }

        /// <summary>
        /// 连接点依附的节点
        /// </summary>
        public NodeModelBase NodeModel { get; }
    }
}
