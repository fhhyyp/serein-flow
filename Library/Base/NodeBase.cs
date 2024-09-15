using Serein.Library.Entity;
using Serein.Library.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Serein.Library.Base
{
    public abstract class NodeBase
    {
        /// <summary>
        /// 节点类型
        /// </summary>
        public abstract NodeControlType ControlType { get; set; }

        /// <summary>
        /// 方法描述，对应DLL的方法
        /// </summary>
        public abstract MethodDetails MethodDetails { get; set; }

        /// <summary>
        /// 节点guid
        /// </summary>
        public abstract string Guid { get; set; }

        /// <summary>
        /// 显示名称
        /// </summary>
        public abstract string DisplayName { get; set; }

        /// <summary>
        /// 是否为起点控件
        /// </summary>
        public abstract bool IsStart { get; set; }

        /// <summary>
        /// 运行时的上一节点
        /// </summary>
        public abstract NodeBase PreviousNode { get; set; }

        /// <summary>
        /// 上一节点集合
        /// </summary>
        public abstract List<NodeBase> PreviousNodes { get; set; }

        /// <summary>
        /// 下一节点集合（真分支）
        /// </summary>
        public abstract List<NodeBase> SucceedBranch { get; set; }

        /// <summary>
        /// 下一节点集合（假分支）
        /// </summary>
        public abstract List<NodeBase> FailBranch { get; set; }

        /// <summary>
        /// 异常分支
        /// </summary>
        public abstract List<NodeBase> ErrorBranch { get; set; }

        /// <summary>
        /// 上游分支
        /// </summary>
        public abstract List<NodeBase> UpstreamBranch { get; set; } 

        /// <summary>
        /// 当前执行状态（进入真分支还是假分支，异常分支在异常中确定）
        /// </summary>
        public abstract FlowStateType FlowState { get; set; } 

        /// <summary>
        /// 运行时的异常信息（仅在 FlowState 为 Error 时存在对应值）
        /// </summary>
        public abstract Exception RuningException { get; set; }

        /// <summary>
        /// 当前传递数据（执行了节点对应的方法，才会存在值）
        /// </summary>
        public abstract object FlowData { get; set; }

    }
}
