using Serein.Library;
using Serein.Library.Api;
using Serein.Library.FlowNode;
using Serein.Library.Utils;
using Serein.NodeFlow.Tool;
using System.Reflection;

namespace Serein.NodeFlow.Env
{
    /// <summary>
    /// 流程运行环境
    /// </summary>
    public class FlowEnvironment : IFlowEnvironment, IFlowEnvironmentEvent
    {
        public FlowEnvironment()
        {
            flowEnvironment = new LocalFlowEnvironment();
            // 默认使用本地环境
            currentFlowEnvironment = flowEnvironment;
            currentFlowEnvironmentEvent = flowEnvironment;
            SereinEnv.SetEnv(currentFlowEnvironment);
        }

        /// <summary>
        /// 本地环境
        /// </summary>
        private readonly LocalFlowEnvironment flowEnvironment;

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

        /// <inheritdoc/>
        public IFlowEnvironment CurrentEnv { get => currentFlowEnvironment; }
        /// <inheritdoc/>
        public UIContextOperation UIContextOperation => currentFlowEnvironment.UIContextOperation;
        /// <inheritdoc/>
        public NodeMVVMManagement NodeMVVMManagement => currentFlowEnvironment.NodeMVVMManagement;
        /// <inheritdoc/>
        public ISereinIOC IOC => currentFlowEnvironment.IOC;

        /// <inheritdoc/>
        public IFlowEnvironmentEvent Event => currentFlowEnvironment.Event;


        /// <inheritdoc/>
        public string EnvName => currentFlowEnvironment.EnvName;

        /// <inheritdoc/>
        public string ProjectFileLocation => currentFlowEnvironment.EnvName;

        /// <inheritdoc/>
        public bool IsGlobalInterrupt => currentFlowEnvironment.IsGlobalInterrupt;

        /// <inheritdoc/>
        public bool IsControlRemoteEnv => currentFlowEnvironment.IsControlRemoteEnv;

        /// <inheritdoc/>
        public InfoClass InfoClass { get => currentFlowEnvironment.InfoClass; set => currentFlowEnvironment.InfoClass = value; }

        /// <inheritdoc/>
        public RunState FlowState { get => currentFlowEnvironment.FlowState; set => currentFlowEnvironment.FlowState = value; }

        /// <inheritdoc/>
        public event LoadDllHandler OnDllLoad { 
            add { currentFlowEnvironmentEvent.OnDllLoad += value; }
            remove { currentFlowEnvironmentEvent.OnDllLoad -= value; }
        }

        /// <inheritdoc/>
        public event ProjectLoadedHandler OnProjectLoaded
        {
            add { currentFlowEnvironmentEvent.OnProjectLoaded += value; }
            remove { currentFlowEnvironmentEvent.OnProjectLoaded -= value; }
        }

        /// <inheritdoc/>
        public event ProjectSavingHandler? OnProjectSaving
        {
            add { currentFlowEnvironmentEvent.OnProjectSaving += value; }
            remove { currentFlowEnvironmentEvent.OnProjectSaving -= value; }
        }

        /// <inheritdoc/>
        public event NodeConnectChangeHandler OnNodeConnectChange
        {
            add { currentFlowEnvironmentEvent.OnNodeConnectChange += value; }
            remove { currentFlowEnvironmentEvent.OnNodeConnectChange -= value; }
        }

        /// <inheritdoc/>
        public event CanvasCreateHandler OnCanvasCreate
        {
            add { currentFlowEnvironmentEvent.OnCanvasCreate += value; }
            remove { currentFlowEnvironmentEvent.OnCanvasCreate -= value; }
        }

        /// <inheritdoc/>
        public event CanvasRemoveHandler OnCanvasRemove
        {
            add { currentFlowEnvironmentEvent.OnCanvasRemove += value; }
            remove { currentFlowEnvironmentEvent.OnCanvasRemove -= value; }
        }

        /// <inheritdoc/>
        public event NodeCreateHandler OnNodeCreate
        {
            add { currentFlowEnvironmentEvent.OnNodeCreate += value; }
            remove { currentFlowEnvironmentEvent.OnNodeCreate -= value; }
        }

        /// <inheritdoc/>
        public event NodeRemoveHandler OnNodeRemove
        {
            add { currentFlowEnvironmentEvent.OnNodeRemove += value; }
            remove { currentFlowEnvironmentEvent.OnNodeRemove -= value; }
        }

