using Serein.Library;
using Serein.Library.Api;
using Serein.Library.FlowNode;
using Serein.Library.Utils;
using Serein.NodeFlow.Tool;
using System.Reflection;

namespace Serein.NodeFlow.Env
{
    /// <summary>
    /// 自动管理本地与远程的环境
    /// </summary>
    public class FlowEnvironmentDecorator : IFlowEnvironment, IFlowEnvironmentEvent, ISereinIOC
    {
        public FlowEnvironmentDecorator()
        {
            flowEnvironment = new FlowEnvironment();
            // 默认使用本地环境
            currentFlowEnvironment = flowEnvironment;
            currentFlowEnvironmentEvent = flowEnvironment;
            SereinEnv.SetEnv(currentFlowEnvironment);
        }

        /// <summary>
        /// 本地环境
        /// </summary>
        private readonly FlowEnvironment flowEnvironment;

        /// <summary>
        /// 远程环境
        /// </summary>
        private RemoteFlowEnvironment remoteFlowEnvironment;

        /// <summary>
        /// 本地环境事件
        /// </summary>
        private readonly IFlowEnvironmentEvent flowEnvironmentEvent;

        /// <summary>
        /// 远程环境事件
        /// </summary>
        private IFlowEnvironmentEvent remoteFlowEnvironmentEvent;


        /// <summary>
        /// 管理当前环境
        /// </summary>

        private IFlowEnvironment currentFlowEnvironment;

        /// <summary>
        /// 管理当前环境事件
        /// </summary>
        private IFlowEnvironmentEvent currentFlowEnvironmentEvent;



        private int _loadingProjectFlag = 0; // 使用原子自增代替锁
        /// <summary>
        /// 传入false时，将停止数据通知。传入true时，
        /// </summary>
        /// <param name="value"></param>
        public void SetProjectLoadingFlag(bool value)
        {
            Interlocked.Exchange(ref _loadingProjectFlag, value ? 1 : 0);
        }
        /// <summary>
        /// 判断是否正在加载项目
        /// </summary>
        /// <returns></returns>
        public bool IsLoadingProject()
        {
            return Interlocked.CompareExchange(ref _loadingProjectFlag, 1, 1) == 1;
        }




        /// <summary>
        /// 当前环境，用于切换远程与本地环境
        /// </summary>
        public IFlowEnvironment CurrentEnv { get => currentFlowEnvironment; }

        public UIContextOperation UIContextOperation => currentFlowEnvironment.UIContextOperation;

        public ISereinIOC IOC => (ISereinIOC)currentFlowEnvironment;
  
   
        public string EnvName => currentFlowEnvironment.EnvName;
        public string ProjectFileLocation => currentFlowEnvironment.EnvName;

        public bool IsGlobalInterrupt => currentFlowEnvironment.IsGlobalInterrupt;

        public bool IsControlRemoteEnv => currentFlowEnvironment.IsControlRemoteEnv;

        /// <summary>
        /// 信息输出等级
        /// </summary>
        public InfoClass InfoClass { get => currentFlowEnvironment.InfoClass; set => currentFlowEnvironment.InfoClass = value; }
        public RunState FlowState { get => currentFlowEnvironment.FlowState; set => currentFlowEnvironment.FlowState = value; }
        public RunState FlipFlopState { get => currentFlowEnvironment.FlipFlopState; set => currentFlowEnvironment.FlipFlopState = value; }

        public event LoadDllHandler OnDllLoad { 
            add { currentFlowEnvironmentEvent.OnDllLoad += value; }
            remove { currentFlowEnvironmentEvent.OnDllLoad -= value; }
        }

        public event ProjectLoadedHandler OnProjectLoaded
        {
            add { currentFlowEnvironmentEvent.OnProjectLoaded += value; }
            remove { currentFlowEnvironmentEvent.OnProjectLoaded -= value; }
        }

