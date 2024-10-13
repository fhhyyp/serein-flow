using Serein.Library.Entity;
using Serein.Library.Enums;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using static Serein.Library.Utils.ChannelFlowInterrupt;

namespace Serein.Library.Api
{
    #region 环境委托
    /// <summary>
    /// 流程运行完成
    /// </summary>
    /// <param name="eventArgs"></param>
    public delegate void FlowRunCompleteHandler(FlowEventArgs eventArgs);

    /// <summary>
    /// 项目加载完成
    /// </summary>
    public delegate void ProjectLoadedHandler(ProjectLoadedEventArgs eventArgs);

    /// <summary>
    /// 加载项目文件时成功加载了DLL文件
    /// </summary>
    public delegate void LoadDllHandler(LoadDllEventArgs eventArgs);

    /// <summary>
    /// 移除了加载的dll
    /// </summary>
    /// <param name="eventArgs"></param>
    public delegate void RemoteDllHandler(RemoteDllEventArgs eventArgs);

    /// <summary>
    /// 运行环境节点连接发生了改变
    /// </summary>
    /// <param name="fromNodeGuid"></param>
    /// <param name="toNodeGuid"></param>
    /// <param name="connectionType"></param>
    public delegate void NodeConnectChangeHandler(NodeConnectChangeEventArgs eventArgs);

    /// <summary>
    /// 环境中加载了一个节点
    /// </summary>
    /// <param name="fromNodeGuid"></param>
    /// <param name="toNodeGuid"></param>
    /// <param name="connectionType"></param>
    public delegate void NodeCreateHandler(NodeCreateEventArgs eventArgs);

    /// <summary>
    /// 环境中流程起始节点发生了改变
    /// </summary>
    /// <param name="eventArgs"></param>
    public delegate void StartNodeChangeHandler(StartNodeChangeEventArgs eventArgs);

    /// <summary>
    /// 被监视的对象改变事件
    /// </summary>
    /// <param name="eventArgs"></param>
    public delegate void MonitorObjectChangeHandler(MonitorObjectEventArgs eventArgs);

    /// <summary>
    /// 节点中断状态改变事件（开启了中断/取消了中断）
    /// </summary>
    /// <param name="eventArgs"></param>
    public delegate void NodeInterruptStateChangeHandler(NodeInterruptStateChangeEventArgs eventArgs);

    /// <summary>
    /// 节点触发中断事件
    /// </summary>
    /// <param name="eventArgs"></param>
    public delegate void ExpInterruptTriggerHandler(InterruptTriggerEventArgs eventArgs);


    #endregion

    #region 环境事件签名

    /// <summary>
    /// 流程事件签名基类
    /// </summary>
    public class FlowEventArgs : EventArgs
    {
        /// <summary>
        /// 是否完成
        /// </summary>
        public bool IsSucceed { get; protected set; } = true;
        /// <summary>
        /// 错误提示
        /// </summary>
        public string ErrorTips { get; protected set; } = string.Empty;
    }

    //public class LoadNodeEventArgs : FlowEventArgs
    //{
    //    public LoadNodeEventArgs(NodeInfo NodeInfo, MethodDetails MethodDetailss)
    //    {
    //        this.NodeInfo = NodeInfo;
    //        this.MethodDetailss = MethodDetailss;
    //    }
    //    /// <summary>
    //    /// 项目文件节点信息参数
    //    /// </summary>
    //    public NodeInfo NodeInfo { get; protected set; }
    //    /// <summary>
    //    /// 已加载在环境中的方法描述
    //    /// </summary>
    //    public MethodDetails MethodDetailss { get; protected set; }
    //}

    public class ProjectLoadedEventArgs : FlowEventArgs
    {
        public ProjectLoadedEventArgs()
        {
        }
    }

    public class LoadDllEventArgs : FlowEventArgs
    {
        public LoadDllEventArgs(NodeLibrary nodeLibrary, List<MethodDetails> MethodDetailss)
        {
            this.NodeLibrary = nodeLibrary;
            this.MethodDetailss = MethodDetailss;
        }
        /// <summary>
        /// 已加载了的程序集
        /// </summary>
        public NodeLibrary NodeLibrary { get; protected set; }
        /// <summary>
        /// dll文件中有效的流程方法描述
        /// </summary>
        public List<MethodDetails> MethodDetailss { get; protected set; }
    }

    public class RemoteDllEventArgs : FlowEventArgs
    {
        public RemoteDllEventArgs()
        {
        }
    }