        /// <inheritdoc/>
        public event NodePlaceHandler OnNodePlace
        {
            add { currentFlowEnvironmentEvent.OnNodePlace += value; }
            remove { currentFlowEnvironmentEvent.OnNodePlace -= value; }
        }

        /// <inheritdoc/>
        public event NodeTakeOutHandler OnNodeTakeOut
        {
            add { currentFlowEnvironmentEvent.OnNodeTakeOut += value; }
            remove { currentFlowEnvironmentEvent.OnNodeTakeOut -= value; }
        }

        /// <inheritdoc/>
        public event StartNodeChangeHandler OnStartNodeChange
        {
            add { currentFlowEnvironmentEvent.OnStartNodeChange += value; }
            remove { currentFlowEnvironmentEvent.OnStartNodeChange -= value; }
        }

        /// <inheritdoc/>
        public event FlowRunCompleteHandler OnFlowRunComplete
        {
            add { currentFlowEnvironmentEvent.OnFlowRunComplete += value; }
            remove { currentFlowEnvironmentEvent.OnFlowRunComplete -= value; }
        }

        /// <inheritdoc/>
        public event MonitorObjectChangeHandler OnMonitorObjectChange
        {
            add { currentFlowEnvironmentEvent.OnMonitorObjectChange += value; }
            remove { currentFlowEnvironmentEvent.OnMonitorObjectChange -= value; }
        }

        /// <inheritdoc/>
        public event NodeInterruptStateChangeHandler OnNodeInterruptStateChange
        {
            add { currentFlowEnvironmentEvent.OnNodeInterruptStateChange += value; }
            remove { currentFlowEnvironmentEvent.OnNodeInterruptStateChange -= value; }
        }

        /// <inheritdoc/>
        public event ExpInterruptTriggerHandler OnInterruptTrigger
        {
            add { currentFlowEnvironmentEvent.OnInterruptTrigger += value; }
            remove { currentFlowEnvironmentEvent.OnInterruptTrigger -= value; }
        }

        /// <inheritdoc/>
        public event IOCMembersChangedHandler OnIOCMembersChanged
        {
            add { currentFlowEnvironmentEvent.OnIOCMembersChanged += value; }
            remove { currentFlowEnvironmentEvent.OnIOCMembersChanged -= value; }
        }

        /// <inheritdoc/>
        public event NodeLocatedHandler OnNodeLocated
        {
            add { currentFlowEnvironmentEvent.OnNodeLocated += value; }
            remove { currentFlowEnvironmentEvent.OnNodeLocated -= value; }
        }

        /// <inheritdoc/>
        public event NodeMovedHandler OnNodeMoved
        {
            add { currentFlowEnvironmentEvent.OnNodeMoved += value; }
            remove { currentFlowEnvironmentEvent.OnNodeMoved -= value; }
        }

        /// <inheritdoc/>
        public event EnvOutHandler OnEnvOut
        {
            add { currentFlowEnvironmentEvent.OnEnvOut += value; }
            remove { currentFlowEnvironmentEvent.OnEnvOut -= value; }
        }



        /// <inheritdoc/>
        public void ActivateFlipflopNode(string nodeGuid)
        {
            currentFlowEnvironment.ActivateFlipflopNode(nodeGuid);
        }

