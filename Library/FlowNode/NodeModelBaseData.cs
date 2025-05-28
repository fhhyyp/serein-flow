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
    /// 节点基类（数据）
    /// </summary>
    [NodeProperty(ValuePath = NodeValuePath.Node)]
    public abstract partial class NodeModelBase : INotifyPropertyChanged, IDynamicFlowNode
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
        /// 所属画布
        /// </summary>
        [PropertyInfo(IsProtection = true)]
        private FlowCanvasDetails _canvasDetails ;

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
        /// 是否公开
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private bool _isPublic;

       /* /// <summary>
        /// 是否保护参数
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private bool _isProtectionParameter;*/

        /// <summary>
        /// 附加的调试功能
        /// </summary>
        [PropertyInfo(IsProtection = true)] 
        private NodeDebugSetting _debugSetting ;

        /// <summary>
        /// 方法描述。包含参数信息。不包含Method与委托，如若需要调用对应的方法，需要通过MethodName从环境中获取委托进行调用。
        /// </summary>
        [PropertyInfo] 
        private MethodDetails _methodDetails ;
    }


    public abstract partial class NodeModelBase : IDynamicFlowNode
    {
        /// <summary>
        /// 是否为基础节点
        /// </summary>
        public virtual bool IsBase { get; } = false;

        /// <summary>
        /// 可以放置多少个节点
        /// </summary>
        public virtual int MaxChildrenCount { get; } = 0;

        public NodeModelBase(IFlowEnvironment environment)
        {
            PreviousNodes = new Dictionary<ConnectionInvokeType, List<NodeModelBase>>();
            SuccessorNodes = new Dictionary<ConnectionInvokeType, List<NodeModelBase>>();
            foreach (ConnectionInvokeType ctType in NodeStaticConfig.ConnectionTypes)
            {
                PreviousNodes[ctType] = new List<NodeModelBase>();
                SuccessorNodes[ctType] = new List<NodeModelBase>();
            }
            ChildrenNode = new List<NodeModelBase>();
            DebugSetting = new NodeDebugSetting(this);
            this.Env = environment;
        }


        /// <summary>
        /// 不同分支的父节点（流程调用）
        /// </summary>
        public Dictionary<ConnectionInvokeType, List<NodeModelBase>> PreviousNodes { get; }

        /// <summary>
        /// 不同分支的子节点（流程调用）
        /// </summary>
        public Dictionary<ConnectionInvokeType, List<NodeModelBase>> SuccessorNodes { get; set; }

        /// <summary>
        /// 该节点的容器节点
        /// </summary>
        public NodeModelBase ContainerNode {  get; set; } = null;

        /// <summary>
        /// 该节点的子项节点（如果该节点是容器节点，那就会有这个参数）
        /// </summary>
        public List<NodeModelBase> ChildrenNode {  get; }

        /// <summary>
        /// 节点公开状态发生改变
        /// </summary>
        partial void OnIsPublicChanged(bool oldValue, bool newValue)
        {
            if (newValue)
            {
                // 公开节点
                if (!CanvasDetails.PublicNodes.Contains(this))
                {
                    CanvasDetails.PublicNodes.Add(this);
                }
            }
            else
            {
                // 取消公开
                if (CanvasDetails.PublicNodes.Contains(this))
                {
                    CanvasDetails.PublicNodes.Remove(this);
                }
            }
        }


    }
 }