        /// <summary>
        /// 项目准备保存
        /// </summary>
        public event ProjectSavingHandler? OnProjectSaving
        {
            add { currentFlowEnvironmentEvent.OnProjectSaving += value; }
            remove { currentFlowEnvironmentEvent.OnProjectSaving -= value; }
        }


        public event NodeConnectChangeHandler OnNodeConnectChange
        {
            add { currentFlowEnvironmentEvent.OnNodeConnectChange += value; }
            remove { currentFlowEnvironmentEvent.OnNodeConnectChange -= value; }
        }

        public event NodeCreateHandler OnNodeCreate
        {
            add { currentFlowEnvironmentEvent.OnNodeCreate += value; }
            remove { currentFlowEnvironmentEvent.OnNodeCreate -= value; }
        }

        public event NodeRemoveHandler OnNodeRemove
        {
            add { currentFlowEnvironmentEvent.OnNodeRemove += value; }
            remove { currentFlowEnvironmentEvent.OnNodeRemove -= value; }
        }

        public event NodePlaceHandler OnNodePlace
        {
            add { currentFlowEnvironmentEvent.OnNodePlace += value; }
            remove { currentFlowEnvironmentEvent.OnNodePlace -= value; }
        }

        public event NodeTakeOutHandler OnNodeTakeOut
        {
            add { currentFlowEnvironmentEvent.OnNodeTakeOut += value; }
            remove { currentFlowEnvironmentEvent.OnNodeTakeOut -= value; }
        }

        public event StartNodeChangeHandler OnStartNodeChange
        {
            add { currentFlowEnvironmentEvent.OnStartNodeChange += value; }
            remove { currentFlowEnvironmentEvent.OnStartNodeChange -= value; }
        }

        public event FlowRunCompleteHandler OnFlowRunComplete
        {
            add { currentFlowEnvironmentEvent.OnFlowRunComplete += value; }
            remove { currentFlowEnvironmentEvent.OnFlowRunComplete -= value; }
        }

        public event MonitorObjectChangeHandler OnMonitorObjectChange
        {
            add { currentFlowEnvironmentEvent.OnMonitorObjectChange += value; }
            remove { currentFlowEnvironmentEvent.OnMonitorObjectChange -= value; }
        }

        public event NodeInterruptStateChangeHandler OnNodeInterruptStateChange
        {
            add { currentFlowEnvironmentEvent.OnNodeInterruptStateChange += value; }
            remove { currentFlowEnvironmentEvent.OnNodeInterruptStateChange -= value; }
        }

        public event ExpInterruptTriggerHandler OnInterruptTrigger
        {
            add { currentFlowEnvironmentEvent.OnInterruptTrigger += value; }
            remove { currentFlowEnvironmentEvent.OnInterruptTrigger -= value; }
        }

        public event IOCMembersChangedHandler OnIOCMembersChanged
        {
            add { currentFlowEnvironmentEvent.OnIOCMembersChanged += value; }
            remove { currentFlowEnvironmentEvent.OnIOCMembersChanged -= value; }
        }

        public event NodeLocatedHandler OnNodeLocated
        {
            add { currentFlowEnvironmentEvent.OnNodeLocated += value; }
            remove { currentFlowEnvironmentEvent.OnNodeLocated -= value; }
        }

        public event NodeMovedHandler OnNodeMoved
        {
            add { currentFlowEnvironmentEvent.OnNodeMoved += value; }
            remove { currentFlowEnvironmentEvent.OnNodeMoved -= value; }
        }

        public event EnvOutHandler OnEnvOut
        {
            add { currentFlowEnvironmentEvent.OnEnvOut += value; }
            remove { currentFlowEnvironmentEvent.OnEnvOut -= value; }
        }




        public void ActivateFlipflopNode(string nodeGuid)
        {
            currentFlowEnvironment.ActivateFlipflopNode(nodeGuid);
        }

        


