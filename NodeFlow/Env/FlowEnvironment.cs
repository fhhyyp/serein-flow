using Serein.Library;
using Serein.Library.Api;
using Serein.Library.FlowNode;
using Serein.Library.Utils;
using Serein.NodeFlow.Services;
using Serein.NodeFlow.Tool;
using System.Reflection;

namespace Serein.NodeFlow.Env
{

    /*
     SetExportDirectory(string directory)
    UseRemoteEdit(string wspath)



    IFlowEnvironment env = new ();
    env.LoadProject()

    List<FlowApiInfo> apiInfos = env.GetInterfaceInfo();
    List<FlowEventInfo> enventInfos = env.GetEventInfo();


    flowApiService = env.GetFlowApiService();
    flowEventService = env.GetFlowEventService();


    object result = flowApiService.Invoke("", params);
    TResult result = flowApiService.Invoke<TResult>("", params);
    object result = await flowApiService.InvokeAsync("", params);
    TResult result = await flowApiService.InvokeAsync<TResult>("", params);

    flowEventService.Monitor("", (e) => {
        object data = e.EventData;
        Debug.Writeline(e.EventName);
    });
    
    
     */


    /// <summary>
    /// 流程运行环境
    /// </summary>
    public class FlowEnvironment : IFlowEnvironment
    {
        public FlowEnvironment()
        {
            ISereinIOC ioc = new SereinIOC();
            ioc.Reset()
               .Register<ISereinIOC>(()=> ioc) // 注册IOC
               .Register<IFlowEnvironment>(() => this)
               .Register<IFlowEnvironmentEvent, FlowEnvironmentEvent>()
               .Register<LocalFlowEnvironment>()
               .Register<FlowModelService>()
               .Register<FlowOperationService>()
               .Register<NodeMVVMService>()
               .Register<FlowLibraryService>()
               .Build();
            // 默认使用本地环境
            currentFlowEnvironment = ioc.Get<LocalFlowEnvironment>();
            currentFlowEnvironmentEvent = ioc.Get<IFlowEnvironmentEvent>();
            SereinEnv.SetEnv(currentFlowEnvironment);
        }

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
        public IFlowEnvironment CurrentEnv => currentFlowEnvironment;
        /// <inheritdoc/>
        public UIContextOperation UIContextOperation => currentFlowEnvironment.UIContextOperation;
        /// <inheritdoc/>
        public NodeMVVMService NodeMVVMManagement => currentFlowEnvironment.NodeMVVMManagement;

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
        public event LoadDllHandler DllLoad { 
            add { currentFlowEnvironmentEvent.DllLoad += value; }
            remove { currentFlowEnvironmentEvent.DllLoad -= value; }
        }

        /// <inheritdoc/>
        public event ProjectLoadedHandler ProjectLoaded
        {
            add { currentFlowEnvironmentEvent.ProjectLoaded += value; }
            remove { currentFlowEnvironmentEvent.ProjectLoaded -= value; }
        }

        /// <inheritdoc/>
        public event ProjectSavingHandler? ProjectSaving
        {
            add { currentFlowEnvironmentEvent.ProjectSaving += value; }
            remove { currentFlowEnvironmentEvent.ProjectSaving -= value; }
        }

        /// <inheritdoc/>
        public event NodeConnectChangeHandler NodeConnectChanged
        {
            add { currentFlowEnvironmentEvent.NodeConnectChanged += value; }
            remove { currentFlowEnvironmentEvent.NodeConnectChanged -= value; }
        }

        /// <inheritdoc/>
        public event CanvasCreateHandler CanvasCreated
        {
            add { currentFlowEnvironmentEvent.CanvasCreated += value; }
            remove { currentFlowEnvironmentEvent.CanvasCreated -= value; }
        }

        /// <inheritdoc/>
        public event CanvasRemoveHandler CanvasRemoved
        {
            add { currentFlowEnvironmentEvent.CanvasRemoved += value; }
            remove { currentFlowEnvironmentEvent.CanvasRemoved -= value; }
        }

        /// <inheritdoc/>
        public event NodeCreateHandler NodeCreated
        {
            add { currentFlowEnvironmentEvent.NodeCreated += value; }
            remove { currentFlowEnvironmentEvent.NodeCreated -= value; }
        }

