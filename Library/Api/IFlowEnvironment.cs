using Serein.Library.Utils;
using System;
using System.Threading.Tasks;

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
    /// 项目准备保存
    /// </summary>
    /// <param name="eventArgs"></param>
    public delegate void ProjectSavingHandler(ProjectSavingEventArgs eventArgs);


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
    /// 环境中新增了一个画布
    /// </summary>
    /// <param name="eventArgs"></param>
    public delegate void CanvasCreateHandler(CanvasCreateEventArgs eventArgs);

    /// <summary>
    /// 环境中移除了一个画布
    /// </summary>
    /// <param name="eventArgs"></param>
    public delegate void CanvasRemoveHandler(CanvasRemoveEventArgs eventArgs);

    /// <summary>
    /// 环境中加载了一个节点
    /// </summary>
    /// <param name="eventArgs"></param>
    public delegate void NodeCreateHandler(NodeCreateEventArgs eventArgs);

    /// <summary>
    /// 环境中移除了一个节点
    /// </summary>
    /// <param name="eventArgs"></param>

    public delegate void NodeRemoveHandler(NodeRemoveEventArgs eventArgs);

    /// <summary>
    /// 节点放置事件
    /// </summary>
    /// <param name="eventArgs"></param>
    public delegate void NodePlaceHandler(NodePlaceEventArgs eventArgs);

    /// <summary>
    /// 节点取出事件
    /// </summary>
    /// <param name="eventArgs"></param>
    public delegate void NodeTakeOutHandler(NodeTakeOutEventArgs eventArgs);

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
        public bool IsSucceed { get;} = true;
        /// <summary>
        /// 错误提示
        /// </summary>
        public string ErrorTips { get;} = string.Empty;
    }

    /// <summary>
    /// 项目加载完成
    /// </summary>
    public class ProjectLoadedEventArgs : FlowEventArgs
    {
        /// <summary>
        /// 项目加载完成事件参数
        /// </summary>
        public ProjectLoadedEventArgs()
        {
        }
    }
    
    /// <summary>
    /// 项目保存
    /// </summary>
    public class ProjectSavingEventArgs : FlowEventArgs
    {
        /// <summary>
        /// 项目保存事件参数
        /// </summary>
        /// <param name="projectData"></param>
        public ProjectSavingEventArgs(SereinProjectData projectData)
        {
            ProjectData = projectData;
        }

        /// <summary>
        /// 项目数据
        /// </summary>
        public SereinProjectData ProjectData { get; }
    }

    /// <summary>
    /// 加载了DLL外部依赖
    /// </summary>
    public class LoadDllEventArgs : FlowEventArgs
    {
        /// <summary>
        /// 加载了DLL外部依赖事件参数
        /// </summary>
        /// <param name="nodeLibraryInfo"></param>
        public LoadDllEventArgs(FlowLibraryInfo nodeLibraryInfo)
        {
            this.NodeLibraryInfo = nodeLibraryInfo;
        }
        /// <summary>
        /// 已加载了的程序集
        /// </summary>
        public FlowLibraryInfo NodeLibraryInfo { get;}
    }

    /// <summary>
    /// 移除了DLL外部依赖
    /// </summary>
    public class RemoteDllEventArgs : FlowEventArgs
    {
        /// <summary>
        /// 移除了DLL外部依赖事件参数
        /// </summary>
        public RemoteDllEventArgs()
        {
        }
    }

    /// <summary>
    /// 改变节点连接关系
    /// </summary>

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
            Remove,
        }

        /// <summary>
        /// 更改方法调用关系
        /// </summary>
        /// <param name="fromNodeGuid"></param>
        /// <param name="toNodeGuid"></param>
        /// <param name="junctionOfConnectionType"></param>
        /// <param name="connectionInvokeType"></param>
        /// <param name="changeType"></param>
        public NodeConnectChangeEventArgs(string canvasGuid,
                                          string fromNodeGuid,
                                          string toNodeGuid,
                                          JunctionOfConnectionType junctionOfConnectionType, // 指示需要创建什么类型的连接线
                                          ConnectionInvokeType connectionInvokeType, // 节点调用的方法类型（true/false/error/cancel )
                                          ConnectChangeType changeType) // 需要创建连接线还是删除连接线
        {
            this.CanvasGuid = canvasGuid;
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
        public NodeConnectChangeEventArgs(string canvasGuid,
                                          string fromNodeGuid,
                                          string toNodeGuid,
                                          int argIndex,
                                          JunctionOfConnectionType junctionOfConnectionType, // 指示需要创建什么类型的连接线
                                          ConnectionArgSourceType connectionArgSourceType, // 节点对应的方法入参所需参数来源
                                          ConnectChangeType changeType) // 需要创建连接线还是删除连接线
        {
            CanvasGuid = canvasGuid;
            this.FromNodeGuid = fromNodeGuid;
            this.ToNodeGuid = toNodeGuid;
            this.ChangeType = changeType;
            this.ArgIndex = argIndex;
            this.ConnectionArgSourceType = connectionArgSourceType;
            this.JunctionOfConnectionType = junctionOfConnectionType;

        }

        /// <summary>
        /// 连接关系所在的画布Guid
        /// </summary>
        public string CanvasGuid { get; }

        /// <summary>
        /// 连接关系中始节点的Guid
        /// </summary>
        public string FromNodeGuid { get;}
        /// <summary>
        /// 连接关系中目标节点的Guid
        /// </summary>
        public string ToNodeGuid { get;}
        /// <summary>
        /// 连接类型
        /// </summary>
        public ConnectionInvokeType ConnectionInvokeType { get; } = ConnectionInvokeType.None;
        /// <summary>
        /// 表示此次需要在两个节点之间创建连接关系，或是移除连接关系
        /// </summary>
        public ConnectChangeType ChangeType { get;} 
        /// <summary>
        /// 指示需要创建什么类型的连接线
        /// </summary>
        public JunctionOfConnectionType JunctionOfConnectionType { get; } = JunctionOfConnectionType.None;
        /// <summary>
        /// 节点对应的方法入参所需参数来源
        /// </summary>
        public ConnectionArgSourceType ConnectionArgSourceType { get;}  
        /// <summary>
        /// 第几个参数
        /// </summary>
        public int ArgIndex { get; } = -1; 

        
    }

    /// <summary>
    /// 添加了一个画布
    /// </summary>
    public class CanvasCreateEventArgs : FlowEventArgs
    {
        /// <summary>
        /// 画布添加事件参数
        /// </summary>
        /// <param name="model"></param>
        public CanvasCreateEventArgs(FlowCanvasDetails model)
        {
            Model = model;
        }

        /// <summary>
        /// 画布
        /// </summary>
        public FlowCanvasDetails Model { get; }
    }

    /// <summary>
    /// 移除了一个画布
    /// </summary>
    public class CanvasRemoveEventArgs : FlowEventArgs
    {
        /// <summary>
        /// 画布移除事件参数
        /// </summary>
        /// <param name="canvasGuid"></param>
        public CanvasRemoveEventArgs(string canvasGuid)
        {
            CanvasGuid = canvasGuid;
        }

        /// <summary>
        /// 所处画布Guid
        /// </summary>
        public string CanvasGuid { get; }
    }

    /// <summary>
    /// 添加了节点
    /// </summary>
    public class NodeCreateEventArgs : FlowEventArgs
    {
        /// <summary>
        /// 节点添加事件参数
        /// </summary>
        /// <param name="canvasGuid">画布</param>
        /// <param name="nodeModel">节点对象</param>
        /// <param name="position">位置</param>
        public NodeCreateEventArgs(string canvasGuid, IFlowNode nodeModel, PositionOfUI position)
        {
            CanvasGuid = canvasGuid;
            this.NodeModel = nodeModel;
            this.Position = position;
        }
        /// <summary>
        /// 所处画布Guid
        /// </summary>
        public string CanvasGuid { get; }

        /// <summary>
        /// 节点Model对象
        /// </summary>
        public IFlowNode NodeModel { get; private set; }
        /// <summary>
        /// 在UI上的位置
        /// </summary>
        public PositionOfUI Position { get; private set; }
    }

    /// <summary>
    /// 移除了节点的事件
    /// </summary>
    public class NodeRemoveEventArgs : FlowEventArgs
    {
        /// <summary>
        /// 被移除节点事件参数
        /// </summary>
        /// <param name="canvasGuid"></param>
        /// <param name="nodeGuid"></param>
        public NodeRemoveEventArgs(string canvasGuid, string nodeGuid)
        {
            CanvasGuid = canvasGuid;
            this.NodeGuid = nodeGuid;
        }

        /// <summary>
        /// 被移除节点所在的画布Guid
        /// </summary>
        public string CanvasGuid { get; }

        /// <summary>
        /// 被移除节点的Guid
        /// </summary>
        public string NodeGuid { get; private set; }
    }

    /// <summary>
    /// 节点放置事件参数
    /// </summary>
    public class NodePlaceEventArgs : FlowEventArgs 
    {
        /// <summary>
        /// 节点放置事件参数
        /// </summary>
        /// <param name="canvasGuid"></param>
        /// <param name="nodeGuid"></param>
        /// <param name="containerNodeGuid"></param>
        public NodePlaceEventArgs(string canvasGuid, string nodeGuid, string containerNodeGuid)
        {
            CanvasGuid = canvasGuid;
            NodeGuid = nodeGuid;
            ContainerNodeGuid = containerNodeGuid;
        }

        /// <summary>
        /// 画布Guid
        /// </summary>
        public string CanvasGuid { get; }

        /// <summary>
        /// 子节点，该数据为此次时间的主节点
        /// </summary>
        public string NodeGuid { get; private set; }
        /// <summary>
        /// 父节点
        /// </summary>
        public string ContainerNodeGuid { get; private set; }
    }

    /// <summary>
    /// 节点取出事件参数
    /// </summary>
    public class NodeTakeOutEventArgs : FlowEventArgs
    {
        /// <summary>
        /// 节点取出事件参数
        /// </summary>
        /// <param name="canvasGuid"></param>
        /// <param name="containerNodeGuid"></param>
        /// <param name="nodeGuid"></param>
        public NodeTakeOutEventArgs(string canvasGuid, string containerNodeGuid, string nodeGuid)
        {
            CanvasGuid = canvasGuid;
            NodeGuid = nodeGuid;
            ContainerNodeGuid = containerNodeGuid;
        }

        /// <summary>
        /// 所在画布Guid
        /// </summary>
        public string CanvasGuid { get; }

        /// <summary>
        /// 需要取出的节点Guid
        /// </summary>
        public string NodeGuid { get; private set; }

        /// <summary>
        /// 容器节点Guid
        /// </summary>
        public string ContainerNodeGuid { get; private set; }
    }





    /// <summary>
    /// 起始节点发生了变化
    /// </summary>
    public class StartNodeChangeEventArgs : FlowEventArgs
    {
        /// <summary>
        /// 起始节点发生了变化事件参数
        /// </summary>
        /// <param name="canvasGuid"></param>
        /// <param name="oldNodeGuid"></param>
        /// <param name="newNodeGuid"></param>
        public StartNodeChangeEventArgs(string canvasGuid, string oldNodeGuid, string newNodeGuid)
        {
            CanvasGuid = canvasGuid;
            this.OldNodeGuid = oldNodeGuid;
            this.NewNodeGuid = newNodeGuid; ;
        }

        /// <summary>
        /// 所在画布Guid
        /// </summary>
        public string CanvasGuid { get; }

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
        public string NodeGuid { get;}

        /// <summary>
        /// 监听对象类别
        /// </summary>
        public ObjSourceType ObjSource { get;}
        /// <summary>
        /// 新的数据
        /// </summary>
        public object NewData { get;}
    }

    /// <summary>
    /// 节点中断状态改变事件参数
    /// </summary>
    public class NodeInterruptStateChangeEventArgs : FlowEventArgs
    {
        /// <summary>
        /// 节点中断状态改变事件参数
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="isInterrupt"></param>
        public NodeInterruptStateChangeEventArgs(string nodeGuid,bool isInterrupt)
        {
            NodeGuid = nodeGuid;
            // Class = @class;
            IsInterrupt = isInterrupt;
        }

        /// <summary>
        /// 中断的节点Guid
        /// </summary>
        public string NodeGuid { get;}
        /// <summary>
        /// 是否中断
        /// </summary>
        public bool IsInterrupt { get;}
    }
    /// <summary>
    /// 节点触发了中断事件参数
    /// </summary>
    public class InterruptTriggerEventArgs : FlowEventArgs
    {
        /// <summary>
        /// 中断触发类型
        /// </summary>
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

        /// <summary>
        /// 中断触发事件参数
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="expression"></param>
        /// <param name="type"></param>
        public InterruptTriggerEventArgs(string nodeGuid, string expression, InterruptTriggerType type)
        {
            this.NodeGuid = nodeGuid;
            this.Expression = expression;
            this.Type = type;
        }

        /// <summary>
        /// 中断的节点Guid
        /// </summary>
        public string NodeGuid { get;}
        /// <summary>
        /// 被触发的表达式
        /// </summary>
        public string Expression { get;}
        /// <summary>
        /// 中断触发类型
        /// </summary>
        public InterruptTriggerType Type { get;}
    }



    /// <summary>
    /// 流程事件签名基类
    /// </summary>
    public class IOCMembersChangedEventArgs : FlowEventArgs
    {
        /// <summary>
        /// IOC成员发生改变的事件类型
        /// </summary>
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
        /// <summary>
        /// IOC成员发生改变事件参数
        /// </summary>
        /// <param name="key"></param>
        /// <param name="instance"></param>
        public IOCMembersChangedEventArgs(string key, object instance)
        {
            this.Key = key;
            this.Instance = instance;
        }
        /// <summary>
        /// IOC成员发生改变事件参数
        /// </summary>
        public string Key { get; private set; }
        /// <summary>
        /// IOC成员发生改变事件参数
        /// </summary>
        public object Instance { get; private set; }
    }

    /// <summary>
    /// 节点需要定位
    /// </summary>
    public class NodeLocatedEventArgs : FlowEventArgs
    {
        /// <summary>
        /// 节点需要定位事件参数
        /// </summary>
        /// <param name="nodeGuid"></param>
        public NodeLocatedEventArgs(string nodeGuid)
        {
            NodeGuid = nodeGuid;
        }
        /// <summary>
        /// 节点需要定位事件参数
        /// </summary>
        public string NodeGuid { get; private set; }
    }


    #endregion


    /// <summary>
    /// 流程环境事件接口
    /// </summary>
    public interface IFlowEnvironmentEvent
    {
        /// <summary>
        /// 加载Dll
        /// </summary>
        event LoadDllHandler DllLoad;

        /// <summary>
        /// 项目加载完成
        /// </summary>
        event ProjectLoadedHandler ProjectLoaded;

        /// <summary>
        /// 项目准备保存
        /// </summary>
        event ProjectSavingHandler ProjectSaving;

        /// <summary>
        /// 节点连接属性改变事件
        /// </summary>
        event NodeConnectChangeHandler NodeConnectChanged;

        /// <summary>
        /// 增加画布事件
        /// </summary>
        event CanvasCreateHandler CanvasCreated;

        /// <summary>
        /// 删除画布事件
        /// </summary>
        event CanvasRemoveHandler CanvasRemoved;

        /// <summary>
        /// 节点创建事件
        /// </summary>
        event NodeCreateHandler NodeCreated;

        /// <summary>
        /// 移除节点事件
        /// </summary>
        event NodeRemoveHandler NodeRemoved;

        /// <summary>
        /// 节点放置事件
        /// </summary>
        event NodePlaceHandler NodePlace;

        /// <summary>
        /// 节点取出事件
        /// </summary>
        event NodeTakeOutHandler NodeTakeOut;

        /// <summary>
        /// 起始节点变化事件
        /// </summary>
        event StartNodeChangeHandler StartNodeChanged;

        /// <summary>
        /// 流程运行完成事件
        /// </summary>
        event FlowRunCompleteHandler FlowRunComplete;

        /// <summary>
        /// 被监视的对象改变事件
        /// </summary>
        event MonitorObjectChangeHandler MonitorObjectChanged;

        /// <summary>
        /// 节点中断状态变化事件
        /// </summary>
        event NodeInterruptStateChangeHandler NodeInterruptStateChanged;

        /// <summary>
        /// 触发中断
        /// </summary>
        event ExpInterruptTriggerHandler InterruptTriggered;

        /// <summary>
        /// IOC容器发生改变
        /// </summary>
        event IOCMembersChangedHandler IOCMembersChanged;

        /// <summary>
        /// 节点需要定位
        /// </summary>
        event NodeLocatedHandler NodeLocated;


        /// <summary>
        /// 运行环境输出
        /// </summary>
        event EnvOutHandler EnvOutput;

        /// <summary>
        /// 加载了DLL外部依赖事件
        /// </summary>
        /// <param name="eventArgs"></param>
        public void OnDllLoad(LoadDllEventArgs eventArgs);

        /// <summary>
        /// 项目加载完成事件
        /// </summary>
        /// <param name="eventArgs"></param>
        public void OnProjectLoaded(ProjectLoadedEventArgs eventArgs);

        /// <summary>
        /// 项目准备保存事件
        /// </summary>
        /// <param name="eventArgs"></param>
        public void OnProjectSaving(ProjectSavingEventArgs eventArgs);

        /// <summary>
        /// 节点连接关系发生改变事件
        /// </summary>
        /// <param name="eventArgs"></param>
        public void OnNodeConnectChanged(NodeConnectChangeEventArgs eventArgs);

        /// <summary>
        /// 画布创建事件
        /// </summary>
        /// <param name="eventArgs"></param>
        public void OnCanvasCreated(CanvasCreateEventArgs eventArgs);

        /// <summary>
        /// 画布移除事件
        /// </summary>
        /// <param name="eventArgs"></param>
        public void OnCanvasRemoved(CanvasRemoveEventArgs eventArgs);

        /// <summary>
        /// 节点创建事件
        /// </summary>
        /// <param name="eventArgs"></param>
        public void OnNodeCreated(NodeCreateEventArgs eventArgs);

        /// <summary>
        /// 节点移除事件
        /// </summary>
        /// <param name="eventArgs"></param>
        public void OnNodeRemoved(NodeRemoveEventArgs eventArgs);

        /// <summary>
        /// 节点放置事件
        /// </summary>
        /// <param name="eventArgs"></param>
        public void OnNodePlace(NodePlaceEventArgs eventArgs);

        /// <summary>
        /// 节点取出事件
        /// </summary>
        /// <param name="eventArgs"></param>
        public void OnNodeTakeOut(NodeTakeOutEventArgs eventArgs);

        /// <summary>
        /// 起始节点发生了变化事件
        /// </summary>
        /// <param name="eventArgs"></param>
        public void OnStartNodeChanged(StartNodeChangeEventArgs eventArgs);

        /// <summary>
        /// 流程运行完成事件
        /// </summary>
        /// <param name="eventArgs"></param>
        public void OnFlowRunComplete(FlowEventArgs eventArgs);

        /// <summary>
        /// 被监视的对象发生了改变事件
        /// </summary>
        /// <param name="eventArgs"></param>
        public void OnMonitorObjectChanged(MonitorObjectEventArgs eventArgs);

        /// <summary>
        /// 节点中断状态发生了改变事件（开启了中断/取消了中断）
        /// </summary>
        /// <param name="eventArgs"></param>
        public void OnNodeInterruptStateChanged(NodeInterruptStateChangeEventArgs eventArgs);

        /// <summary>
        /// 触发了中断事件
        /// </summary>
        /// <param name="eventArgs"></param>
        public void OnInterruptTriggered(InterruptTriggerEventArgs eventArgs);

        /// <summary>
        /// IOC容器成员发生了改变事件
        /// </summary>
        /// <param name="eventArgs"></param>
        public void OnIOCMembersChanged(IOCMembersChangedEventArgs eventArgs);

        /// <summary>
        /// 节点需要定位事件
        /// </summary>
        /// <param name="eventArgs"></param>
        public void OnNodeLocated(NodeLocatedEventArgs eventArgs);

        /// <summary>
        /// 环境输出信息事件
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public void OnEnvOutput(InfoType type, string value);
    }

    

    /// <summary>
    /// 运行环境
    /// </summary>
    public interface IFlowEnvironment 
    {
        #region 属性


        /// <summary>
        /// <para>运行环境使用的IOC，默认情况下无需对其进行调用</para>
        /// </summary>
        ISereinIOC IOC { get; }

        /// <summary>
        /// 流程编辑接口
        /// </summary>
        IFlowEdit FlowEdit { get; }

        /// <summary>
        /// 流程控制接口
        /// </summary>
        IFlowControl FlowControl { get; }

        /// <summary>
        /// 流程依赖类库接口
        /// </summary>
        IFlowLibraryService FlowLibraryService { get; }

        /// <summary>
        /// 流程事件接口
        /// </summary>
        IFlowEnvironmentEvent Event { get; }

        /// <summary>
        /// 环境名称
        /// </summary>
        string EnvName { get; }

        /// <summary>
        /// 项目文件位置
        /// </summary>
        string ProjectFileLocation { get; }

        /// <summary>
        /// 是否全局中断
        /// </summary>
        bool _IsGlobalInterrupt { get; }

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
        /// 表示当前环境
        /// </summary>
        IFlowEnvironment CurrentEnv { get; }

        /// <summary>
        /// 由运行环境提供的UI线程上下文操作，用于类库中需要在UI线程中操作视觉元素的场景
        /// </summary>
        UIContextOperation UIContextOperation { get;  }

        #endregion

        #region 基本接口

        /// <summary>
        /// 输出信息
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="type">输出类型</param>
        /// <param name="class">输出级别</param>
        void WriteLine(InfoType type, string message, InfoClass @class = InfoClass.Debug);
        /// <summary>
        /// <para>提供设置UI上下文的能力</para>
        /// <para>提供设置UI上下文的能力，在WinForm/WPF项目中，在UI线程外对UI元素的修改将会导致异常</para>
        /// <para>需要你提供</para>
        /// </summary>
        /// <param name="uiContextOperation"></param>
        void SetUIContextOperation(UIContextOperation uiContextOperation);
        #endregion

        #region 项目相关操作

        /// <summary>
        /// 加载项目文件
        /// </summary>
        /// <param name="filePath"></param>
        void LoadProject(string filePath);

        /// <summary>
        /// 加载项目文件
        /// </summary>
        /// <param name="filePath"></param>
        Task LoadProjetAsync(string filePath);

        /// <summary>
        /// 保存项目
        /// </summary>
        void SaveProject();

        /// <summary>
        /// 获取当前项目信息
        /// </summary>
        /// <returns></returns>
        SereinProjectData GetProjectInfoAsync();

        #endregion

        #region 获取节点信息，获取方法信息，获取Emit委托
        /// <summary>
        /// 获取节点信息
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="nodeModel"></param>
        /// <returns></returns>
        bool TryGetNodeModel(string nodeGuid, out IFlowNode nodeModel);

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
        #endregion

        #region 类库依赖相关

        /// <summary>
        /// 从文件中加载Dll
        /// </summary>
        /// <param name="dllPath"></param>
        void LoadLibrary(string dllPath);

        /// <summary>
        /// 移除DLL
        /// </summary>
        /// <param name="assemblyFullName">程序集的名称</param>
        bool TryUnloadLibrary(string assemblyFullName);

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

        #region 远程相关
        /*/// <summary>
        /// 启动远程服务
        /// </summary>
        Task StartRemoteServerAsync(int port = 7525);

        /// <summary>
        /// 停止远程服务
        /// </summary>
        void StopRemoteServer();

        /// <summary>
        /// (适用于远程连接后获取环境的运行状态)获取当前环境的信息
        /// </summary>
        /// <returns></returns>
        Task<FlowEnvInfo> GetEnvInfoAsync();

        /// <summary>
        /// 加载远程环境
        /// </summary>
        /// <param name="addres">远程环境地址</param>
        /// <param name="port">远程环境端口</param>
        /// <param name="token">密码</param>
        Task<(bool, RemoteMsgUtil)> ConnectRemoteEnv(string addres, int port, string token);

        /// <summary>
        /// 退出远程环境
        /// </summary>
        void ExitRemoteEnv();
        */

        /// <summary>
        /// 启动远程服务
        /// </summary>
        /// <returns></returns>
        Task StartRemoteServerAsync(int port = 7525);

        /// <summary>
        /// （用于远程）通知节点属性变更
        /// </summary>
        /// <param name="nodeGuid">节点Guid</param>
        /// <param name="path">属性路径</param>
        /// <param name="value">属性值</param>
        /// <returns></returns>
        Task NotificationNodeValueChangeAsync(string nodeGuid, string path, object value);

        #endregion

        #region 节点中断、表达式（暂时没用）
#if false

        /// <summary>
        /// 设置节点中断
        /// </summary>
        /// <param name="nodeGuid">更改中断状态的节点Guid</param>
        /// <param name="isInterrup">是否中断</param>
        /// <returns></returns>
        Task<bool> SetNodeInterruptAsync(string nodeGuid, bool isInterrup);

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
        void SetMonitorObjState(string key, bool isMonitor);

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
        Task<CancelType> InterruptNode(); 
#endif
        #endregion


    }


}
