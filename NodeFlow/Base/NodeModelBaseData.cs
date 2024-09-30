using Serein.Library.Api;
using Serein.Library.Entity;
using Serein.Library.Enums;

namespace Serein.NodeFlow.Base
{
    /// <summary>
    /// 节点基类（数据）：条件控件，动作控件，条件区域，动作区域
    /// </summary>
    public abstract partial class NodeModelBase : IDynamicFlowNode
    {
        
        public NodeModelBase()
        {
            PreviousNodes = [];
            SuccessorNodes = [];
            foreach (ConnectionType ctType in NodeStaticConfig.ConnectionTypes)
            {
                PreviousNodes[ctType] = [];
                SuccessorNodes[ctType] = [];
            }
            DebugSetting = new NodeDebugSetting();
        }


        /// <summary>
        /// 调试功能
        /// </summary>
        public NodeDebugSetting DebugSetting { get; set; }

        /// <summary>
        /// 节点对应的控件类型
        /// </summary>
        public NodeControlType ControlType { get; set; }

        /// <summary>
        /// 方法描述，对应DLL的方法
        /// </summary>
        public MethodDetails MethodDetails { get; set; }

        /// <summary>
        /// 节点guid
        /// </summary>
        public string Guid { get; set; } = string.Empty;

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
        public NodeModelBase? PreviousNode { get; set; }

        /// <summary>
        /// 不同分支的父节点
        /// </summary>
        public Dictionary<ConnectionType,List<NodeModelBase>> PreviousNodes { get; }
        
        /// <summary>
        /// 不同分支的子节点
        /// </summary>
        public Dictionary<ConnectionType,List<NodeModelBase>> SuccessorNodes { get; }

        /// <summary>
        /// 当前节点执行完毕后需要执行的下一个分支的类别
        /// </summary>
        public ConnectionType NextOrientation { get; set; } = ConnectionType.None;

        /// <summary>
        /// 运行时的异常信息（仅在 FlowState 为 Error 时存在对应值）
        /// </summary>
        public Exception? RuningException { get; set; } = null;


        /// <summary>
        /// 控制FlowData在同一时间只会被同一个线程更改。
        /// </summary>
        private readonly ReaderWriterLockSlim _flowDataLock = new ReaderWriterLockSlim();
        private object? _flowData;
        /// <summary>
        /// 当前传递数据（执行了节点对应的方法，才会存在值）。
        /// </summary>
        protected object? FlowData
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

