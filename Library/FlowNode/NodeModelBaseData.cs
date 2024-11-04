using Serein.Library.Api;
using Serein.Library.NodeGenerator;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Mime;
using System.Threading;

namespace Serein.Library
{

    /// <summary>
    /// 节点基类（数据）：条件控件，动作控件，条件区域，动作区域
    /// </summary>
    [NodeProperty(ValuePath = NodeValuePath.None)]
    public abstract partial class NodeModelBase : IDynamicFlowNode
    {
        /// <summary>
        /// 节点运行环境
        /// </summary>
        [PropertyInfo(IsProtection = true)]
        private IFlowEnvironment _env;

        /// <summary>
        /// 标识节点对象全局唯一
        /// </summary>
        [PropertyInfo(IsProtection = true)]
        private string _guid;

        /// <summary>
        /// 描述节点对应的控件类型
        /// </summary>
        [PropertyInfo(IsProtection = true)]
        private NodeControlType _controlType;

        /// <summary>
        /// 在画布中的位置
        /// </summary>
        [PropertyInfo(IsProtection = true)] 
        private PositionOfUI _position ;

        /// <summary>
        /// 显示名称
        /// </summary>
        [PropertyInfo]
        private string _displayName;

        /// <summary>
        /// 是否为起点控件
        /// </summary>
        [PropertyInfo]
        private bool _isStart;

        /// <summary>
        /// 附加的调试功能
        /// </summary>
        [PropertyInfo(IsProtection = true)] 
        private NodeDebugSetting _debugSetting ;

        /// <summary>
        /// 方法描述。不包含Method与委托，需要通过MethodName从环境中获取委托进行调用。
        /// </summary>
        [PropertyInfo(IsProtection = true)] 
        private MethodDetails _methodDetails ;
    }


    public abstract partial class NodeModelBase : IDynamicFlowNode
    {
        /// <summary>
        /// 实体节点创建完成后调用的方法，调用时间早于 LoadInfo() 方法
        /// </summary>
        public abstract void OnCreating();

        public NodeModelBase(IFlowEnvironment environment)
        {
            PreviousNodes = new Dictionary<ConnectionInvokeType, List<NodeModelBase>>();
            SuccessorNodes = new Dictionary<ConnectionInvokeType, List<NodeModelBase>>();
            foreach (ConnectionInvokeType ctType in NodeStaticConfig.ConnectionTypes)
            {
                PreviousNodes[ctType] = new List<NodeModelBase>();
                SuccessorNodes[ctType] = new List<NodeModelBase>();
            }
            DebugSetting = new NodeDebugSetting(this);
            this.Env = environment;
        }


        /// <summary>
        /// 不同分支的父节点
        /// </summary>
        public Dictionary<ConnectionInvokeType, List<NodeModelBase>> PreviousNodes { get; }

        /// <summary>
        /// 不同分支的子节点
        /// </summary>
        public Dictionary<ConnectionInvokeType, List<NodeModelBase>> SuccessorNodes { get; }

    }
 }