        /// <inheritdoc/>
        public event NodeRemoveHandler NodeRemoved
        {
            add { currentFlowEnvironmentEvent.NodeRemoved += value; }
            remove { currentFlowEnvironmentEvent.NodeRemoved -= value; }
        }

        /// <inheritdoc/>
        public event NodePlaceHandler NodePlace
        {
            add { currentFlowEnvironmentEvent.NodePlace += value; }
            remove { currentFlowEnvironmentEvent.NodePlace -= value; }
        }

        /// <inheritdoc/>
        public event NodeTakeOutHandler NodeTakeOut
        {
            add { currentFlowEnvironmentEvent.NodeTakeOut += value; }
            remove { currentFlowEnvironmentEvent.NodeTakeOut -= value; }
        }

        /// <inheritdoc/>
        public event StartNodeChangeHandler StartNodeChanged
        {
            add { currentFlowEnvironmentEvent.StartNodeChanged += value; }
            remove { currentFlowEnvironmentEvent.StartNodeChanged -= value; }
        }

        /// <inheritdoc/>
        public event FlowRunCompleteHandler FlowRunComplete
        {
            add { currentFlowEnvironmentEvent.FlowRunComplete += value; }
            remove { currentFlowEnvironmentEvent.FlowRunComplete -= value; }
        }

        /// <inheritdoc/>
        public event MonitorObjectChangeHandler MonitorObjectChanged
        {
            add { currentFlowEnvironmentEvent.MonitorObjectChanged += value; }
            remove { currentFlowEnvironmentEvent.MonitorObjectChanged -= value; }
        }

        /// <inheritdoc/>
        public event NodeInterruptStateChangeHandler NodeInterruptStateChanged
        {
            add { currentFlowEnvironmentEvent.NodeInterruptStateChanged += value; }
            remove { currentFlowEnvironmentEvent.NodeInterruptStateChanged -= value; }
        }

        /// <inheritdoc/>
        public event ExpInterruptTriggerHandler InterruptTriggered
        {
            add { currentFlowEnvironmentEvent.InterruptTriggered += value; }
            remove { currentFlowEnvironmentEvent.InterruptTriggered -= value; }
        }

        /// <inheritdoc/>
        public event IOCMembersChangedHandler IOCMembersChanged
        {
            add { currentFlowEnvironmentEvent.IOCMembersChanged += value; }
            remove { currentFlowEnvironmentEvent.IOCMembersChanged -= value; }
        }

        /// <inheritdoc/>
        public event NodeLocatedHandler NodeLocated
        {
            add { currentFlowEnvironmentEvent.NodeLocated += value; }
            remove { currentFlowEnvironmentEvent.NodeLocated -= value; }
        }

        /// <inheritdoc/>
        public event EnvOutHandler EnvOutput
        {
            add { currentFlowEnvironmentEvent.EnvOutput += value; }
            remove { currentFlowEnvironmentEvent.EnvOutput -= value; }
        }



        /// <inheritdoc/>
        public void ActivateFlipflopNode(string nodeGuid)
        {
            currentFlowEnvironment.ActivateFlipflopNode(nodeGuid);
        }

        /// <inheritdoc/>
        public void CreateCanvas(string canvasName, int width, int height)
        {
            currentFlowEnvironment.CreateCanvas(canvasName, width, height);
        }

        /// <inheritdoc/>
        public void RemoveCanvas(string canvasGuid)
        {
             currentFlowEnvironment.RemoveCanvas(canvasGuid);
        }

        /// <inheritdoc/>
        public void ConnectInvokeNode(string canvasGuid,
                                                       string fromNodeGuid,
                                                       string toNodeGuid,
                                                       JunctionType fromNodeJunctionType,
                                                       JunctionType toNodeJunctionType,
                                                       ConnectionInvokeType invokeType)
        {
            currentFlowEnvironment.ConnectInvokeNode(canvasGuid, fromNodeGuid, toNodeGuid, fromNodeJunctionType, toNodeJunctionType, invokeType);
        }