        /// <summary>
        /// 在两个节点之间创建连接关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点Guid</param>
        /// <param name="toNodeGuid">目标节点Guid</param>
        /// <param name="fromNodeJunctionType">起始节点控制点</param>
        /// <param name="toNodeJunctionType">目标节点控制点</param>
        /// <param name="invokeType">决定了方法执行后的后继行为</param>
        public async Task<bool> ConnectInvokeNodeAsync(string fromNodeGuid,
                                                 string toNodeGuid,
                                                 JunctionType fromNodeJunctionType,
                                                 JunctionType toNodeJunctionType,
                                                 ConnectionInvokeType invokeType)
        {
            return await currentFlowEnvironment.ConnectInvokeNodeAsync(fromNodeGuid, toNodeGuid, fromNodeJunctionType, toNodeJunctionType, invokeType);
        }


        /// <summary>
        /// 在两个节点之间创建连接关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点Guid</param>
        /// <param name="toNodeGuid">目标节点Guid</param>
        /// <param name="fromNodeJunctionType">起始节点控制点</param>
        /// <param name="toNodeJunctionType">目标节点控制点</param>
        /// <param name="argSourceType">决定了方法参数来源</param>
        /// <param name="argIndex">设置第几个参数</param>
        public async Task<bool> ConnectArgSourceNodeAsync(string fromNodeGuid,
                                                 string toNodeGuid,
                                                 JunctionType fromNodeJunctionType,
                                                 JunctionType toNodeJunctionType,
                                                 ConnectionArgSourceType argSourceType,
                                                 int argIndex)
        {
            return await currentFlowEnvironment.ConnectArgSourceNodeAsync(fromNodeGuid, toNodeGuid, fromNodeJunctionType, toNodeJunctionType, argSourceType, argIndex);
        }


        /// <summary>
        /// 连接远程环境并自动切换环境
        /// </summary>
        /// <param name="addres"></param>
        /// <param name="port"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<(bool, RemoteMsgUtil)> ConnectRemoteEnv(string addres, int port, string token)
        {
            // 连接成功，切换远程环境
            (var isConnect, var remoteMsgUtil) = await currentFlowEnvironment.ConnectRemoteEnv(addres, port, token);
            if (isConnect)
            {
                
                remoteFlowEnvironment ??= new RemoteFlowEnvironment(remoteMsgUtil, this.UIContextOperation);
                currentFlowEnvironment = remoteFlowEnvironment;
            }
            return (isConnect, remoteMsgUtil);
        }

        /// <summary>
        /// 从节点信息集合批量加载节点控件
        /// </summary>
        /// <param name="List<NodeInfo>">节点信息</param>
        /// <param name="position">需要加载的位置</param>
        /// <returns></returns>
        public async Task LoadNodeInfosAsync(List<NodeInfo> nodeInfos)
        {
            SetProjectLoadingFlag(false);
            await currentFlowEnvironment.LoadNodeInfosAsync(nodeInfos); // 装饰器调用
            SetProjectLoadingFlag(true);
        }

        public async Task<NodeInfo> CreateNodeAsync(NodeControlType nodeBase, PositionOfUI position, MethodDetailsInfo methodDetailsInfo = null)
        {
            SetProjectLoadingFlag(false);
            var result = await currentFlowEnvironment.CreateNodeAsync(nodeBase, position, methodDetailsInfo); // 装饰器调用
            SetProjectLoadingFlag(true);
            return result;
        }


        /// <summary>
        /// 将节点放置在容器中
        /// </summary>
        /// <returns></returns>
        public async Task<bool> PlaceNodeToContainerAsync(string nodeGuid, string containerNodeGuid)
        {
            SetProjectLoadingFlag(false);
            var result = await currentFlowEnvironment.PlaceNodeToContainerAsync(nodeGuid, containerNodeGuid); // 装饰器调用
            SetProjectLoadingFlag(true);
            return result;
        }

        /// <summary>
        /// 将节点从容器中脱离
        /// </summary>
        /// <returns></returns>
        public async Task<bool> TakeOutNodeToContainerAsync(string nodeGuid)
        {
            SetProjectLoadingFlag(false);
            var result = await currentFlowEnvironment.TakeOutNodeToContainerAsync(nodeGuid); // 装饰器调用
            SetProjectLoadingFlag(true);
            return result;
        }