    public class NodeConnectChangeEventArgs : FlowEventArgs
    {
        /// <summary>
        /// 连接关系改变类型
        /// </summary>
        public enum ConnectChangeType
        {
            /// <summary>
            /// 创建
            /// </summary>
            Create,
            /// <summary>
            /// 移除
            /// </summary>
            Remote,
        }
        public NodeConnectChangeEventArgs(string fromNodeGuid, string toNodeGuid, ConnectionType connectionType, ConnectChangeType changeType)
        {
            this.FromNodeGuid = fromNodeGuid;
            this.ToNodeGuid = toNodeGuid;
            this.ConnectionType = connectionType;
            this.ChangeType = changeType;
        }
        /// <summary>
        /// 连接关系中始节点的Guid
        /// </summary>
        public string FromNodeGuid { get; protected set; }
        /// <summary>
        /// 连接关系中目标节点的Guid
        /// </summary>
        public string ToNodeGuid { get; protected set; }
        /// <summary>
        /// 连接类型
        /// </summary>
        public ConnectionType ConnectionType { get; protected set; }
        /// <summary>
        /// 表示此次需要在两个节点之间创建连接关系，或是移除连接关系
        /// </summary>
        public ConnectChangeType ChangeType { get; protected set; }
    }


    public class NodeCreateEventArgs : FlowEventArgs
    {
        public NodeCreateEventArgs(object nodeModel, Position position)
        {
            this.NodeModel = nodeModel;
            this.Position = position;
        }
        public NodeCreateEventArgs(object nodeModel, bool isAddInRegion, string regeionGuid)
        {
            this.NodeModel = nodeModel;
            this.RegeionGuid = regeionGuid;
            this.IsAddInRegion = isAddInRegion;
        }

        /// <summary>
        /// 节点Model对象，目前需要手动转换对应的类型
        /// </summary>
        public object NodeModel { get; private set; }
        public Position Position { get; private set; }
        public bool IsAddInRegion { get; private set; }
        public string RegeionGuid { get; private set; }
    }

    /// <summary>
    /// 环境中移除了一个节点
    /// </summary>
    /// <param name="eventArgs"></param>

    public delegate void NodeRemoteHandler(NodeRemoteEventArgs eventArgs);
    public class NodeRemoteEventArgs : FlowEventArgs
    {
        public NodeRemoteEventArgs(string nodeGuid)
        {
            this.NodeGuid = nodeGuid;
        }
        /// <summary>
        /// 被移除节点的Guid
        /// </summary>
        public string NodeGuid { get; private set; }
    }




    public class StartNodeChangeEventArgs : FlowEventArgs
    {
        public StartNodeChangeEventArgs(string oldNodeGuid, string newNodeGuid)
        {
            this.OldNodeGuid = oldNodeGuid;
            this.NewNodeGuid = newNodeGuid; ;
        }
        /// <summary>
        /// 原来的起始节点Guid
        /// </summary>
        public string OldNodeGuid { get; private set; }
        /// <summary>
        /// 新的起始节点Guid
        /// </summary>
        public string NewNodeGuid { get; private set; }
    }



    /// <summary>
    /// 监视的节点数据发生变化
    /// </summary>
    public class MonitorObjectEventArgs : FlowEventArgs
    {
        public enum ObjSourceType
        {
            NodeFlowData,
            IOCObj,
        }
        public MonitorObjectEventArgs(string nodeGuid, object monitorData, ObjSourceType objSourceType)
        {
            NodeGuid = nodeGuid;
            NewData = monitorData;
            ObjSource = objSourceType;
        }

        /// <summary>
        /// 中断的节点Guid
        /// </summary>
        public string NodeGuid { get; protected set; }
        public ObjSourceType ObjSource { get; protected set; }
        /// <summary>
        /// 新的数据
        /// </summary>
        public object NewData { get; protected set; }
    }

    /// <summary>
    /// 节点中断状态改变事件参数
    /// </summary>
    public class NodeInterruptStateChangeEventArgs : FlowEventArgs
    {
        public NodeInterruptStateChangeEventArgs(string nodeGuid, InterruptClass @class)
        {
            NodeGuid = nodeGuid;
            Class = @class;
        }

        /// <summary>
        /// 中断的节点Guid
        /// </summary>
        public string NodeGuid { get; protected set; }
        public InterruptClass Class { get; protected set; }
    }
    /// <summary>
    /// 节点触发了中断事件参数
    /// </summary>
    public class InterruptTriggerEventArgs : FlowEventArgs
    {
        public enum InterruptTriggerType
        {
            /// <summary>
            /// 主动监视中断
            /// </summary>
            Monitor,
            /// <summary>
            /// 表达式中断
            /// </summary>
            Exp,
            /// <summary>
            /// 对象监视中断
            /// </summary>
            Obj,
        }

