

using Serein.Library.FlowNode;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
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
    /// <param name="eventArgs"></param>
    public delegate void NodeConnectChangeHandler(NodeConnectChangeEventArgs eventArgs);

    /// <summary>
    /// 环境中加载了一个节点
    /// </summary>
    /// <param name="eventArgs"></param>
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

    /// <summary>
    /// IOC容器发生变化
    /// </summary>
    public delegate void IOCMembersChangedHandler(IOCMembersChangedEventArgs eventArgs);

    /// <summary>
    /// 节点需要定位
    /// </summary>
    /// <param name="eventArgs"></param>
    public delegate void NodeLocatedHandler(NodeLocatedEventArgs eventArgs);

    /// <summary>
    /// 节点移动了（远程插件）
    /// </summary>
    /// <param name="eventArgs"></param>
    public delegate void NodeMovedHandler(NodeMovedEventArgs eventArgs);

    /// <summary>
    /// 远程环境内容输出
    /// </summary>
    /// <param name="type">输出的日志类别</param>
    /// <param name="value">输出的文本信息</param>
    public delegate void EnvOutHandler(InfoType type, string value);


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
        public LoadDllEventArgs(NodeLibraryInfo nodeLibraryInfo, List<MethodDetailsInfo> MethodDetailss)
        {
            this.NodeLibraryInfo = nodeLibraryInfo;
            this.MethodDetailss = MethodDetailss;
        }
        /// <summary>
        /// 已加载了的程序集
        /// </summary>
        public NodeLibraryInfo NodeLibraryInfo { get; protected set; }
        /// <summary>
        /// dll文件中有效的流程方法描述
        /// </summary>
        public List<MethodDetailsInfo> MethodDetailss { get; protected set; }
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

        /// <summary>
        /// 更改方法调用关系
        /// </summary>
        /// <param name="fromNodeGuid"></param>
        /// <param name="toNodeGuid"></param>
        /// <param name="junctionOfConnectionType"></param>
        /// <param name="connectionInvokeType"></param>
        /// <param name="changeType"></param>
        public NodeConnectChangeEventArgs(string fromNodeGuid,
                                          string toNodeGuid,
                                          JunctionOfConnectionType junctionOfConnectionType, // 指示需要创建什么类型的连接线
                                          ConnectionInvokeType connectionInvokeType, // 节点调用的方法类型（true/false/error/cancel )
                                          ConnectChangeType changeType) // 需要创建连接线还是删除连接线
        {
            this.FromNodeGuid = fromNodeGuid;
            this.ToNodeGuid = toNodeGuid;
            this.ConnectionInvokeType = connectionInvokeType;
            this.ChangeType = changeType;
            this.JunctionOfConnectionType = junctionOfConnectionType;
        }

        /// <summary>
        /// 更改参数传递关系
        /// </summary>
        /// <param name="fromNodeGuid"></param>
        /// <param name="toNodeGuid"></param>
        /// <param name="junctionOfConnectionType"></param>
        /// <param name="argIndex"></param>
        /// <param name="connectionArgSourceType"></param>
        /// <param name="changeType"></param>
        public NodeConnectChangeEventArgs(string fromNodeGuid,
                                          string toNodeGuid,
                                          JunctionOfConnectionType junctionOfConnectionType, // 指示需要创建什么类型的连接线
                                          int argIndex,
                                          ConnectionArgSourceType connectionArgSourceType, // 节点对应的方法入参所需参数来源
                                          ConnectChangeType changeType) // 需要创建连接线还是删除连接线
        {
            this.FromNodeGuid = fromNodeGuid;
            this.ToNodeGuid = toNodeGuid;
            this.ChangeType = changeType;
            this.ArgIndex = argIndex;
            this.ConnectionArgSourceType = connectionArgSourceType;
            this.JunctionOfConnectionType = junctionOfConnectionType;

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
        public ConnectionInvokeType ConnectionInvokeType { get; protected set; }
        /// <summary>
        /// 表示此次需要在两个节点之间创建连接关系，或是移除连接关系
        /// </summary>
        public ConnectChangeType ChangeType { get; protected set; }
        /// <summary>
        /// 指示需要创建什么类型的连接线
        /// </summary>
        public JunctionOfConnectionType JunctionOfConnectionType { get; protected set; }
        /// <summary>
        /// 节点对应的方法入参所需参数来源
        /// </summary>
        public ConnectionArgSourceType ConnectionArgSourceType { get; protected set; }
        /// <summary>
        /// 第几个参数
        /// </summary>
        public int ArgIndex { get; protected set; }

        
    }


    public class NodeCreateEventArgs : FlowEventArgs
    {
        public NodeCreateEventArgs(object nodeModel, PositionOfUI position)
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
        public PositionOfUI Position { get; private set; }
        public bool IsAddInRegion { get; private set; }
        public string RegeionGuid { get; private set; }
    }

    /// <summary>
    /// 环境中移除了一个节点
    /// </summary>
    /// <param name="eventArgs"></param>

    public delegate void NodeRemoveHandler(NodeRemoveEventArgs eventArgs);
    public class NodeRemoveEventArgs : FlowEventArgs
    {
        public NodeRemoveEventArgs(string nodeGuid)
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
        /// <summary>
        /// 变化的数据类别
        /// </summary>
        public enum ObjSourceType
        {
            /// <summary>
            /// 流程节点的数据
            /// </summary>
            NodeFlowData,

            /// <summary>
            /// IOC容器对象
            /// </summary>
            IOCObj,
        }
        /// <summary>
        /// 在某个节点运行时，监听的数据发生了改变
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="monitorData"></param>
        /// <param name="objSourceType"></param>
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

        /// <summary>
        /// 监听对象类别
        /// </summary>
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
        public NodeInterruptStateChangeEventArgs(string nodeGuid,bool isInterrupt)
        {
            NodeGuid = nodeGuid;
            // Class = @class;
            IsInterrupt = isInterrupt;
        }

        /// <summary>
        /// 中断的节点Guid
        /// </summary>
        public string NodeGuid { get; protected set; }
        public bool IsInterrupt { get; protected set; }
        // public InterruptClass Class { get; protected set; }
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
    public class NodeLocatedEventArgs : FlowEventArgs
    {
        public NodeLocatedEventArgs(string nodeGuid)
        {
            NodeGuid = nodeGuid;
        }
        public string NodeGuid { get; private set; }
    }

    /// <summary>
    /// 节点移动了
    /// </summary>
     public class NodeMovedEventArgs : FlowEventArgs
    {
        public NodeMovedEventArgs(string nodeGuid, double x, double y)
        {
            this.NodeGuid = nodeGuid;
            this.X = x;
            this.Y = y;
        }
        /// <summary>
        /// 节点唯一标识
        /// </summary>
        public string NodeGuid { get; private set; }
        /// <summary>
        /// 画布上的x坐标
        /// </summary>
        public double X { get; private set; }
        /// <summary>
        /// 画布上的y坐标
        /// </summary>
        public double Y { get; private set; }
    }


    #endregion




    /// <summary>
    /// 运行环境
    /// </summary>
    public interface IFlowEnvironment
    {
        #region 属性


        /// <summary>
        /// <para>单例模式IOC容器，内部维护了一个实例字典，默认使用类型的FullName作为Key，如果以“接口-实现类”的方式注册，那么将使用接口类型的FullName作为Key。</para>
        /// <para>当某个类型注册绑定成功后，将不会因为其它地方尝试注册相同类型的行为导致类型被重新创建。</para>
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

        /// <summary>
        /// <para>表示是否正在控制远程</para>
        /// <para>Local control remote env</para>
        /// </summary>
        bool IsControlRemoteEnv { get; }

        /// <summary>
        /// 信息输出等级
        /// </summary>
        InfoClass InfoClass { get; set; }

        /// <summary>
        /// 流程运行状态
        /// </summary>
        RunState FlowState { get;  set; }

        /// <summary>
        /// 全局触发器运行状态
        /// </summary>
        RunState FlipFlopState { get;  set; }

        /// <summary>
        /// 表示当前环境
        /// </summary>
        IFlowEnvironment CurrentEnv { get; }

        /// <summary>
        /// 由运行环境提供的UI线程上下文操作，用于类库中需要在UI线程中操作视觉元素的场景
        /// </summary>
        UIContextOperation UIContextOperation { get;  }

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
        event NodeRemoveHandler OnNodeRemove;

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
        event NodeLocatedHandler OnNodeLocated;

        /// <summary>
        /// 节点移动了（远程插件）
        /// </summary>
        event NodeMovedHandler OnNodeMoved;

        /// <summary>
        /// 运行环境输出
        /// </summary>
        event EnvOutHandler OnEnvOut;
        #endregion

        #region 流程接口

        /// <summary>
        /// 设置输出
        /// </summary>
        // <param name="output"></param>
        // <param name="clearMsg"></param>
        ///void SetConsoleOut(); // Action<string> output, Action clearMsg

        /// <summary>
        /// 输出信息
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        void WriteLine(InfoType type, string message, InfoClass @class = InfoClass.Trivial);

        ///// <summary>
        ///// 使用JSON处理库输出对象信息
        ///// </summary>
        ///// <param name="obj"></param>
        //void WriteLineObjToJson(object obj);

        /// <summary>
        /// 启动远程服务
        /// </summary>
        Task StartRemoteServerAsync(int port = 7525);
        /// <summary>
        /// 停止远程服务
        /// </summary>
        void StopRemoteServer();


        /// <summary>
        /// 保存当前项目
        /// </summary>
        /// <returns></returns>
        Task<SereinProjectData> GetProjectInfoAsync();
        /// <summary>
        /// 加载项目文件
        /// </summary>
        /// <param name="flowEnvInfo">包含项目信息的远程环境</param>
        /// <param name="filePath"></param>
        void LoadProject(FlowEnvInfo flowEnvInfo, string filePath);
        /// <summary>
        /// 加载远程环境
        /// </summary>
        /// <param name="addres">远程环境地址</param>
        /// <param name="port">远程环境端口</param>
        /// <param name="token">密码</param>
        Task<(bool, RemoteMsgUtil)> ConnectRemoteEnv(string addres,int port, string token);

        /// <summary>
        /// 退出远程环境
        /// </summary>
        void ExitRemoteEnv();

        /// <summary>
        /// 从文件中加载Dll
        /// </summary>
        /// <param name="dllPath"></param>
        void LoadLibrary(string dllPath);
        /// <summary>
        /// 移除DLL
        /// </summary>
        /// <param name="assemblyFullName">程序集的名称</param>
        bool UnloadLibrary(string assemblyFullName);

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
        Task StartAsyncInSelectNode(string startNodeGuid);

        /// <summary>
        /// 立刻调用某个节点，并获取其返回值
        /// </summary>
        /// <param name="context">调用时的上下文</param>
        /// <param name="nodeGuid">节点Guid</param>
        /// <returns></returns>
        Task<object> InvokeNodeAsync(IDynamicContext context, string nodeGuid);

        /// <summary>
        /// 结束运行
        /// </summary>
        void ExitFlow();

        /// <summary>
        /// 移动了某个节点(远程插件使用）
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        void MoveNode(string nodeGuid,double x, double y);

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
        /// <param name="fromNodeJunctionType">起始节点控制点</param>
        /// <param name="toNodeJunctionType">目标节点控制点</param>
        /// <param name="invokeType">决定了方法执行后的后继行为</param>
        Task<bool> ConnectInvokeNodeAsync(string fromNodeGuid,
                                    string toNodeGuid,
                                    JunctionType fromNodeJunctionType,
                                    JunctionType toNodeJunctionType,
                                    ConnectionInvokeType invokeType);

        /// <summary>
        /// 在两个节点之间创建连接关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点Guid</param>
        /// <param name="toNodeGuid">目标节点Guid</param>
        /// <param name="fromNodeJunctionType">起始节点控制点</param>
        /// <param name="toNodeJunctionType">目标节点控制点</param>
        /// <param name="argSourceType">决定了方法参数来源</param>
        /// <param name="argIndex">设置第几个参数</param>
        Task<bool> ConnectArgSourceNodeAsync(string fromNodeGuid,
                                                 string toNodeGuid,
                                                 JunctionType fromNodeJunctionType,
                                                 JunctionType toNodeJunctionType,
                                                 ConnectionArgSourceType argSourceType,
                                                 int argIndex);
        /// <summary>
        /// 创建节点/区域/基础控件
        /// </summary>
        /// <param name="nodeType">节点/区域/基础控件类型</param>
        /// <param name="position">节点在画布上的位置（</param>
        /// <param name="methodDetailsInfo">节点绑定的方法说明</param>
        Task<NodeInfo> CreateNodeAsync(NodeControlType nodeType, PositionOfUI position, MethodDetailsInfo methodDetailsInfo = null);

        /// <summary>
        /// 移除两个节点之间的方法调用关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点</param>
        /// <param name="toNodeGuid">目标节点</param>
        /// <param name="connectionType">连接类型</param>
        Task<bool> RemoveConnectInvokeAsync(string fromNodeGuid, string toNodeGuid, ConnectionInvokeType connectionType);

        /// <summary>
        /// 移除连接节点之间参数传递的关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点Guid</param>
        /// <param name="toNodeGuid">目标节点Guid</param>
        /// <param name="argIndex">连接到第几个参数</param>
        /// <param name="connectionArgSourceType">参数来源类型</param>
        Task<bool> RemoveConnectArgSourceAsync(string fromNodeGuid, string toNodeGuid, int argIndex);


        /// <summary>
        /// 移除节点/区域/基础控件
        /// </summary>
        /// <param name="nodeGuid">待移除的节点Guid</param>
        Task<bool> RemoveNodeAsync(string nodeGuid);

        /// <summary>
        /// 激活未启动的全局触发器
        /// </summary>
        /// <param name="nodeGuid"></param>
        void ActivateFlipflopNode(string nodeGuid);

        /// <summary>
        /// 终结一个全局触发器，在它触发后将不会再次监听消息（表现为已经启动的触发器至少会再次处理一次消息，后面版本再修正这个非预期行为）
        /// </summary>
        /// <param name="nodeGuid"></param>
        void TerminateFlipflopNode(string nodeGuid);


        /// <summary>
        /// 设置节点中断
        /// </summary>
        /// <param name="nodeGuid">更改中断状态的节点Guid</param>
        /// <param name="isInterrup">是否中断</param>
        /// <returns></returns>
        Task<bool> SetNodeInterruptAsync(string nodeGuid,bool isInterrup);

        /// <summary>
        /// 添加作用于某个对象的中断表达式
        /// </summary>
        /// <param name="key"></param>
        /// <param name="expression"></param>
        /// <returns></returns>
        Task<bool> AddInterruptExpressionAsync(string key, string expression);

        /// <summary>
        /// 监视指定对象
        /// </summary>
        /// <param name="key">需要监视的对象</param>
        /// <param name="isMonitor">是否启用监视</param>
        void SetMonitorObjState(string key,bool isMonitor);

        /// <summary>
        /// 检查一个对象是否处于监听状态，如果是，则传出与该对象相关的表达式（用于中断），如果不是，则返回false。
        /// </summary>
        /// <param name="key">判断的对象</param>
        /// <returns></returns>
        Task<(bool, string[])> CheckObjMonitorStateAsync(string key);


        /// <summary>
        /// 全局中断
        /// </summary>
        /// <param name="signal"></param>
        /// <param name="interruptClass"></param>
        /// <returns></returns>
        Task<CancelType> GetOrCreateGlobalInterruptAsync();

        /// <summary>
        /// （用于远程）通知节点属性变更
        /// </summary>
        /// <param name="nodeGuid">节点Guid</param>
        /// <param name="path">属性路径</param>
        /// <param name="value">属性值</param>
        /// <returns></returns>
        Task NotificationNodeValueChangeAsync(string nodeGuid, string path, object value);

        /// <summary>
        /// 改变可选参数的数目
        /// </summary>
        /// <param name="nodeGuid">对应的节点Guid</param>
        /// <param name="isAdd">true，增加参数；false，减少参数</param>
        /// <param name="paramIndex">以哪个参数为模板进行拷贝，或删去某个参数（该参数必须为可选参数）</param>
        /// <returns></returns>
        Task<bool> ChangeParameter(string nodeGuid, bool isAdd, int paramIndex);


        /// <summary>
        /// 获取方法描述信息
        /// </summary>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="methodName">方法描述</param>
        /// <param name="mdInfo">方法信息</param>
        /// <returns></returns>
        bool TryGetMethodDetailsInfo(string assemblyName, string methodName, out MethodDetailsInfo mdInfo);

        /// <summary>
        /// 获取指定方法的Emit委托
        /// </summary>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="methodName"></param>
        /// <param name="del"></param>
        /// <returns></returns>
        bool TryGetDelegateDetails(string assemblyName, string methodName, out DelegateDetails del);


        #region 远程相关
        /// <summary>
        /// (适用于远程连接后获取环境的运行状态)获取当前环境的信息
        /// </summary>
        /// <returns></returns>
        Task<FlowEnvInfo> GetEnvInfoAsync();
        #endregion

        #endregion

        #region 启动器调用

        /// <summary>
        /// 流程启动器调用，监视数据更新通知
        /// </summary>
        /// <param name="nodeGuid">更新了数据的节点Guid</param>
        /// <param name="monitorData">更新的数据</param>
        /// <param name="sourceType">更新的数据</param>
        void MonitorObjectNotification(string nodeGuid, object monitorData, MonitorObjectEventArgs.ObjSourceType sourceType);

        /// <summary>
        /// 流程启动器调用，节点触发了中断
        /// </summary>
        /// <param name="nodeGuid">被中断的节点Guid</param>
        /// <param name="expression">被触发的表达式</param>
        /// <param name="type">中断类型。0主动监视，1表达式</param>
        void TriggerInterrupt(string nodeGuid, string expression, InterruptTriggerEventArgs.InterruptTriggerType type);


        #endregion

        #region 流程依赖类库的接口

        /// <summary>
        /// 运行时加载
        /// </summary>
        /// <param name="file">文件名</param>
        /// <returns></returns>
        bool LoadNativeLibraryOfRuning(string file);

        /// <summary>
        /// 运行时加载指定目录下的类库
        /// </summary>
        /// <param name="path">目录</param>
        /// <param name="isRecurrence">是否递归加载</param>
        void LoadAllNativeLibraryOfRuning(string path, bool isRecurrence = true);

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