        public async Task<bool> ExitFlowAsync()
        {
            return await currentFlowEnvironment.ExitFlowAsync();
        }

        public void ExitRemoteEnv()
        {
            currentFlowEnvironment.ExitRemoteEnv();
        }


        public async Task<FlowEnvInfo> GetEnvInfoAsync()
        {
            return await currentFlowEnvironment.GetEnvInfoAsync();
        }

        
        public async Task<SereinProjectData> GetProjectInfoAsync()
        {
            return await currentFlowEnvironment.GetProjectInfoAsync();
        }


        public void LoadLibrary(string dllPath)
        {
            currentFlowEnvironment.LoadLibrary(dllPath);
        }

        /// <summary>
        /// 保存项目
        /// </summary>
        public void SaveProject()
        {
            currentFlowEnvironment.SaveProject();
        }

        public void LoadProject(FlowEnvInfo flowEnvInfo, string filePath)
        {
            if (flowEnvInfo is null) return;
            SetProjectLoadingFlag(false);
            currentFlowEnvironment.LoadProject(flowEnvInfo, filePath);
            SetProjectLoadingFlag(true);
        }

        public void MonitorObjectNotification(string nodeGuid, object monitorData, MonitorObjectEventArgs.ObjSourceType sourceType)
        {
            currentFlowEnvironment.MonitorObjectNotification(nodeGuid, monitorData, sourceType);
        }

        public void MoveNode(string nodeGuid, double x, double y)
        {
            currentFlowEnvironment.MoveNode(nodeGuid, x, y);
        }

        public void NodeLocated(string nodeGuid)
        {
            currentFlowEnvironment.NodeLocated(nodeGuid);
        }


        public bool TryUnloadLibrary(string assemblyName)
        {
            return currentFlowEnvironment.TryUnloadLibrary(assemblyName);
        }

        /// <summary>
        /// 设置两个节点某个类型的方法调用关系为优先调用
        /// </summary>
        /// <param name="fromNodeGuid">起始节点</param>
        /// <param name="toNodeGuid">目标节点</param>
        /// <param name="connectionType">连接关系</param>
        /// <returns>是否成功调用</returns>
        public async Task<bool> SetConnectPriorityInvoke(string fromNodeGuid, string toNodeGuid, ConnectionInvokeType connectionType)
        {
            return await currentFlowEnvironment.SetConnectPriorityInvoke(fromNodeGuid, toNodeGuid, connectionType);
        }
        /// <summary>
        /// 移除方法调用关系
        /// </summary>
        /// <param name="fromNodeGuid"></param>
        /// <param name="toNodeGuid"></param>
        /// <param name="connectionType"></param>
        /// <returns></returns>
        public async Task<bool> RemoveConnectInvokeAsync(string fromNodeGuid, string toNodeGuid, ConnectionInvokeType connectionType)
        {
            return await currentFlowEnvironment.RemoveConnectInvokeAsync(fromNodeGuid, toNodeGuid, connectionType);
        }

        /// <summary>
        /// 移除连接节点之间参数传递的关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点Guid</param>
        /// <param name="toNodeGuid">目标节点Guid</param>
        /// <param name="argIndex">连接到第几个参数</param>
        /// <param name="connectionArgSourceType">参数来源类型</param>
        public async Task<bool> RemoveConnectArgSourceAsync(string fromNodeGuid, string toNodeGuid, int argIndex)
        {
            return await currentFlowEnvironment.RemoveConnectArgSourceAsync(fromNodeGuid, toNodeGuid, argIndex);
        }

        public async Task<bool> RemoveNodeAsync(string nodeGuid)
        {
          return await  currentFlowEnvironment.RemoveNodeAsync(nodeGuid);
        }


        //public void SetConsoleOut()
        //{
        //    currentFlowEnvironment.SetConsoleOut();
        //}

