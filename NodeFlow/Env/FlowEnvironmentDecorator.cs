﻿using Serein.Library;
using Serein.Library.Api;
using Serein.Library.FlowNode;
using Serein.Library.Utils;
using Serein.NodeFlow.Tool;

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

        public bool IsControlRemoteEnv => currentFlowEnvironment.IsControlRemoteEnv;

        /// <summary>
        /// 信息输出等级
        /// </summary>
        public InfoClass InfoClass { get => currentFlowEnvironment.InfoClass; set => currentFlowEnvironment.InfoClass = value; }
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

        /// <summary>
        /// 项目准备保存
        /// </summary>
        public event ProjectSavingHandler? OnProjectSaving
        {
            add { currentFlowEnvironment.OnProjectSaving += value; }
            remove { currentFlowEnvironment.OnProjectSaving -= value; }
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


        public bool UnloadLibrary(string assemblyName)
        {
            return currentFlowEnvironment.UnloadLibrary(assemblyName);
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
        /// 添加或更新全局数据
        /// </summary>
        /// <param name="keyName">数据名称</param>
        /// <param name="data">数据集</param>
        /// <returns></returns>
        public object AddOrUpdateGlobalData(string keyName, object data)
        {
            return currentFlowEnvironment.AddOrUpdateGlobalData(keyName, data);
        }

        /// <summary>
        /// 获取全局数据
        /// </summary>
        /// <param name="keyName">数据名称</param>
        /// <returns></returns>
        public object GetGlobalData(string keyName)
        {
            return currentFlowEnvironment.GetGlobalData(keyName);
        }


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