        public InterruptTriggerEventArgs(string nodeGuid, string expression, InterruptTriggerType type)
        {
            this.NodeGuid = nodeGuid;
            this.Expression = expression;
            this.Type = type;
        }

        /// <summary>
        /// 中断的节点Guid
        /// </summary>
        public string NodeGuid { get; protected set; }
        public string Expression { get; protected set; }
        public InterruptTriggerType Type { get; protected set; }
    }
    #endregion


    /// <summary>
    /// IOC容器发生变化
    /// </summary>
    public delegate void IOCMembersChangedHandler(IOCMembersChangedEventArgs eventArgs);


    /// <summary>
    /// 流程事件签名基类
    /// </summary>
    public class IOCMembersChangedEventArgs : FlowEventArgs
    {
        public enum EventType
        {
            /// <summary>
            /// 登记了类型
            /// </summary>
            Registered,
            /// <summary>
            /// 构建了类型
            /// </summary>
            Completeuild,
        }
        public IOCMembersChangedEventArgs(string key, object instance)
        {
            this.Key = key;
            this.Instance = instance;
        }
        public string Key { get; private set; }
        public object Instance { get; private set; }
    }

    /// <summary>
    /// 节点需要定位
    /// </summary>
    /// <param name="eventArgs"></param>
    public delegate void NodeLocatedHandler(NodeLocatedEventArgs eventArgs);

    public class NodeLocatedEventArgs : FlowEventArgs
    {
        public NodeLocatedEventArgs(string nodeGuid)
        {
            NodeGuid = nodeGuid;
        }
        public string NodeGuid { get; private set; }
    }

    public interface IFlowEnvironment
    {
        #region 属性
        /// <summary>
        /// IOC容器
        /// </summary>
        ISereinIOC IOC { get; }

        /// <summary>
        /// 环境名称
        /// </summary>
        string EnvName { get; }
        /// <summary>
        /// 是否全局中断
        /// </summary>
        bool IsGlobalInterrupt { get; }

        

        #endregion

        #region 事件

        /// <summary>
        /// 加载Dll
        /// </summary>
        event LoadDllHandler OnDllLoad;

        /// <summary>
        /// 项目加载完成
        /// </summary>
        event ProjectLoadedHandler OnProjectLoaded;

        /// <summary>
        /// 节点连接属性改变事件
        /// </summary>
        event NodeConnectChangeHandler OnNodeConnectChange;

        /// <summary>
        /// 节点创建事件
        /// </summary>
        event NodeCreateHandler OnNodeCreate;

        /// <summary>
        /// 移除节点事件
        /// </summary>
        event NodeRemoteHandler OnNodeRemote;

        /// <summary>
        /// 起始节点变化事件
        /// </summary>
        event StartNodeChangeHandler OnStartNodeChange;

        /// <summary>
        /// 流程运行完成事件
        /// </summary>
        event FlowRunCompleteHandler OnFlowRunComplete;

        /// <summary>
        /// 被监视的对象改变事件
        /// </summary>
        event MonitorObjectChangeHandler OnMonitorObjectChange;

        /// <summary>
        /// 节点中断状态变化事件
        /// </summary>
        event NodeInterruptStateChangeHandler OnNodeInterruptStateChange;

        /// <summary>
        /// 触发中断
        /// </summary>
        event ExpInterruptTriggerHandler OnInterruptTrigger;

        /// <summary>
        /// IOC容器发生改变
        /// </summary>
        event IOCMembersChangedHandler OnIOCMembersChanged;


        /// <summary>
        /// 节点需要定位
        /// </summary>
        event NodeLocatedHandler OnNodeLocate;

        #endregion


        /// <summary>
        /// 获取方法描述
        /// </summary>
        /// <param name="name"></param>
        /// <param name="md"></param>
        /// <returns></returns>
        bool TryGetMethodDetails(string methodName, out MethodDetails md);


        bool TryGetDelegateDetails(string methodName, out DelegateDetails del);

        //bool TryGetNodeData(string methodName, out NodeData node);

        #region 环境基础接口

        /// <summary>
        /// 保存当前项目
        /// </summary>
        /// <returns></returns>
        SereinProjectData SaveProject();
        /// <summary>
        /// 加载项目文件
        /// </summary>
        /// <param name="projectFile"></param>
        /// <param name="filePath"></param>
        void LoadProject(SereinProjectData projectFile, string filePath);
        /// <summary>
        /// 从文件中加载Dll
        /// </summary>
        /// <param name="dllPath"></param>
        void LoadDll(string dllPath);
        /// <summary>
        /// 移除DLL
        /// </summary>
        /// <param name="dllPath"></param>
        bool RemoteDll(string assemblyFullName);

