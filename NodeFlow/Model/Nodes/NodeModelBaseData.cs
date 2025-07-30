using Serein.Library;
using Serein.Library.Api;
using Serein.Library.NodeGenerator;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Mime;
using System.Threading;

namespace Serein.NodeFlow.Model.Nodes
{

    /// <summary>
    /// 节点基类（数据）
    /// </summary>
    [FlowDataProperty(ValuePath = NodeValuePath.Node)]
    public abstract partial class NodeModelBase : IFlowNode
    {
        /// <summary>
        /// 节点运行环境
        /// </summary>
        [DataInfo(IsProtection = true)]
        private IFlowEnvironment _env;

        /// <summary>
        /// 标识节点对象全局唯一
        /// </summary>
        [DataInfo(IsProtection = true)]
        private string _guid;

        /// <summary>
        /// 描述节点对应的控件类型
        /// </summary>
        [DataInfo(IsProtection = true)]
        private NodeControlType _controlType;

        /// <summary>
        /// 所属画布
        /// </summary>
        [DataInfo(IsProtection = true)]
        private FlowCanvasDetails _canvasDetails ;

        /// <summary>
        /// 在画布中的位置
        /// </summary>
        [DataInfo(IsProtection = true)] 
        private PositionOfUI _position ;

        /// <summary>
        /// 显示名称
        /// </summary>
        [DataInfo]
        private string _displayName;

        /// <summary>
        /// 是否公开
        /// </summary>
        [DataInfo(IsNotification = true)]
        private bool _isPublic;

       /* /// <summary>
        /// 是否保护参数
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private bool _isProtectionParameter;*/

        /// <summary>
        /// 附加的调试功能
        /// </summary>
        [DataInfo(IsProtection = true)] 
        private NodeDebugSetting _debugSetting ;

        /// <summary>
        /// 方法描述。包含参数信息。不包含Method与委托，如若需要调用对应的方法，需要通过MethodName从环境中获取委托进行调用。
        /// </summary>
        [DataInfo] 
        private MethodDetails _methodDetails ;
    }


    public abstract partial class NodeModelBase : ISereinFlow
    {
        /// <summary>
        /// 是否为基础节点
        /// </summary>
        public virtual bool IsBase { get; } = false;

        /// <summary>
        /// 可以放置多少个节点
        /// </summary>
        public virtual int MaxChildrenCount { get; } = 0;

        /// <summary>
        /// 创建一个节点模型的基类实例
        /// </summary>
        /// <param name="environment"></param>
        public NodeModelBase(IFlowEnvironment environment)
        {
            PreviousNodes = new Dictionary<ConnectionInvokeType, List<IFlowNode>>();
            SuccessorNodes = new Dictionary<ConnectionInvokeType, List<IFlowNode>>();
            NeedResultNodes = new Dictionary<ConnectionArgSourceType, List<IFlowNode>>();

            foreach (ConnectionInvokeType ctType in NodeStaticConfig.ConnectionTypes)
            {
                PreviousNodes[ctType] = new List<IFlowNode>();
                SuccessorNodes[ctType] = new List<IFlowNode>();
            }
            
            foreach (ConnectionArgSourceType ctType in NodeStaticConfig.ConnectionArgSourceTypes)
            {
                NeedResultNodes[ctType] = new List<IFlowNode>();
            }

            ChildrenNode = new List<IFlowNode>();
            DebugSetting = new NodeDebugSetting(this);
            this.Env = environment;
        }


        /// <summary>
        /// 不同分支的父节点（流程调用）
        /// </summary>
        public Dictionary<ConnectionInvokeType, List<IFlowNode>> PreviousNodes { get; }

        /// <summary>
        /// 不同分支的子节点（流程调用）
        /// </summary>
        public Dictionary<ConnectionInvokeType, List<IFlowNode>> SuccessorNodes { get; set; }
       
        /// <summary>
        /// 需要该节点返回值作为入参参数的节点集合
        /// </summary>
        public Dictionary<ConnectionArgSourceType, List<IFlowNode>> NeedResultNodes { get;}

        /// <summary>
        /// 该节点的容器节点
        /// </summary>
        public IFlowNode ContainerNode {  get; set; } = null;

        /// <summary>
        /// 该节点的子项节点（如果该节点是容器节点，那就会有这个参数）
        /// </summary>
        public List<IFlowNode> ChildrenNode {  get; }

        /// <summary>
        /// 节点公开状态发生改变
        /// </summary>
        partial void OnIsPublicChanged(bool oldValue, bool newValue)
        {
            var list = CanvasDetails.PublicNodes.ToList();
            _ = SereinEnv.TriggerEvent(() =>
            {
                if (newValue)
                {
                    // 公开节点
                    if (!CanvasDetails.PublicNodes.Contains(this))
                    {
                        list.Add(this);
                        CanvasDetails.PublicNodes = list;
                    }
                }
                else
                {
                    // 取消公开
                    if (CanvasDetails.PublicNodes.Contains(this))
                    {
                        list.Remove(this);
                        CanvasDetails.PublicNodes = list;
                    }
                }
            });
           
        }


    }
 }