        /// <summary>
        /// 输出信息
        /// </summary>
        /// <param name="message">日志内容</param>
        /// <param name="type">日志类别</param>
        /// <param name="class">日志级别</param>
        public void WriteLine(InfoType type, string message, InfoClass @class = InfoClass.Trivial)
        {
            currentFlowEnvironment.WriteLine(type,  message,  @class);
        }


        #region MyRegion
#if false
        public async Task<bool> AddInterruptExpressionAsync(string key, string expression)
        {
            return await currentFlowEnvironment.AddInterruptExpressionAsync(key, expression);
        }


        public async Task<(bool, string[])> CheckObjMonitorStateAsync(string key)
        {
            return await currentFlowEnvironment.CheckObjMonitorStateAsync(key);
        }
        public async Task<ChannelFlowInterrupt.CancelType> GetOrCreateGlobalInterruptAsync()
        {
            return await currentFlowEnvironment.InterruptNode();
        }

        public void SetMonitorObjState(string key, bool isMonitor)
        {
            currentFlowEnvironment.SetMonitorObjState(key, isMonitor);
        }

        public async Task<bool> SetNodeInterruptAsync(string nodeGuid, bool isInterrupt)
        {
            return await currentFlowEnvironment.SetNodeInterruptAsync(nodeGuid, isInterrupt);
        } 
#endif

        #endregion
        public async Task<string> SetStartNodeAsync(string nodeGuid)
        {
            return await currentFlowEnvironment.SetStartNodeAsync(nodeGuid);
        }

        public async Task<bool> StartFlowAsync()
        {
            return await currentFlowEnvironment.StartFlowAsync();
        }

        public async Task<bool> StartAsyncInSelectNode(string startNodeGuid)
        {
           return await currentFlowEnvironment.StartAsyncInSelectNode(startNodeGuid);
        }

        public async Task<object> InvokeNodeAsync(IDynamicContext context, string nodeGuid)
        {
            return await currentFlowEnvironment.InvokeNodeAsync(context, nodeGuid);
        }

        public async Task StartRemoteServerAsync(int port = 7525)
        {
            await currentFlowEnvironment.StartRemoteServerAsync(port);
        }

        public void StopRemoteServer()
        {
            currentFlowEnvironment.StopRemoteServer();
        }

        public void TerminateFlipflopNode(string nodeGuid)
        {
            currentFlowEnvironment.TerminateFlipflopNode(nodeGuid);
        }

        public void TriggerInterrupt(string nodeGuid, string expression, InterruptTriggerEventArgs.InterruptTriggerType type)
        {
            currentFlowEnvironment.TriggerInterrupt(nodeGuid, expression, type);
        }
        /// <summary>
        /// 设置在UI线程操作的线程上下文
        /// </summary>
        /// <param name="uiContextOperation"></param>
        public void SetUIContextOperation(UIContextOperation uiContextOperation)
        {
            currentFlowEnvironment.SetUIContextOperation(uiContextOperation);
        }
        public bool TryGetDelegateDetails(string libraryName, string methodName, out DelegateDetails del)
        {
            return currentFlowEnvironment.TryGetDelegateDetails(libraryName, methodName, out del);
        }

        public bool TryGetMethodDetailsInfo(string libraryName, string methodName, out MethodDetailsInfo mdInfo)
        {
            return currentFlowEnvironment.TryGetMethodDetailsInfo(libraryName, methodName, out mdInfo);
        }

        //public void WriteLineObjToJson(object obj)
        //{
        //    currentFlowEnvironment.WriteLineObjToJson(obj);
        //}

        /// <summary>
        /// （用于远程）通知节点属性变更
        /// </summary>
        /// <param name="nodeGuid">节点Guid</param>
        /// <param name="path">属性路径</param>
        /// <param name="value">属性值</param>
        /// <returns></returns>
        public async Task NotificationNodeValueChangeAsync(string nodeGuid, string path, object value)
        {
            if (!IsLoadingProject())
            {
                return;
            }
            if (currentFlowEnvironment.IsControlRemoteEnv)
            {
                await currentFlowEnvironment.NotificationNodeValueChangeAsync(nodeGuid, path, value);
            }
        }


