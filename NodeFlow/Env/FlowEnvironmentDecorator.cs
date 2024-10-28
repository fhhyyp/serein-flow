using Serein.Library;
using Serein.Library.Api;
using Serein.Library.FlowNode;
using Serein.Library.Utils;

namespace Serein.NodeFlow.Env
{
    /// <summary>
    /// 自动管理本地与远程的环境
    /// </summary>
    public class FlowEnvironmentDecorator : IFlowEnvironment, ISereinIOC
    {
        public FlowEnvironmentDecorator(UIContextOperation uiContextOperation)
        {
            flowEnvironment = new FlowEnvironment(uiContextOperation);
            // 默认使用本地环境
            currentFlowEnvironment = flowEnvironment;
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
        /// 管理当前环境
        /// </summary>

        private IFlowEnvironment currentFlowEnvironment;


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

        public bool IsGlobalInterrupt => currentFlowEnvironment.IsGlobalInterrupt;

        public bool IsLcR => currentFlowEnvironment.IsLcR;

        public bool IsRcL => currentFlowEnvironment.IsRcL;

        public RunState FlowState { get => currentFlowEnvironment.FlowState; set => currentFlowEnvironment.FlowState = value; }
        public RunState FlipFlopState { get => currentFlowEnvironment.FlipFlopState; set => currentFlowEnvironment.FlipFlopState = value; }

        public event LoadDllHandler OnDllLoad { 
            add { currentFlowEnvironment.OnDllLoad += value; }
            remove { currentFlowEnvironment.OnDllLoad -= value; }
        }
        public event ProjectLoadedHandler OnProjectLoaded
        {
            add { currentFlowEnvironment.OnProjectLoaded += value; }
            remove { currentFlowEnvironment.OnProjectLoaded -= value; }
        }

        public event NodeConnectChangeHandler OnNodeConnectChange
        {
            add { currentFlowEnvironment.OnNodeConnectChange += value; }
            remove { currentFlowEnvironment.OnNodeConnectChange -= value; }
        }

        public event NodeCreateHandler OnNodeCreate
        {
            add { currentFlowEnvironment.OnNodeCreate += value; }
            remove { currentFlowEnvironment.OnNodeCreate -= value; }
        }

        public event NodeRemoveHandler OnNodeRemove
        {
            add { currentFlowEnvironment.OnNodeRemove += value; }
            remove { currentFlowEnvironment.OnNodeRemove -= value; }
        }

        public event StartNodeChangeHandler OnStartNodeChange
        {
            add { currentFlowEnvironment.OnStartNodeChange += value; }
            remove { currentFlowEnvironment.OnStartNodeChange -= value; }
        }

        public event FlowRunCompleteHandler OnFlowRunComplete
        {
            add { currentFlowEnvironment.OnFlowRunComplete += value; }
            remove { currentFlowEnvironment.OnFlowRunComplete -= value; }
        }

        public event MonitorObjectChangeHandler OnMonitorObjectChange
        {
            add { currentFlowEnvironment.OnMonitorObjectChange += value; }
            remove { currentFlowEnvironment.OnMonitorObjectChange -= value; }
        }

        public event NodeInterruptStateChangeHandler OnNodeInterruptStateChange
        {
            add { currentFlowEnvironment.OnNodeInterruptStateChange += value; }
            remove { currentFlowEnvironment.OnNodeInterruptStateChange -= value; }
        }

        public event ExpInterruptTriggerHandler OnInterruptTrigger
        {
            add { currentFlowEnvironment.OnInterruptTrigger += value; }
            remove { currentFlowEnvironment.OnInterruptTrigger -= value; }
        }

        public event IOCMembersChangedHandler OnIOCMembersChanged
        {
            add { currentFlowEnvironment.OnIOCMembersChanged += value; }
            remove { currentFlowEnvironment.OnIOCMembersChanged -= value; }
        }

        public event NodeLocatedHandler OnNodeLocated
        {
            add { currentFlowEnvironment.OnNodeLocated += value; }
            remove { currentFlowEnvironment.OnNodeLocated -= value; }
        }

        public event NodeMovedHandler OnNodeMoved
        {
            add { currentFlowEnvironment.OnNodeMoved += value; }
            remove { currentFlowEnvironment.OnNodeMoved -= value; }
        }

        public event EnvOutHandler OnEnvOut
        {
            add { currentFlowEnvironment.OnEnvOut += value; }
            remove { currentFlowEnvironment.OnEnvOut -= value; }
        }




        public void ActivateFlipflopNode(string nodeGuid)
        {
            currentFlowEnvironment.ActivateFlipflopNode(nodeGuid);
        }

        public async Task<bool> AddInterruptExpressionAsync(string key, string expression)
        {
            return await currentFlowEnvironment.AddInterruptExpressionAsync(key, expression);
        }


        public async Task<(bool, string[])> CheckObjMonitorStateAsync(string key)
        {
            return await currentFlowEnvironment.CheckObjMonitorStateAsync(key);
        }

        public void ClearAll()
        {
            currentFlowEnvironment.ClearAll();
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

        public async Task<NodeInfo> CreateNodeAsync(NodeControlType nodeBase, PositionOfUI position, MethodDetailsInfo methodDetailsInfo = null)
        {
            SetProjectLoadingFlag(false);
            var result = await currentFlowEnvironment.CreateNodeAsync(nodeBase, position, methodDetailsInfo); // 装饰器调用
            SetProjectLoadingFlag(true);
            return result;
        }




        public void ExitFlow()
        {
            currentFlowEnvironment.ExitFlow();
        }

        public void ExitRemoteEnv()
        {
            currentFlowEnvironment.ExitRemoteEnv();
        }


        public async Task<FlowEnvInfo> GetEnvInfoAsync()
        {
            return await currentFlowEnvironment.GetEnvInfoAsync();
        }

        public async Task<ChannelFlowInterrupt.CancelType> GetOrCreateGlobalInterruptAsync()
        {
            return await currentFlowEnvironment.GetOrCreateGlobalInterruptAsync();
        }

        public async Task<SereinProjectData> GetProjectInfoAsync()
        {
            return await currentFlowEnvironment.GetProjectInfoAsync();
        }


        public void LoadDll(string dllPath)
        {
            currentFlowEnvironment.LoadDll(dllPath);
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


        public bool RemoteDll(string assemblyFullName)
        {
            return currentFlowEnvironment.RemoteDll(assemblyFullName);
        }

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


        public void SetConsoleOut()
        {
            currentFlowEnvironment.SetConsoleOut();
        }

        public void SetMonitorObjState(string key, bool isMonitor)
        {
            currentFlowEnvironment.SetMonitorObjState(key, isMonitor);
        }

        public async Task<bool> SetNodeInterruptAsync(string nodeGuid, bool isInterrupt)
        {
            return await currentFlowEnvironment.SetNodeInterruptAsync(nodeGuid, isInterrupt);
        }

        public void SetStartNode(string nodeGuid)
        {
            currentFlowEnvironment.SetStartNode(nodeGuid);
        }

        public async Task StartAsync()
        {
            await currentFlowEnvironment.StartAsync();
        }

        public async Task StartAsyncInSelectNode(string startNodeGuid)
        {
            await currentFlowEnvironment.StartAsyncInSelectNode(startNodeGuid);
        }

        public async Task<object> InvokeNodeAsync( string nodeGuid)
        {
            return await currentFlowEnvironment.InvokeNodeAsync( nodeGuid);
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

        public bool TryGetDelegateDetails(string methodName, out DelegateDetails del)
        {
            return currentFlowEnvironment.TryGetDelegateDetails(methodName, out del);
        }

        public bool TryGetMethodDetailsInfo(string methodName, out MethodDetailsInfo mdInfo)
        {
            return currentFlowEnvironment.TryGetMethodDetailsInfo(methodName, out mdInfo);
        }

        public void WriteLineObjToJson(object obj)
        {
            currentFlowEnvironment.WriteLineObjToJson(obj);
        }


        public async Task NotificationNodeValueChangeAsync(string nodeGuid, string path, object value)
        {
            if (!IsLoadingProject())
            {
                return;
            }
            await currentFlowEnvironment.NotificationNodeValueChangeAsync(nodeGuid, path, value);
        }




        #region IOC容器
        public ISereinIOC Build()
        {
            return IOC.Build();
        }

        public bool CustomRegisterInstance(string key, object instance, bool needInjectProperty = true)
        {
            return IOC.CustomRegisterInstance(key, instance, needInjectProperty);
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