        /// <inheritdoc/>
        public void ConnectArgSourceNode(string canvasGuid,
                                                          string fromNodeGuid,
                                                          string toNodeGuid,
                                                          JunctionType fromNodeJunctionType,
                                                          JunctionType toNodeJunctionType,
                                                          ConnectionArgSourceType argSourceType,
                                                          int argIndex)
        {
             currentFlowEnvironment.ConnectArgSourceNode(canvasGuid, fromNodeGuid, toNodeGuid, fromNodeJunctionType, toNodeJunctionType, argSourceType, argIndex);
        }

        /// <inheritdoc/>
        public async Task<(bool, RemoteMsgUtil)> ConnectRemoteEnv(string addres, int port, string token)
        {
            // 连接成功，切换远程环境
            (var isConnect, var remoteMsgUtil) = await currentFlowEnvironment.ConnectRemoteEnv(addres, port, token);
            if (isConnect)
            {
                
               /* remoteFlowEnvironment ??= new RemoteFlowEnvironment(remoteMsgUtil, this.Event,  this.UIContextOperation);
                currentFlowEnvironment = remoteFlowEnvironment;*/
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
        public void CreateNode(string canvasGuid, NodeControlType nodeBase, PositionOfUI position, MethodDetailsInfo methodDetailsInfo = null)
        {
            SetProjectLoadingFlag(false);
            currentFlowEnvironment.CreateNode(canvasGuid, nodeBase, position, methodDetailsInfo); // 装饰器调用
            SetProjectLoadingFlag(true);
        }

        /// <inheritdoc/>
        public void PlaceNodeToContainer(string canvasGuid, string nodeGuid, string containerNodeGuid)
        {
            SetProjectLoadingFlag(false);
             currentFlowEnvironment.PlaceNodeToContainer(canvasGuid, nodeGuid, containerNodeGuid); // 装饰器调用
            SetProjectLoadingFlag(true);
        }

        /// <inheritdoc/>
        public void TakeOutNodeToContainer(string canvasGuid, string nodeGuid)
        {
            SetProjectLoadingFlag(false);
            currentFlowEnvironment.TakeOutNodeToContainer(canvasGuid,nodeGuid); // 装饰器调用
            SetProjectLoadingFlag(true);
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

        /*/// <inheritdoc/>
        public void MoveNode(string canvasGuid, string nodeGuid, double x, double y)
        {
            currentFlowEnvironment.MoveNode(canvasGuid, nodeGuid, x, y);
        }
*/
        /// <inheritdoc/>
        public void NodeLocate(string nodeGuid)
        {
            currentFlowEnvironment.NodeLocate(nodeGuid);
        }

        /// <inheritdoc/>
        public bool TryUnloadLibrary(string assemblyName)
        {
            return currentFlowEnvironment.TryUnloadLibrary(assemblyName);
        }

        /// <inheritdoc/>
        public void SetConnectPriorityInvoke(string fromNodeGuid, string toNodeGuid, ConnectionInvokeType connectionType)
        {
            currentFlowEnvironment.SetConnectPriorityInvoke(fromNodeGuid, toNodeGuid, connectionType);
        }

        /// <inheritdoc/>
        public void RemoveInvokeConnect(string canvasGuid, string fromNodeGuid, string toNodeGuid, ConnectionInvokeType connectionType)
        {
           currentFlowEnvironment.RemoveInvokeConnect(canvasGuid, fromNodeGuid, toNodeGuid, connectionType);
        }

        /// <inheritdoc/>
        public void RemoveArgSourceConnect(string canvasGuid, string fromNodeGuid, string toNodeGuid, int argIndex)
        {
           currentFlowEnvironment.RemoveArgSourceConnect(canvasGuid, fromNodeGuid, toNodeGuid, argIndex);
        }

        /// <inheritdoc/>
        public void RemoveNode(string canvasGuid, string nodeGuid)
        {
           currentFlowEnvironment.RemoveNode(canvasGuid, nodeGuid);
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
        public void SetStartNode(string canvasGuid, string nodeGuid)
        {
            currentFlowEnvironment.SetStartNode(canvasGuid, nodeGuid);
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
            if(uiContextOperation is null)
            {
                return;
            }
            IOC.Register<UIContextOperation>(() => uiContextOperation).Build();
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
        public void ChangeParameter(string nodeGuid, bool isAdd, int paramIndex)
        {
            currentFlowEnvironment.ChangeParameter(nodeGuid, isAdd, paramIndex); 
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