        /// <summary>
        /// 改变可选参数的数目
        /// </summary>
        /// <param name="nodeGuid">对应的节点Guid</param>
        /// <param name="isAdd">true，增加参数；false，减少参数</param>
        /// <param name="paramIndex">以哪个参数为模板进行拷贝，或删去某个参数（该参数必须为可选参数）</param>
        /// <returns></returns>
        public async Task<bool> ChangeParameter(string nodeGuid, bool isAdd, int paramIndex)
        {
            return await currentFlowEnvironment.ChangeParameter(nodeGuid, isAdd, paramIndex); 
        }

        #region 流程依赖类库的接口
       

        /// <summary>
        /// 运行时加载
        /// </summary>
        /// <param name="file">文件名</param>
        /// <returns></returns>
        public bool LoadNativeLibraryOfRuning(string file)
        {
            return currentFlowEnvironment.LoadNativeLibraryOfRuning(file);
        }

        /// <summary>
        /// 运行时加载指定目录下的类库
        /// </summary>
        /// <param name="path">目录</param>
        /// <param name="isRecurrence">是否递归加载</param>
        public void LoadAllNativeLibraryOfRuning(string path, bool isRecurrence = true)
        {
            currentFlowEnvironment.LoadAllNativeLibraryOfRuning(path,isRecurrence);
        }

        #endregion

        #region IOC容器
        public ISereinIOC Build()
        {
            return IOC.Build();
        }

        public bool RegisterPersistennceInstance(string key, object instance)
        {
            return IOC.RegisterPersistennceInstance(key, instance);
        }

        public bool RegisterInstance(string key, object instance)
        {
            return IOC.RegisterInstance(key, instance);
        }

        public object Get(Type type)
        {
            return IOC.Get(type);
        }

        public T Get<T>()
        {
            return IOC.Get<T>();
        }

        public T Get<T>(string key)
        {
            return IOC.Get<T>(key);
        }

        public object Instantiate(Type type)
        {
            return IOC.Instantiate(type);
        }

        public T Instantiate<T>()
        {
            return IOC.Instantiate<T>();
        }

        public ISereinIOC Register(Type type, params object[] parameters)
        {
            return IOC.Register(type, parameters);
        }

        public ISereinIOC Register<T>(params object[] parameters)
        {
            return IOC.Register<T>(parameters);
        }

        public ISereinIOC Register<TService, TImplementation>(params object[] parameters) where TImplementation : TService
        {
            return IOC.Register<TService, TImplementation>(parameters);
        }

        public ISereinIOC Reset()
        {
            return IOC.Reset();
        }

        public ISereinIOC Run<T>(Action<T> action)
        {
            return IOC.Run(action);
        }

        public ISereinIOC Run<T1, T2>(Action<T1, T2> action)
        {
            return IOC.Run(action);
        }

        public ISereinIOC Run<T1, T2, T3>(Action<T1, T2, T3> action)
        {
            return IOC.Run(action);
        }

        public ISereinIOC Run<T1, T2, T3, T4>(Action<T1, T2, T3, T4> action)
        {
            return IOC.Run(action);
        }

        public ISereinIOC Run<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> action)
        {
            return IOC.Run(action);
        }

        public ISereinIOC Run<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> action)
        {
            return IOC.Run(action);
        }

        public ISereinIOC Run<T1, T2, T3, T4, T5, T6, T7>(Action<T1, T2, T3, T4, T5, T6, T7> action)
        {
            return IOC.Run(action);
        }

        public ISereinIOC Run<T1, T2, T3, T4, T5, T6, T7, T8>(Action<T1, T2, T3, T4, T5, T6, T7, T8> action)
        {
            return IOC.Run(action);
        }

        

        #endregion


    }
}
