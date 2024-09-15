using Serein.Library.Api;
using Serein.Library.Entity;
using Serein.Library.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Serein.Library.Base
{
    /// <summary>
    /// 节点基类（数据）：条件控件，动作控件，条件区域，动作区域
    /// </summary>
    public abstract partial class NodeModelBase : IDynamicFlowNode
    {
        public NodeControlType ControlType { get; set; }

        /// <summary>
        /// 方法描述，对应DLL的方法
        /// </summary>
        public MethodDetails MethodDetails { get; set; }

        /// <summary>
        /// 节点guid
        /// </summary>
        public string Guid { get; set; }

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 是否为起点控件
        /// </summary>
        public bool IsStart { get; set; }

        /// <summary>
        /// 运行时的上一节点
        /// </summary>
        public NodeModelBase PreviousNode { get; set; }

        /// <summary>
        /// 上一节点集合
        /// </summary>
        public List<NodeModelBase> PreviousNodes { get; set; } = new List<NodeModelBase>();

        /// <summary>
        /// 下一节点集合（真分支）
        /// </summary>
        public List<NodeModelBase> SucceedBranch { get; set; } = new List<NodeModelBase>();

        /// <summary>
        /// 下一节点集合（假分支）
        /// </summary>
        public List<NodeModelBase> FailBranch { get; set; } = new List<NodeModelBase>();

        /// <summary>
        /// 异常分支
        /// </summary>
        public List<NodeModelBase> ErrorBranch { get; set; } = new List<NodeModelBase>();

        /// <summary>
        /// 上游分支
        /// </summary>
        public List<NodeModelBase> UpstreamBranch { get; set; } = new List<NodeModelBase>();

        /// <summary>
        /// 当前执行状态（进入真分支还是假分支，异常分支在异常中确定）
        /// </summary>
        public FlowStateType FlowState { get; set; } = FlowStateType.None;

        /// <summary>
        /// 运行时的异常信息（仅在 FlowState 为 Error 时存在对应值）
        /// </summary>
        public Exception RuningException { get; set; } = null;

        /// <summary>
        /// 当前传递数据（执行了节点对应的方法，才会存在值）
        /// </summary>
        public object FlowData { get; set; } = null;

        public abstract Parameterdata[] GetParameterdatas();
        public virtual NodeInfo ToInfo()
        {
            if (MethodDetails == null) return null;

            var trueNodes = SucceedBranch.Select(item => item.Guid); // 真分支
            var falseNodes = FailBranch.Select(item => item.Guid);// 假分支
            var upstreamNodes = UpstreamBranch.Select(item => item.Guid);// 上游分支
            var errorNodes = ErrorBranch.Select(item => item.Guid);// 异常分支

            // 生成参数列表
            Parameterdata[] parameterData = GetParameterdatas();

            return new NodeInfo
            {
                Guid = Guid,
                MethodName = MethodDetails?.MethodName,
                Label = DisplayName ?? "",
                Type = this.GetType().ToString(),
                TrueNodes = trueNodes.ToArray(),
                FalseNodes = falseNodes.ToArray(),
                UpstreamNodes = upstreamNodes.ToArray(),
                ParameterData = parameterData.ToArray(),
                ErrorNodes = errorNodes.ToArray(),
            };
        }

    }
}