        /// <inheritdoc/>
        public async Task<FlowCanvasDetailsInfo> CreateCanvasAsync(string canvasName, int width, int height)
        {
            return await currentFlowEnvironment.CreateCanvasAsync(canvasName, width, height);
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveCanvasAsync(string canvasGuid)
        {
            return await currentFlowEnvironment.RemoveCanvasAsync(canvasGuid);
        }

        /// <inheritdoc/>
        public async Task<bool> ConnectInvokeNodeAsync(string canvasGuid,
                                                       string fromNodeGuid,
                                                       string toNodeGuid,
                                                       JunctionType fromNodeJunctionType,
                                                       JunctionType toNodeJunctionType,
                                                       ConnectionInvokeType invokeType)
        {
            return await currentFlowEnvironment.ConnectInvokeNodeAsync(canvasGuid, fromNodeGuid, toNodeGuid, fromNodeJunctionType, toNodeJunctionType, invokeType);
        }

        /// <inheritdoc/>
        public async Task<bool> ConnectArgSourceNodeAsync(string canvasGuid,
                                                          string fromNodeGuid,
                                                          string toNodeGuid,
                                                          JunctionType fromNodeJunctionType,
                                                          JunctionType toNodeJunctionType,
                                                          ConnectionArgSourceType argSourceType,
                                                          int argIndex)
        {
            return await currentFlowEnvironment.ConnectArgSourceNodeAsync(canvasGuid, fromNodeGuid, toNodeGuid, fromNodeJunctionType, toNodeJunctionType, argSourceType, argIndex);
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public async Task LoadNodeInfosAsync(List<NodeInfo> nodeInfos)
        {
            SetProjectLoadingFlag(false);
            await currentFlowEnvironment.LoadNodeInfosAsync(nodeInfos); // 装饰器调用
            SetProjectLoadingFlag(true);
        }

        /// <inheritdoc/>
        public async Task<NodeInfo> CreateNodeAsync(string canvasGuid, NodeControlType nodeBase, PositionOfUI position, MethodDetailsInfo methodDetailsInfo = null)
        {
            SetProjectLoadingFlag(false);
            var result = await currentFlowEnvironment.CreateNodeAsync(canvasGuid, nodeBase, position, methodDetailsInfo); // 装饰器调用
            SetProjectLoadingFlag(true);
            return result;
        }

        /// <inheritdoc/>
        public async Task<bool> PlaceNodeToContainerAsync(string canvasGuid, string nodeGuid, string containerNodeGuid)
        {
            SetProjectLoadingFlag(false);
            var result = await currentFlowEnvironment.PlaceNodeToContainerAsync(canvasGuid, nodeGuid, containerNodeGuid); // 装饰器调用
            SetProjectLoadingFlag(true);
            return result;
        }

        /// <inheritdoc/>
        public async Task<bool> TakeOutNodeToContainerAsync(string canvasGuid, string nodeGuid)
        {
            SetProjectLoadingFlag(false);
            var result = await currentFlowEnvironment.TakeOutNodeToContainerAsync(canvasGuid,nodeGuid); // 装饰器调用
            SetProjectLoadingFlag(true);
            return result;
        }

        /// <inheritdoc/>
        public async Task<bool> ExitFlowAsync()
        {
            return await currentFlowEnvironment.ExitFlowAsync();
        }

        /// <inheritdoc/>
        public void ExitRemoteEnv()
        {
            currentFlowEnvironment.ExitRemoteEnv();
        }


        /// <inheritdoc/>
        public async Task<FlowEnvInfo> GetEnvInfoAsync()
        {
            return await currentFlowEnvironment.GetEnvInfoAsync();
        }

        /// <inheritdoc/>
        public async Task<SereinProjectData> GetProjectInfoAsync()
        {
            return await currentFlowEnvironment.GetProjectInfoAsync();
        }

        /// <inheritdoc/>
        public void LoadLibrary(string dllPath)
        {
            currentFlowEnvironment.LoadLibrary(dllPath);
        }

        /// <inheritdoc/>
        public void SaveProject()
        {
            currentFlowEnvironment.SaveProject();
        }

        /// <inheritdoc/>
        public void LoadProject(FlowEnvInfo flowEnvInfo, string filePath)
        {
            if (flowEnvInfo is null) return;
            SetProjectLoadingFlag(false);
            currentFlowEnvironment.LoadProject(flowEnvInfo, filePath);
            SetProjectLoadingFlag(true);
        }

        /// <inheritdoc/>
        public void MonitorObjectNotification(string nodeGuid, object monitorData, MonitorObjectEventArgs.ObjSourceType sourceType)
        {
            currentFlowEnvironment.MonitorObjectNotification(nodeGuid, monitorData, sourceType);
        }

        /// <inheritdoc/>
        public void MoveNode(string canvasGuid, string nodeGuid, double x, double y)
        {
            currentFlowEnvironment.MoveNode(canvasGuid, nodeGuid, x, y);
        }

        /// <inheritdoc/>
        public void NodeLocated(string nodeGuid)
        {
            currentFlowEnvironment.NodeLocated(nodeGuid);
        }

        /// <inheritdoc/>
        public bool TryUnloadLibrary(string assemblyName)
        {
            return currentFlowEnvironment.TryUnloadLibrary(assemblyName);
        }

        /// <inheritdoc/>
        public async Task<bool> SetConnectPriorityInvoke(string fromNodeGuid, string toNodeGuid, ConnectionInvokeType connectionType)
        {
            return await currentFlowEnvironment.SetConnectPriorityInvoke(fromNodeGuid, toNodeGuid, connectionType);
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveConnectInvokeAsync(string canvasGuid, string fromNodeGuid, string toNodeGuid, ConnectionInvokeType connectionType)
        {
            return await currentFlowEnvironment.RemoveConnectInvokeAsync(canvasGuid, fromNodeGuid, toNodeGuid, connectionType);
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveConnectArgSourceAsync(string canvasGuid, string fromNodeGuid, string toNodeGuid, int argIndex)
        {
            return await currentFlowEnvironment.RemoveConnectArgSourceAsync(canvasGuid, fromNodeGuid, toNodeGuid, argIndex);
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveNodeAsync(string canvasGuid, string nodeGuid)
        {
          return await  currentFlowEnvironment.RemoveNodeAsync(canvasGuid, nodeGuid);
        }


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

        /// <inheritdoc/>
        public async Task<string> SetStartNodeAsync(string canvasGuid, string nodeGuid)
        {
            return await currentFlowEnvironment.SetStartNodeAsync(canvasGuid, nodeGuid);
        }

        /// <inheritdoc/>
        public async Task<bool> StartFlowAsync(string[] canvasGuids)
        {
            return await currentFlowEnvironment.StartFlowAsync(canvasGuids);
        }

        /// <inheritdoc/>
        public async Task<bool> StartFlowFromSelectNodeAsync(string startNodeGuid)
        {
           return await currentFlowEnvironment.StartFlowFromSelectNodeAsync(startNodeGuid);
        }
       
        /// <inheritdoc/>
        public async Task StartRemoteServerAsync(int port = 7525)
        {
            await currentFlowEnvironment.StartRemoteServerAsync(port);
        }

        /// <inheritdoc/>
        public void StopRemoteServer()
        {
            currentFlowEnvironment.StopRemoteServer();
        }

        /// <inheritdoc/>
        public void TerminateFlipflopNode(string nodeGuid)
        {
            currentFlowEnvironment.TerminateFlipflopNode(nodeGuid);
        }

        /// <inheritdoc/>
        public void TriggerInterrupt(string nodeGuid, string expression, InterruptTriggerEventArgs.InterruptTriggerType type)
        {
            currentFlowEnvironment.TriggerInterrupt(nodeGuid, expression, type);
        }

        /// <inheritdoc/>
        public void SetUIContextOperation(UIContextOperation uiContextOperation)
        {
            currentFlowEnvironment.SetUIContextOperation(uiContextOperation);
        }

        /// <inheritdoc/>
        public void UseExternalIOC(ISereinIOC ioc)
        {
            currentFlowEnvironment.UseExternalIOC(ioc);
        }

        /// <inheritdoc/>
        public bool TryGetNodeModel(string nodeGuid, out IFlowNode nodeModel)
        {
            return currentFlowEnvironment.TryGetNodeModel(nodeGuid, out nodeModel);
        }

        /// <inheritdoc/>
        public bool TryGetDelegateDetails(string libraryName, string methodName, out DelegateDetails del)
        {
            return currentFlowEnvironment.TryGetDelegateDetails(libraryName, methodName, out del);
        }

        /// <inheritdoc/>
        public bool TryGetMethodDetailsInfo(string libraryName, string methodName, out MethodDetailsInfo mdInfo)
        {
            return currentFlowEnvironment.TryGetMethodDetailsInfo(libraryName, methodName, out mdInfo);
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public async Task<bool> ChangeParameter(string nodeGuid, bool isAdd, int paramIndex)
        {
            return await currentFlowEnvironment.ChangeParameter(nodeGuid, isAdd, paramIndex); 
        }

        #region 流程依赖类库的接口

        /// <inheritdoc/>
        public bool LoadNativeLibraryOfRuning(string file)
        {
            return currentFlowEnvironment.LoadNativeLibraryOfRuning(file);
        }

        /// <inheritdoc/>
        public void LoadAllNativeLibraryOfRuning(string path, bool isRecurrence = true)
        {
            currentFlowEnvironment.LoadAllNativeLibraryOfRuning(path,isRecurrence);
        }

        #endregion

    }
}
