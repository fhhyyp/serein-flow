using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace Serein.Library
{

    /// <summary>
    /// 节点基类（数据）：条件控件，动作控件，条件区域，动作区域
    /// </summary>
    [AutoProperty(ValuePath = nameof(NodeModelBase))] // 是否更名为 NodeProperty?
    public abstract partial class NodeModelBase : IDynamicFlowNode
    {
        

        [PropertyInfo(IsProtection = true)]
        private IFlowEnvironment _env;

        /// <summary>
        /// 在画布中的位置
        /// </summary>
        [PropertyInfo(IsProtection = true)] 
        private PositionOfUI _position ;

        /// <summary>
        /// 附加的调试功能
        /// </summary>
        [PropertyInfo(IsProtection = true)] 
        private NodeDebugSetting _debugSetting ;

        /// <summary>
        /// 描述节点对应的控件类型
        /// </summary>
        [PropertyInfo(IsProtection = true)] 
        private NodeControlType _controlType ;

        /// <summary>
        /// 方法描述。不包含Method与委托，需要通过MethodName从环境中获取委托进行调用。
        /// </summary>
        [PropertyInfo(IsProtection = true)] 
        private MethodDetails _methodDetails ;

        /// <summary>
        /// 标识节点对象全局唯一
        /// </summary>
        [PropertyInfo(IsProtection = true)] 
        private string _guid ;

        /// <summary>
        /// 显示名称
        /// </summary>
        [PropertyInfo] 
        private string _displayName ; 

        /// <summary>
        /// 是否为起点控件
        /// </summary>
        [PropertyInfo] 
        private bool _isStart ;

        /// <summary>
        /// 运行时的上一节点
        /// </summary>
        [PropertyInfo] 
        private NodeModelBase _previousNode ;

       

        /// <summary>
        /// 当前节点执行完毕后需要执行的下一个分支的类别
        /// </summary>
        [PropertyInfo]
        private ConnectionType _nextOrientation  = ConnectionType.None;

        /// <summary>
        /// 运行时的异常信息（仅在 FlowState 为 Error 时存在对应值）
        /// </summary>
        [PropertyInfo]
        private Exception _runingException ;

    }





    public abstract partial class NodeModelBase : IDynamicFlowNode
    {
        public NodeModelBase(IFlowEnvironment environment)
        {
            PreviousNodes = new Dictionary<ConnectionType, List<NodeModelBase>>();
            SuccessorNodes = new Dictionary<ConnectionType, List<NodeModelBase>>();
            foreach (ConnectionType ctType in NodeStaticConfig.ConnectionTypes)
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
        public Dictionary<ConnectionType, List<NodeModelBase>> PreviousNodes { get; }

        /// <summary>
        /// 不同分支的子节点
        /// </summary>
        public Dictionary<ConnectionType, List<NodeModelBase>> SuccessorNodes { get; }

        /// <summary>
        /// 控制FlowData在同一时间只会被同一个线程更改。
        /// </summary>
        private readonly ReaderWriterLockSlim _flowDataLock = new ReaderWriterLockSlim();
        private object _flowData;
        /// <summary>
        /// 当前传递数据（执行了节点对应的方法，才会存在值）。
        /// </summary>
        protected object FlowData
        {
            get
            {
                _flowDataLock.EnterReadLock();
                try
                {
                    return _flowData;
                }
                finally
                {
                    _flowDataLock.ExitReadLock();
                }
            }
            set
            {
                _flowDataLock.EnterWriteLock();
                try
                {
                    _flowData = value;
                }
                finally
                {
                    _flowDataLock.ExitWriteLock();
                }
            }
        }
    }



    /*
    /// <summary>
    /// 节点基类（数据）：条件控件，动作控件，条件区域，动作区域
    /// </summary>
    public abstract partial class NodeModelBase : IDynamicFlowNode
    {
        
        /// <summary>
        /// 节点保留对环境的引用，因为需要在属性更改时通知
        /// </summary>
        public IFlowEnvironment Env { get; }

        /// <summary>
        /// 在画布中的位置
        /// </summary>
        public PositionOfUI Position { get; set; }

        /// <summary>
        /// 附加的调试功能
        /// </summary>
        public NodeDebugSetting DebugSetting { get; set; }

        /// <summary>
        /// 描述节点对应的控件类型
        /// </summary>
        public NodeControlType ControlType { get; set; }

        /// <summary>
        /// 方法描述。不包含Method与委托，需要通过MethodName从环境中获取委托进行调用。
        /// </summary>
        public MethodDetails MethodDetails { get; set; }

        /// <summary>
        /// 标识节点对象全局唯一
        /// </summary>
        public string Guid { get; set; } 

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 是否为起点控件
        /// </summary>
        public bool IsStart { get; set; }

        /// <summary>
        /// 运行时的上一节点
        /// </summary>
        public NodeModelBase PreviousNode { get; set; }

        /// <summary>
        /// 当前节点执行完毕后需要执行的下一个分支的类别
        /// </summary>
        public ConnectionType NextOrientation { get; set; } = ConnectionType.None;

        /// <summary>
        /// 运行时的异常信息（仅在 FlowState 为 Error 时存在对应值）
        /// </summary>
        public Exception RuningException { get; set; } = null;


    }*/



    /// <summary>
    /// 节点基类（数据）：条件控件，动作控件，条件区域，动作区域
    /// </summary>
    //public class NodeModelBaseBuilder 
    //{
    //    public NodeModelBaseBuilder(NodeModelBase builder)
    //    {
    //        this.ControlType = builder.ControlType;
    //        this.MethodDetails = builder.MethodDetails;
    //        this.Guid = builder.Guid;
    //        this.DisplayName = builder.DisplayName;
    //        this.IsStart = builder.IsStart;
    //        this.PreviousNode = builder.PreviousNode;
    //        this.PreviousNodes = builder.PreviousNodes;
    //        this.SucceedBranch = builder.SucceedBranch;
    //        this.FailBranch = builder.FailBranch;
    //        this.ErrorBranch = builder.ErrorBranch;
    //        this.UpstreamBranch = builder.UpstreamBranch;
    //        this.FlowState = builder.FlowState;
    //        this.RuningException = builder.RuningException;
    //        this.FlowData = builder.FlowData;
    //    }



    //    /// <summary>
    //    /// 节点对应的控件类型
    //    /// </summary>
    //    public NodeControlType ControlType { get; }

    //    /// <summary>
    //    /// 方法描述，对应DLL的方法
    //    /// </summary>
    //    public MethodDetails MethodDetails { get; }

    //    /// <summary>
    //    /// 节点guid
    //    /// </summary>
    //    public string Guid { get; }

    //    /// <summary>
    //    /// 显示名称
    //    /// </summary>
    //    public string DisplayName { get;}

    //    /// <summary>
    //    /// 是否为起点控件
    //    /// </summary>
    //    public bool IsStart { get;  }

    //    /// <summary>
    //    /// 运行时的上一节点
    //    /// </summary>
    //    public NodeModelBase? PreviousNode { get;  }

    //    /// <summary>
    //    /// 上一节点集合
    //    /// </summary>
    //    public List<NodeModelBase> PreviousNodes { get; } = [];

    //    /// <summary>
    //    /// 下一节点集合（真分支）
    //    /// </summary>
    //    public List<NodeModelBase> SucceedBranch { get; } = [];

    //    /// <summary>
    //    /// 下一节点集合（假分支）
    //    /// </summary>
    //    public List<NodeModelBase> FailBranch { get; } = [];

    //    /// <summary>
    //    /// 异常分支
    //    /// </summary>
    //    public List<NodeModelBase> ErrorBranch { get;  } = [];

    //    /// <summary>
    //    /// 上游分支
    //    /// </summary>
    //    public List<NodeModelBase> UpstreamBranch { get;  } = [];

    //    /// <summary>
    //    /// 当前执行状态（进入真分支还是假分支，异常分支在异常中确定）
    //    /// </summary>
    //    public FlowStateType FlowState { get; set; } = FlowStateType.None;

    //    /// <summary>
    //    /// 运行时的异常信息（仅在 FlowState 为 Error 时存在对应值）
    //    /// </summary>
    //    public Exception RuningException { get; set; } = null;

    //    /// <summary>
    //    /// 当前传递数据（执行了节点对应的方法，才会存在值）
    //    /// </summary>
    //    public object? FlowData { get; set; } = null;
    //}


}