        /// <summary>
        /// 清理加载的DLL（待更改）
        /// </summary>
        void ClearAll();


        /// <summary>
        /// 开始运行
        /// </summary>
        Task StartAsync();
        /// <summary>
        /// 从选定的节点开始运行
        /// </summary>
        /// <param name="startNodeGuid"></param>
        /// <returns></returns>
        Task StartFlowInSelectNodeAsync(string startNodeGuid);

        /// <summary>
        /// 结束运行
        /// </summary>
        void Exit();

        /// <summary>
        /// 设置流程起点节点
        /// </summary>
        /// <param name="nodeGuid"></param>
        void SetStartNode(string nodeGuid);
        /// <summary>
        /// 在两个节点之间创建连接关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点Guid</param>
        /// <param name="toNodeGuid">目标节点Guid</param>
        /// <param name="connectionType">连接类型</param>
        void ConnectNode(string fromNodeGuid, string toNodeGuid, ConnectionType connectionType);
        /// <summary>
        /// 创建节点/区域/基础控件
        /// </summary>
        /// <param name="nodeBase">节点/区域/基础控件</param>
        /// <param name="methodDetails">节点绑定的方法说明（</param>
        void CreateNode(NodeControlType nodeBase, Position position, MethodDetails methodDetails = null);
        /// <summary>
        /// 移除两个节点之间的连接关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点</param>
        /// <param name="toNodeGuid">目标节点</param>
        /// <param name="connectionType">连接类型</param>
        void RemoteConnect(string fromNodeGuid, string toNodeGuid, ConnectionType connectionType);
        /// <summary>
        /// 移除节点/区域/基础控件
        /// </summary>
        /// <param name="nodeGuid">待移除的节点Guid</param>
        void RemoteNode(string nodeGuid);

        // 启动触发器
        void ActivateFlipflopNode(string nodeGuid);
        void TerminateFlipflopNode(string nodeGuid);


        /// <summary>
        /// 设置节点中断级别
        /// </summary>
        /// <param name="nodeGuid">被中断的节点Guid</param>
        /// <param name="interruptClass">新的中断级别</param>
        /// <returns></returns>
        bool SetNodeInterrupt(string nodeGuid, InterruptClass interruptClass);

        /// <summary>
        /// 添加作用于某个对象的中断表达式
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="expression"></param>
        /// <returns></returns>
        bool AddInterruptExpression(string key, string expression);
        
        /// <summary>
        /// 监视指定对象
        /// </summary>
        /// <param name="obj">需要监视的对象</param>
        /// <param name="isMonitor">是否启用监视</param>
        void SetMonitorObjState(string key,bool isMonitor);

        /// <summary>
        /// 检查一个对象是否处于监听状态，如果是，则传出与该对象相关的表达式（用于中断），如果不是，则返回false。
        /// </summary>
        /// <param name="obj">判断的对象</param>
        /// <param name="exps">表达式</param>
        /// <returns></returns>
        bool CheckObjMonitorState(string key, out List<string> exps);


        /// <summary>
        /// 全局中断
        /// </summary>
        /// <param name="signal"></param>
        /// <param name="interruptClass"></param>
        /// <returns></returns>
        Task<CancelType> GetOrCreateGlobalInterruptAsync();


        #endregion

        #region 启动器调用

        /// <summary>
        /// 流程启动器调用，监视数据更新通知
        /// </summary>
        /// <param name="nodeGuid">更新了数据的节点Guid</param>
        /// <param name="flowData">更新的数据</param>
        void MonitorObjectNotification(string nodeGuid, object monitorData, MonitorObjectEventArgs.ObjSourceType sourceType);

        /// <summary>
        /// 流程启动器调用，节点触发了中断
        /// </summary>
        /// <param name="nodeGuid">被中断的节点Guid</param>
        /// <param name="expression">被触发的表达式</param>
        /// <param name="type">中断类型。0主动监视，1表达式</param>
        void TriggerInterrupt(string nodeGuid, string expression, InterruptTriggerEventArgs.InterruptTriggerType type);


        #endregion

        #region UI视觉

        /// <summary>
        /// 节点定位
        /// </summary>
        /// <param name="nodeGuid"></param>
        void NodeLocated(string nodeGuid);

        #endregion
    }
}
