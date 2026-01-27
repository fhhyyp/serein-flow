using Serein.Extend.NewtonsoftJson;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Services;

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
        /// <summary>
        /// 流程运行环境构造函数
        /// </summary>
        public FlowEnvironment()
        {
            ISereinIOC ioc = new SereinIOC();
            ioc.Register<ISereinIOC>(() => ioc) // IOC容器接口
               .Register<IFlowEnvironment>(() => this) // 流程环境接口
               .Register<IFlowEnvironmentEvent, FlowEnvironmentEvent>() // 流程环境事件接口
               .Register<IFlowEdit, FlowEdit>() // 流程编辑接口
               .Register<IFlowControl, FlowControl>() // 流程控制接口
               .Register<IFlowLibraryService, FlowLibraryService>() // 流程库服务
               .Register<LocalFlowEnvironment>() // 本地环境
               .Register<FlowModelService>() // 节点/画布模型服务
               .Register<FlowCoreGenerateService>() // 代码生成
               .Register<FlowOperationService>() // 流程操作
               .Register<NodeMVVMService>() // 节点MVVM服务
               .Build();

            // 设置JSON解析器
            if (JsonHelper.Provider is null)
            {
                JsonHelper.UseJsonProvider(new NewtonsoftJsonProvider());
            }

            // 默认使用本地环境
            localFlowEnvironment = ioc.Get<LocalFlowEnvironment>();
            currentFlowEnvironmentEvent = ioc.Get<IFlowEnvironmentEvent>();
            currentFlowEnvironment = localFlowEnvironment;
            SereinEnv.SetEnv(localFlowEnvironment);
        }

        /// <summary>
        /// 提供上下文操作进行调用
        /// </summary>
        /// <param name="operation"></param>
        public FlowEnvironment(UIContextOperation operation)
        {
            ISereinIOC ioc = new SereinIOC();
            ioc.Register<ISereinIOC>(() => ioc) // IOC容器接口
               .Register<IFlowEnvironment>(() => this) // 流程环境接口
               .Register<IFlowEnvironmentEvent, FlowEnvironmentEvent>() // 流程环境事件接口
               .Register<IFlowLibraryService, FlowLibraryService>() 
               .Register<UIContextOperation>(() => operation) // 流程环境接口
                .Register<IFlowEdit, FlowEdit>() // 流程编辑接口
               .Register<IFlowControl, FlowControl>() // 流程控制接口
               .Register<LocalFlowEnvironment>() // 本地环境
               .Register<FlowModelService>() // 节点/画布模型服务
               .Register<FlowLibraryService>() // 流程库服务
               .Register<FlowCoreGenerateService>() // 代码生成
               .Register<FlowOperationService>() // 流程操作
               .Register<NodeMVVMService>() // 节点MVVM服务
               .Build();

            // 设置JSON解析器
            if (JsonHelper.Provider is null)
            {
                JsonHelper.UseJsonProvider(new NewtonsoftJsonProvider());
            }

            // 默认使用本地环境
            localFlowEnvironment = ioc.Get<LocalFlowEnvironment>();
            currentFlowEnvironmentEvent = ioc.Get<IFlowEnvironmentEvent>();
            currentFlowEnvironment = localFlowEnvironment;
            SereinEnv.SetEnv(localFlowEnvironment);
        }

        /// <summary>
        /// 管理当前环境
        /// </summary>

        private LocalFlowEnvironment localFlowEnvironment;

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
        public IFlowEdit FlowEdit => currentFlowEnvironment.FlowEdit;

        /// <inheritdoc/>
        public IFlowControl FlowControl => currentFlowEnvironment.FlowControl;
        
        /// <inheritdoc/>
        public IFlowLibraryService FlowLibraryService => currentFlowEnvironment.FlowLibraryService;

        /// <inheritdoc/>
        public ISereinIOC IOC => currentFlowEnvironment.IOC;

        /// <inheritdoc/>
        public IFlowEnvironmentEvent Event => currentFlowEnvironment.Event;


        /// <inheritdoc/>
        public string EnvName => currentFlowEnvironment.EnvName;

        /// <inheritdoc/>
        public string ProjectFileLocation => currentFlowEnvironment.EnvName;

        /// <inheritdoc/>
        public bool _IsGlobalInterrupt => currentFlowEnvironment._IsGlobalInterrupt;

        /// <inheritdoc/>
        public bool IsControlRemoteEnv => currentFlowEnvironment.IsControlRemoteEnv;

        /// <inheritdoc/>
        public InfoClass InfoClass { get => currentFlowEnvironment.InfoClass; set => currentFlowEnvironment.InfoClass = value; }

        /// <inheritdoc/>
        public RunState FlowState { get => currentFlowEnvironment.FlowState; set => currentFlowEnvironment.FlowState = value; }

       /* /// <inheritdoc/>
        public void ActivateFlipflopNode(string nodeGuid)
        {
            currentFlowEnvironment.FlowControl.ActivateFlipflopNode(nodeGuid);
        }*/

       /* /// <inheritdoc/>
        public async Task<(bool, RemoteMsgUtil)> ConnectRemoteEnv(string addres, int port, string token)
        {
            // 连接成功，切换远程环境
            (var isConnect, var remoteMsgUtil) = await currentFlowEnvironment.ConnectRemoteEnv(addres, port, token);
            if (isConnect)
            {
                
               *//* remoteFlowEnvironment ??= new RemoteFlowEnvironment(remoteMsgUtil, this.Event,  this.UIContextOperation);
                currentFlowEnvironment = remoteFlowEnvironment;*//*
            }
            return (isConnect, remoteMsgUtil);
        }*/

       /* public async Task<bool> ExitFlowAsync()
        {
            return await currentFlowEnvironment.FlowControl.ExitFlowAsync();
        }*/

       /* /// <inheritdoc/>
        public void ExitRemoteEnv()
        {
            currentFlowEnvironment.ExitRemoteEnv();
        }


        /// <inheritdoc/>
        public async Task<FlowEnvInfo> GetEnvInfoAsync()
        {
            return await currentFlowEnvironment.GetEnvInfoAsync();
        }*/

        /// <inheritdoc/>
        public SereinProjectData GetProjectInfoAsync()
        {
            return  currentFlowEnvironment.GetProjectInfoAsync();
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
        public void LoadProject(string filePath)
        {
            //if (flowEnvInfo is null) return;
            SetProjectLoadingFlag(false);
            currentFlowEnvironment.LoadProject(filePath);
            SetProjectLoadingFlag(true);
        }
        
        /// <inheritdoc/>
        public async Task LoadProjetAsync(string filePath)
        {
            //if (flowEnvInfo is null) return;
            SetProjectLoadingFlag(false);
            await currentFlowEnvironment.LoadProjetAsync(filePath);
            SetProjectLoadingFlag(true);
        }

        /// <inheritdoc/>
        public bool TryUnloadLibrary(string assemblyName)
        {
            return currentFlowEnvironment.TryUnloadLibrary(assemblyName);
        }

        /// <summary>
        /// 输出信息
        /// </summary>
        /// <param name="message">日志内容</param>
        /// <param name="type">日志类别</param>
        /// <param name="class">日志级别</param>
        public void WriteLine(InfoType type, string message, InfoClass @class = InfoClass.General)
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
        public Task StartRemoteServerAsync(int port = 7525)
        {
            
            throw new NotImplementedException();
        }


        /// <inheritdoc/>
        public void SetUIContextOperation(UIContextOperation uiContextOperation)
        {
            
            currentFlowEnvironment.SetUIContextOperation(uiContextOperation);
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
