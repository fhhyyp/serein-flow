
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Core;
using Serein.Library.FlowNode;
using Serein.Library.Utils;
using Serein.Library.Utils.SereinExpression;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Tool;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Loader;
using System.Security.AccessControl;
using System.Text;
using System.Xml.Linq;
using static Serein.Library.Utils.ChannelFlowInterrupt;

namespace Serein.NodeFlow.Env
{




    /// <summary>
    /// 运行环境
    /// </summary>
    public class FlowEnvironment : IFlowEnvironment, ISereinIOC
    {
        /// <summary>
        /// 节点的命名空间
        /// </summary>
        public const string SpaceName = $"{nameof(Serein)}.{nameof(NodeFlow)}.{nameof(Model)}";
        public const string ThemeKey = "theme";
        public const string DataKey = "data";
        public const string MsgIdKey = "msgid";

        /// <summary>
        /// 流程运行环境
        /// </summary>
        public FlowEnvironment(UIContextOperation uiContextOperation)
        {
            this.sereinIOC = new SereinIOC();
            this.ChannelFlowInterrupt = new ChannelFlowInterrupt();
            this.IsGlobalInterrupt = false;
            this.flowStarter = null;
            this.sereinIOC.OnIOCMembersChanged += e =>
            {
                if (OperatingSystem.IsWindows())
                {
                    UIContextOperation?.Invoke(() => this?.OnIOCMembersChanged?.Invoke(e)); // 监听IOC容器的注册 
                }
                
            };
            this.UIContextOperation = uiContextOperation; // 为加载的类库提供在UI线程上执行某些操作的封装工具类
            this.FlowLibraryManagement = new FlowLibraryManagement(this); // 实例化类库管理

            #region 注册基本节点类型
            NodeMVVMManagement.RegisterModel(NodeControlType.Action, typeof(SingleActionNode)); // 动作节点
            NodeMVVMManagement.RegisterModel(NodeControlType.Flipflop, typeof(SingleFlipflopNode)); // 触发器节点
            NodeMVVMManagement.RegisterModel(NodeControlType.ExpOp, typeof(SingleExpOpNode)); // 表达式节点
            NodeMVVMManagement.RegisterModel(NodeControlType.ExpCondition, typeof(SingleConditionNode)); // 条件表达式节点
            NodeMVVMManagement.RegisterModel(NodeControlType.ConditionRegion, typeof(CompositeConditionNode)); // 条件区域
            NodeMVVMManagement.RegisterModel(NodeControlType.GlobalData, typeof(SingleGlobalDataNode));  // 全局数据节点
            #endregion
        }

        #region 远程管理

        private MsgControllerOfServer clientMsgManage;


        /// <summary>
        /// <para>表示是否正在控制远程</para>
        /// <para>Local control remote env</para>
        /// </summary>
        public bool IsControlRemoteEnv { get; set; }


        /// <summary>
        /// 打开远程管理
        /// </summary>
        /// <param name="port"></param>
        public async Task StartRemoteServerAsync(int port = 7525)
        {
            if (clientMsgManage is null)
            {
                clientMsgManage = new MsgControllerOfServer(this);
                //clientMsgManage = new MsgControllerOfServer(this,"123456");
            }
            _ = clientMsgManage.StartRemoteServerAsync(port);
        }

        /// <summary>
        /// 结束远程管理
        /// </summary>
        public void StopRemoteServer()
        {
            try
            {
                clientMsgManage.StopRemoteServer();
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(InfoType.ERROR, "结束远程管理异常：" + ex);
            }
        }

        #endregion

        #region 环境运行事件
        /// <summary>
        /// 加载Dll
        /// </summary>
        public event LoadDllHandler? OnDllLoad;

        /// <summary>
        /// 移除DLL
        /// </summary>
        public event RemoteDllHandler? OnDllRemote;

        /// <summary>
        /// 项目加载完成
        /// </summary>
        public event ProjectLoadedHandler? OnProjectLoaded;

        /// <summary>
        /// 项目准备保存
        /// </summary>
        public event ProjectSavingHandler? OnProjectSaving;

        /// <summary>
        /// 节点连接属性改变事件
        /// </summary>
        public event NodeConnectChangeHandler? OnNodeConnectChange;

        /// <summary>
        /// 节点创建事件
        /// </summary>
        public event NodeCreateHandler? OnNodeCreate;

        /// <summary>
        /// 移除节点事件
        /// </summary>
        public event NodeRemoveHandler? OnNodeRemove;

        /// <summary>
        /// 起始节点变化事件
        /// </summary>
        public event StartNodeChangeHandler? OnStartNodeChange;

        /// <summary>
        /// 流程运行完成事件
        /// </summary>
        public event FlowRunCompleteHandler? OnFlowRunComplete;

        /// <summary>
        /// 被监视的对象改变事件
        /// </summary>
        public event MonitorObjectChangeHandler? OnMonitorObjectChange;

        /// <summary>
        /// 节点中断状态改变事件
        /// </summary>
        public event NodeInterruptStateChangeHandler? OnNodeInterruptStateChange;

        /// <summary>
        /// 节点触发了中断
        /// </summary>
        public event ExpInterruptTriggerHandler? OnInterruptTrigger;

        /// <summary>
        /// 容器改变
        /// </summary>
        public event IOCMembersChangedHandler? OnIOCMembersChanged;

        /// <summary>
        /// 节点需要定位
        /// </summary>
        public event NodeLocatedHandler? OnNodeLocated;

        /// <summary>
        /// 节点移动了（远程插件）
        /// </summary>
        public event NodeMovedHandler? OnNodeMoved;

        /// <summary>
        /// 运行环境输出
        /// </summary>
        public event EnvOutHandler? OnEnvOut;

        #endregion

        #region 属性

        /// <summary>
        /// 当前环境
        /// </summary>
        public IFlowEnvironment CurrentEnv { get => this; }

        /// <summary>
        /// UI线程操作类
        /// </summary>
        public UIContextOperation UIContextOperation { get; set; }

        /// <summary>
        /// 信息输出等级
        /// </summary>
        public InfoClass InfoClass { get ; set ; } = InfoClass.General;

        /// <summary>
        /// 如果没有全局触发器，且没有循环分支，流程执行完成后自动为 Completion 。
        /// </summary>
        public RunState FlowState { get; set; } = RunState.NoStart;
        /// <summary>
        /// 如果全局触发器还在运行，则为 Running 。
        /// </summary>
        public RunState FlipFlopState { get; set; } = RunState.NoStart;

        /// <summary>
        /// 环境名称
        /// </summary>
        public string EnvName { get; set; } = SpaceName;

        /// <summary>
        /// 是否全局中断
        /// </summary>
        public bool IsGlobalInterrupt { get; set; }

        /// <summary>
        /// 流程中断器
        /// </summary>
        public ChannelFlowInterrupt ChannelFlowInterrupt { get; set; }

        /// <summary>
        /// <para>单例模式IOC容器，内部维护了一个实例字典，默认使用类型的FullName作为Key，如果以“接口-实现类”的方式注册，那么将使用接口类型的FullName作为Key。</para>
        /// <para>当某个类型注册绑定成功后，将不会因为其它地方尝试注册相同类型的行为导致类型被重新创建。</para>
        /// </summary>
        public ISereinIOC IOC { get => this; }


        #endregion

        #region 私有变量

        /// <summary>
        /// 通过程序集名称管理动态加载的程序集，用于节点创建提供方法描述，流程运行时提供Emit委托
        /// </summary>
        private readonly FlowLibraryManagement FlowLibraryManagement;

#if false

        /// <summary>
        /// Library 与 MethodDetailss的依赖关系
        /// </summary>
        public ConcurrentDictionary<NodeLibraryInfo, List<MethodDetails>> MethodDetailsOfLibraryInfos { get; } = [];


        /// <summary>
        /// <para>存储已加载的程序集</para>
        /// <para>Key：程序集的FullName </para>
        /// <para>Value：构造的方法信息</para>
        /// </summary>
        public ConcurrentDictionary<string, NodeLibraryInfo> LibraryInfos { get; } = [];

        /// <summary>
        /// <para>存储已加载的方法信息。描述所有DLL中NodeAction特性的方法的原始副本</para>
        /// <para>Key：反射时获取的MethodInfo.MehtodName</para>
        /// <para>Value：构造的方法信息</para>
        /// </summary>
        public ConcurrentDictionary<string, MethodDetails> MethodDetailss { get; } = [];

        /// <summary>
        /// 从dll中加载的类的注册类型
        /// </summary>
        private Dictionary<RegisterSequence, List<Type>> AutoRegisterTypes { get; } = [];

        /// <summary>
        /// 存放所有通过Emit加载的委托
        /// md.Methodname - delegate
        /// </summary>
        private ConcurrentDictionary<string, DelegateDetails> MethodDelegates { get; } = []; 
#endif

        /// <summary>
        /// IOC对象容器管理
        /// </summary>
        private readonly SereinIOC sereinIOC;

        /// <summary>
        /// 环境加载的节点集合
        /// Node Guid - Node Model
        /// </summary>
        private Dictionary<string, NodeModelBase> NodeModels { get; } = [];

        /// <summary>
        /// 存放触发器节点（运行时全部调用）
        /// </summary>
        private List<SingleFlipflopNode> FlipflopNodes { get; } = [];



        /// <summary>
        /// 起始节点私有属性
        /// </summary>
        private NodeModelBase? _startNode = null;

        /// <summary>
        /// 起始节点
        /// </summary>
        private NodeModelBase? StartNode
        {
            get
            {
                return _startNode;
            }
            set
            {
                if (value is null)
                {
                    return;
                }
                if (_startNode is not null)
                {
                    _startNode.IsStart = false;
                }
                value.IsStart = true;
                _startNode = value;
            }
        }

        /// <summary>
        /// 流程启动器（每次运行时都会重新new一个）
        /// </summary>
        private FlowStarter? flowStarter;

        #endregion

        #region 环境对外接口

        ///// <summary>
        ///// 重定向Console输出
        ///// </summary>
        //public void SetConsoleOut()
        //{
        //    var logTextWriter = new LogTextWriter(msg => Output(msg));
        //    Console.SetOut(logTextWriter);
        //}

        /// <summary>
        /// 输出信息
        /// </summary>
        /// <param name="message">日志内容</param>
        /// <param name="type">日志类别</param>
        /// <param name="class">日志级别</param>
        public void WriteLine(InfoType type, string message, InfoClass @class = InfoClass.Trivial)
        {
            if (@class >= this.InfoClass)
            {
                OnEnvOut?.Invoke(type, message);
            }
            //Console.WriteLine($"{DateTime.UtcNow} [{type}] : {message}{Environment.NewLine}");
            
        }

        ///// <summary>
        ///// 使用JSON处理库输出对象信息
        ///// </summary>
        ///// <param name="obj"></param>
        //public void WriteLineObjToJson(object obj)
        //{
        //    var msg = JsonConvert.SerializeObject(obj);
        //    if (OperatingSystem.IsWindows())
        //    {
        //        UIContextOperation?.Invoke(() => OnEnvOut?.Invoke(msg + Environment.NewLine)); 
        //    }

        //}

        /// <summary>
        /// 异步运行
        /// </summary>
        /// <returns></returns>
        public async Task StartAsync()
        {
            ChannelFlowInterrupt?.CancelAllTasks();
            flowStarter = new FlowStarter();
            var nodes = NodeModels.Values.ToList();

           

            List<MethodDetails> initMethods = this.FlowLibraryManagement.GetMdsOnFlowStart(NodeType.Init);
            List<MethodDetails> loadMethods = this.FlowLibraryManagement.GetMdsOnFlowStart(NodeType.Loading);
            List<MethodDetails> exitMethods = this.FlowLibraryManagement.GetMdsOnFlowStart(NodeType.Exit);
            Dictionary<RegisterSequence, List<Type>> autoRegisterTypes = this.FlowLibraryManagement.GetaAutoRegisterType();


            IOC.Reset(); // 开始运行时清空ioc中注册的实例

            IOC.CustomRegisterInstance(typeof(IFlowEnvironment).FullName, this);
            if (this.UIContextOperation is not null)
                IOC.CustomRegisterInstance(typeof(UIContextOperation).FullName, this.UIContextOperation, false);

            await flowStarter.RunAsync(this, nodes, autoRegisterTypes, initMethods, loadMethods, exitMethods);

            if (FlipFlopState == RunState.Completion)
            {
                ExitFlow(); // 未运行触发器时，才会调用结束方法
            }
            
        }

        /// <summary>
        /// 从选定节点开始运行
        /// </summary>
        /// <param name="startNodeGuid"></param>
        /// <returns></returns>
        public async Task StartAsyncInSelectNode(string startNodeGuid)
        {

            if (flowStarter is null)
            {
                SereinEnv.WriteLine(InfoType.ERROR, "没有启动流程，无法运行单个节点");
                return;
            }
            if (true || FlowState == RunState.Running || FlipFlopState == RunState.Running)
            {
                NodeModelBase? nodeModel = GuidToModel(startNodeGuid);
                if (nodeModel is null || nodeModel is SingleFlipflopNode)
                {
                    return;
                }
                //var getExp = "@get .DebugSetting.IsEnable";
                //var getExpResult1 = SerinExpressionEvaluator.Evaluate(getExp, nodeModel,out _);
                //var setExp = "@set .DebugSetting.IsEnable = false";
                //SerinExpressionEvaluator.Evaluate(setExp, nodeModel,out _);
                //var getExpResult2 = SerinExpressionEvaluator.Evaluate(getExp, nodeModel, out _);

                await flowStarter.StartFlowInSelectNodeAsync(this, nodeModel);
            }
            else
            {
                return;
            }
        }

        /// <summary>
        /// 单独运行一个节点
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <returns></returns>
        public async Task<object> InvokeNodeAsync(IDynamicContext context, string nodeGuid)
        {
            object result = true;
            if (this.NodeModels.TryGetValue(nodeGuid, out var model))
            {
                result =  await model.ExecutingAsync(context);
            }
            return result;
        }

        /// <summary>
        /// 结束流程
        /// </summary>
        public void ExitFlow()
        {
            ChannelFlowInterrupt?.CancelAllTasks();
            flowStarter?.Exit();
            UIContextOperation?.Invoke(() => OnFlowRunComplete?.Invoke(new FlowEventArgs()));
            flowStarter = null;
            GC.Collect();

        }

        /// <summary>
        /// 激活全局触发器
        /// </summary>
        /// <param name="nodeGuid"></param>
        // [AutoSocketHandle]
        public void ActivateFlipflopNode(string nodeGuid)
        {
            var nodeModel = GuidToModel(nodeGuid);
            if (nodeModel is null) return;
            if (flowStarter is not null && nodeModel is SingleFlipflopNode flipflopNode) // 子节点为触发器
            {
                if (FlowState != RunState.Completion
                    && flipflopNode.NotExitPreviousNode()) // 正在运行，且该触发器没有上游节点
                {
                    _ = flowStarter.RunGlobalFlipflopAsync(this, flipflopNode);// 被父节点移除连接关系的子节点若为触发器，且无上级节点，则当前流程正在运行，则加载到运行环境中

                }
            }
        }

        /// <summary>
        /// 关闭全局触发器
        /// </summary>
        /// <param name="nodeGuid"></param>
        // [AutoSocketHandle]
        public void TerminateFlipflopNode(string nodeGuid)
        {
            var nodeModel = GuidToModel(nodeGuid);
            if (nodeModel is null) return;
            if (flowStarter is not null && nodeModel is SingleFlipflopNode flipflopNode) // 子节点为触发器
            {
                flowStarter.TerminateGlobalFlipflopRuning(flipflopNode);
            }
        }

        /// <summary>
        /// 获取当前环境信息（远程连接）
        /// </summary>
        /// <returns></returns>
        public async Task<FlowEnvInfo> GetEnvInfoAsync()
        {
            // 获取所有的程序集对应的方法信息（程序集相关的数据）
            var libraryMdss = this.FlowLibraryManagement.GetAllLibraryMds().ToArray();
            // 获取当前项目的信息（节点相关的数据）
            var project = await GetProjectInfoAsync(); // 远程连接获取远程环境项目信息
            SereinEnv.WriteLine(InfoType.INFO, "已将当前环境信息发送到远程客户端");
            return new FlowEnvInfo
            {
                Project = project, // 项目信息
                LibraryMds = libraryMdss, // 环境方法
            };
        }

        /// <summary>
        /// 清除所有
        /// </summary>
        public void ClearAll()
        {
            //LoadedAssemblyPaths.Clear();
            //NodeLibrarys.Clear();
            //MethodDetailss.Clear();

        }

        /// <summary>
        /// 保存项目
        /// </summary>
        public void SaveProject()
        {
            OnProjectSaving?.Invoke(new ProjectSavingEventArgs());
        }

        /// <summary>
        /// 加载项目文件
        /// </summary>
        /// <param name="flowEnvInfo">环境信息</param>
        /// <param name="filePath"></param>
        public void LoadProject(FlowEnvInfo flowEnvInfo, string filePath)
        {
            var projectData = flowEnvInfo.Project;
            // 加载项目配置文件
            var dllPaths = projectData.Librarys.Select(it => it.FilePath).ToList();
            List<MethodDetails> methodDetailss = [];

            // 遍历依赖项中的特性注解，生成方法详情
            foreach (var dllPath in dllPaths)
            {
                string cleanedRelativePath = dllPath.TrimStart('.', '\\');
                var tmpPath = Path.Combine(filePath, cleanedRelativePath);
                var dllFilePath = Path.GetFullPath(tmpPath);
                LoadLibrary(dllFilePath);  // 加载项目文件时加载对应的程序集
            }

            List<(NodeModelBase, string[])> regionChildNodes = new List<(NodeModelBase, string[])>();
            List<(NodeModelBase, PositionOfUI)> ordinaryNodes = new List<(NodeModelBase, PositionOfUI)>();
            // 加载节点
            foreach (NodeInfo? nodeInfo in projectData.Nodes)
            {
                NodeControlType controlType = FlowFunc.GetNodeControlType(nodeInfo);
                if (controlType == NodeControlType.None)
                {
                    continue;
                }
                MethodDetails? methodDetails = null;

                if (controlType.IsBaseNode())
                {
                    // 加载基础节点
                    methodDetails = new MethodDetails();
                }
                else
                {
                    // 加载方法节点
                    if (string.IsNullOrEmpty(nodeInfo.AssemblyName) && string.IsNullOrEmpty(nodeInfo.MethodName))
                    {
                        continue;
                    }
                    FlowLibraryManagement.TryGetMethodDetails(nodeInfo.AssemblyName, nodeInfo.MethodName, out methodDetails); // 加载项目时尝试获取方法信息
                }
               
                var nodeModel = FlowFunc.CreateNode(this, controlType, methodDetails); // 加载项目时创建节点
                nodeModel.LoadInfo(nodeInfo); // 创建节点model
                if (nodeModel is null)
                {
                    nodeInfo.Guid = string.Empty;
                    continue;
                }

                TryAddNode(nodeModel); // 加载项目时将节点加载到环境中
                if (nodeInfo.ChildNodeGuids?.Length > 0)
                {
                    regionChildNodes.Add((nodeModel, nodeInfo.ChildNodeGuids));

                    UIContextOperation?.Invoke(() => OnNodeCreate?.Invoke(new NodeCreateEventArgs(nodeModel, nodeInfo.Position)));
                }
                else
                {
                    ordinaryNodes.Add((nodeModel, nodeInfo.Position));
                }
                
            }
            // 加载区域子项
            foreach ((NodeModelBase region, string[] childNodeGuids) item in regionChildNodes)
            {
                foreach (var childNodeGuid in item.childNodeGuids)
                {
                    NodeModels.TryGetValue(childNodeGuid, out NodeModelBase? childNode);
                    if (childNode is null)
                    {
                        // 节点尚未加载
                        continue;
                    }
                    UIContextOperation?.Invoke(() => OnNodeCreate?.Invoke(new NodeCreateEventArgs(childNode, true, item.region.Guid)));
                    // 存在节点
                    
                }
            }
            // 加载节点
            foreach ((NodeModelBase nodeModel, PositionOfUI position) item in ordinaryNodes)
            {
                bool IsContinue = false;
                foreach ((NodeModelBase region, string[] childNodeGuids) item2 in regionChildNodes)
                {
                    foreach (var childNodeGuid in item2.childNodeGuids)
                    {
                        if (item.nodeModel.Guid.Equals(childNodeGuid))
                        {
                            IsContinue = true;
                        }
                    }
                }
                if (IsContinue) continue;
                UIContextOperation?.Invoke(() => OnNodeCreate?.Invoke(new NodeCreateEventArgs(item.nodeModel, item.position)));
                
            }



            // 确定节点之间的连接关系
            Task.Run(async () =>
            {
                await Task.Delay(777);
                #region 方法调用关系
                foreach (var nodeInfo in projectData.Nodes)
                {
                    if (!NodeModels.TryGetValue(nodeInfo.Guid, out NodeModelBase? fromNode))
                    {
                        // 不存在对应的起始节点
                        continue;
                    }
                    List<(ConnectionInvokeType connectionType, string[] guids)> allToNodes = [(ConnectionInvokeType.IsSucceed,nodeInfo.TrueNodes),
                                                                                    (ConnectionInvokeType.IsFail,   nodeInfo.FalseNodes),
                                                                                    (ConnectionInvokeType.IsError,  nodeInfo.ErrorNodes),
                                                                                    (ConnectionInvokeType.Upstream, nodeInfo.UpstreamNodes)];

                    List<(ConnectionInvokeType, NodeModelBase[])> fromNodes = allToNodes.Where(info => info.guids.Length > 0)
                                                                         .Select(info => (info.connectionType,
                                                                                          info.guids.Where(guid => NodeModels.ContainsKey(guid)).Select(guid => NodeModels[guid])
                                                                                            .ToArray()))
                                                                         .ToList();
                    // 遍历每种类型的节点分支（四种）
                    foreach ((ConnectionInvokeType connectionType, NodeModelBase[] toNodes) item in fromNodes)
                    {
                        // 遍历当前类型分支的节点（确认连接关系）
                        foreach (var toNode in item.toNodes)
                        {
                            _ = ConnectInvokeOfNode(fromNode, toNode, item.connectionType); // 加载时确定节点间的连接关系
                        }
                    } 
                }
                #endregion
                #region 参数调用关系
                foreach (var toNode in NodeModels.Values)
                {
                    for (var i = 0; i < toNode.MethodDetails.ParameterDetailss.Length; i++)
                    {
                        var pd = toNode.MethodDetails.ParameterDetailss[i];
                        if (!string.IsNullOrEmpty(pd.ArgDataSourceNodeGuid) 
                            && NodeModels.TryGetValue(pd.ArgDataSourceNodeGuid, out var fromNode))
                        {

                            await ConnectArgSourceOfNodeAsync(fromNode, toNode, pd.ArgDataSourceType, pd.Index);
                        }
                    }
                }
                #endregion
            });

            SetStartNode(projectData.StartNode);
            UIContextOperation?.Invoke(() => OnProjectLoaded?.Invoke(new ProjectLoadedEventArgs()));
            

        }

        /// <summary>
        /// 加载远程环境
        /// </summary>
        /// <param name="addres">远程环境地址</param>
        /// <param name="port">远程环境端口</param>
        /// <param name="token">密码</param>
        public async Task<(bool, RemoteMsgUtil)> ConnectRemoteEnv(string addres, int port, string token)
        {
            if (IsControlRemoteEnv)
            {
                await Console.Out.WriteLineAsync($"当前已经连接远程环境");
                return (false, null);
            }
            // 没有连接远程环境，可以重新连接

            var controlConfiguration = new RemoteMsgUtil.ControlConfiguration
            {
                Addres = addres,
                Port = port,
                Token = token,
                ThemeJsonKey = FlowEnvironment.ThemeKey,
                MsgIdJsonKey = FlowEnvironment.MsgIdKey,
                DataJsonKey = FlowEnvironment.DataKey,
            };
            var remoteMsgUtil = new RemoteMsgUtil(controlConfiguration);
            var result = await remoteMsgUtil.ConnectAsync();
            if (!result)
            {
                await Console.Out.WriteLineAsync("连接失败，请检查地址与端口是否正确");
                return (false, null);
            }
            await Console.Out.WriteLineAsync("连接成功，开始验证Token");
            IsControlRemoteEnv = true;
            return (true, remoteMsgUtil);
        }

        /// <summary>
        /// 退出远程环境
        /// </summary>
        public void ExitRemoteEnv()
        {
            IsControlRemoteEnv = false;
        }

        /// <summary>
        /// 序列化当前项目的依赖信息、节点信息
        /// </summary>
        /// <returns></returns>
        public Task<SereinProjectData> GetProjectInfoAsync()
        {
            var projectData = new SereinProjectData()
            {
                Librarys = this.FlowLibraryManagement.GetAllLibraryInfo().ToArray(),
                Nodes = NodeModels.Values.Select(node => node.ToInfo()).Where(info => info is not null).ToArray(),
                StartNode = NodeModels.Values.FirstOrDefault(it => it.IsStart)?.Guid,
            };

            return Task.FromResult(projectData);
        }


        /// <summary>
        /// 从文件路径中加载DLL
        /// </summary>
        /// <param name="dllPath"></param>
        /// <returns></returns> 
        public void LoadLibrary(string dllPath)
        {
            
            try
            {
                #region 检查是否已经加载本地依赖
                var thisAssembly = typeof(IFlowEnvironment).Assembly;
                var thisAssemblyName = thisAssembly.GetName().Name;
                if (!string.IsNullOrEmpty(thisAssemblyName) && FlowLibraryManagement.GetLibraryMdsOfAssmbly(thisAssemblyName).Count == 0)
                {
                   var tmp = FlowLibraryManagement.LoadLibraryOfPath(thisAssembly.Location);
                    UIContextOperation?.Invoke(() => OnDllLoad?.Invoke(new LoadDllEventArgs(tmp.Item1, tmp.Item2))); // 通知UI创建dll面板显示
                }
                
                #endregion

                (var libraryInfo, var mdInfos) = FlowLibraryManagement.LoadLibraryOfPath(dllPath);
                if (mdInfos.Count > 0)
                {
                    UIContextOperation?.Invoke(() => OnDllLoad?.Invoke(new LoadDllEventArgs(libraryInfo, mdInfos))); // 通知UI创建dll面板显示
                }
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(InfoType.ERROR, $"无法加载DLL文件：{ex}");
            }

        }

        /// <summary>
        /// 移除DLL
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        public bool UnloadLibrary(string assemblyName)
        {
            // 获取与此程序集相关的节点
            var groupedNodes = NodeModels.Values.Where(node => node.MethodDetails.AssemblyName.Equals(assemblyName)).ToArray();
            if (groupedNodes.Length == 0)
            {
                var isPass = FlowLibraryManagement.UnloadLibrary(assemblyName);
                return isPass;
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine();
                for (int i = 0; i < groupedNodes.Length; i++)
                {
                    NodeModelBase? node = groupedNodes[i];
                    sb.AppendLine($"{i} => {node.Guid}");
                }
                SereinEnv.WriteLine(InfoType.ERROR, $"无法卸载[{assemblyName}]程序集，因为这些节点依赖于此程序集：{sb.ToString()}");

                return false;
            }

            //var mds = FlowLibraryManagement.GetLibraryMdsOfAssmbly(assemblyName);
            //if(mds.Count > 0)
            //{
                
                
            //}
            //else
            //{
            //    return true;
            //}
           
            //var library = LibraryInfos.Values.FirstOrDefault(nl => assemblyName.Equals(nl.AssemblyName));
            //if (library is null)
            //{
            //    return false;
            //}
            //var groupedNodes = NodeModels.Values
            //    .Where(node => node.MethodDetails is not null)
            //    .ToArray()
            //    .GroupBy(node => node.MethodDetails?.MethodName)
            //    .ToDictionary(
            //    key => key.Key,
            //    group => group.Count());


            //if (NodeModels.Count == 0)
            //{
            //    return true; // 当前无节点，可以直接删除
            //}

            //if (MethodDetailsOfLibraryInfos.TryGetValue(library, out var mds)) // 存在方法
            //{
            //    foreach (var md in mds)
            //    {
            //        if (groupedNodes.TryGetValue(md.MethodName, out int count))
            //        {
            //            if (count > 0)
            //            {
            //                return false; // 创建过相关的节点，无法移除
            //            }
            //        }
            //    }
            //    // 开始移除相关信息
            //    foreach (var md in mds)
            //    {
            //        MethodDetailss.TryRemove(md.MethodName, out _);
            //    }
            //    MethodDetailsOfLibraryInfos.TryRemove(library, out _);
            //    return true;
            //}
            //else
            //{
            //    return true;
            //}
        }


        /// <summary>
        /// 流程正在运行时创建节点
        /// </summary>
        /// <param name="nodeControlType"></param>
        /// <param name="position"></param>
        /// <param name="methodDetailsInfo">如果是表达式节点条件节点，该项为null</param>
        public Task<NodeInfo> CreateNodeAsync(NodeControlType nodeControlType, PositionOfUI position, MethodDetailsInfo? methodDetailsInfo = null)
        {
            
            NodeModelBase? nodeModel;
            if (methodDetailsInfo is null)
            {
                nodeModel = FlowFunc.CreateNode(this, nodeControlType); // 加载基础节点
            }
            else
            {
                if (FlowLibraryManagement.TryGetMethodDetails(methodDetailsInfo.AssemblyName,  // 创建节点
                                                              methodDetailsInfo.MethodName, 
                                                              out var methodDetails))
                {
                    nodeModel = FlowFunc.CreateNode(this, nodeControlType, methodDetails); // 一般的加载节点方法
                }
                else
                {
                    return Task.FromResult<NodeInfo>(null);
                }
            }
           
            TryAddNode(nodeModel);
            nodeModel.Position = position;

            // 通知UI更改
            UIContextOperation?.Invoke(() => OnNodeCreate?.Invoke(new NodeCreateEventArgs(nodeModel, position)));

            // 因为需要UI先布置了元素，才能通知UI变更特效
            // 如果不存在流程起始控件，默认设置为流程起始控件
            if (StartNode is null)
            {
                SetStartNode(nodeModel);
            }
            var nodeInfo = nodeModel.ToInfo();
            return Task.FromResult(nodeInfo);
;

        }

        /// <summary>
        /// 移除节点
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<bool> RemoveNodeAsync(string nodeGuid)
        {
            var remoteNode = GuidToModel(nodeGuid);
            if (remoteNode is null)
               return false;

            if (remoteNode is SingleFlipflopNode flipflopNode)
            {
                flowStarter?.TerminateGlobalFlipflopRuning(flipflopNode); // 假设被移除的是全局触发器，尝试从启动器移除
            }

            remoteNode.Remove(); // 调用节点的移除方法

            // 遍历所有前置节点，从那些前置节点中的后继节点集合移除该节点
            foreach (var pnc in remoteNode.PreviousNodes)
            {
                var pCType = pnc.Key; // 连接类型
                for (int i = 0; i < pnc.Value.Count; i++)
                {
                    NodeModelBase? pNode = pnc.Value[i];
                    pNode.SuccessorNodes[pCType].Remove(remoteNode);

                    UIContextOperation?.Invoke(() => OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(
                                                                    pNode.Guid,
                                                                    remoteNode.Guid,
                                                                    JunctionOfConnectionType.Invoke,
                                                                    pCType, // 对应的连接关系
                                                                    NodeConnectChangeEventArgs.ConnectChangeType.Remote))); // 通知UI

                }
            }

            // 遍历所有后继节点，从那些后继节点中的前置节点集合移除该节点
            foreach (var snc in remoteNode.SuccessorNodes)
            {
                var connectionType = snc.Key; // 连接类型
                for (int i = 0; i < snc.Value.Count; i++)
                {
                    NodeModelBase? toNode = snc.Value[i];

                    await RemoteConnectAsync(remoteNode, toNode, connectionType);

                }
            }

            

            // 从集合中移除节点
            NodeModels.Remove(nodeGuid);
            UIContextOperation?.Invoke(() => OnNodeRemove?.Invoke(new NodeRemoveEventArgs(nodeGuid)));
            return true;
        }

        /// <summary>
        /// 连接节点，创建方法调用关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点</param>
        /// <param name="toNodeGuid">目标节点</param>
        /// <param name="fromNodeJunctionType">起始节点控制点</param>
        /// <param name="toNodeJunctionType">目标节点控制点</param>
        /// <param name="invokeType">连接关系</param>
        public async Task<bool> ConnectInvokeNodeAsync(string fromNodeGuid,
                                                 string toNodeGuid,
                                                 JunctionType fromNodeJunctionType,
                                                 JunctionType toNodeJunctionType,
                                                 ConnectionInvokeType invokeType)
        {

            // 获取起始节点与目标节点
            var fromNode = GuidToModel(fromNodeGuid);
            var toNode = GuidToModel(toNodeGuid);
            if (fromNode is null || toNode is null) return false;
            (var type, var state) = CheckConnect(fromNode, toNode, fromNodeJunctionType, toNodeJunctionType);
            if (!state)
            {
                SereinEnv.WriteLine(InfoType.WARN, "出现非预期的连接行为");
                return false; // 出现不符预期的连接行为，忽略此次连接行为
            }

            
            if(type == JunctionOfConnectionType.Invoke)
            {
                if (fromNodeJunctionType == JunctionType.Execute) 
                {
                    // 如果 起始控制点 是“方法调用”，需要反转 from to 节点
                    (fromNode, toNode) = (toNode, fromNode);
                }
                // 从起始节点“下一个方法”控制点，连接到目标节点“方法调用”控制点
                state = ConnectInvokeOfNode(fromNode, toNode, invokeType); // 本地环境进行连接
            }
            return state;

        }

        /// <summary>
        /// 移除连接节点之间方法调用的关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点Guid</param>
        /// <param name="toNodeGuid">目标节点Guid</param>
        /// <param name="connectionType">连接关系</param>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<bool> RemoveConnectInvokeAsync(string fromNodeGuid, string toNodeGuid, ConnectionInvokeType connectionType)
        {
            // 获取起始节点与目标节点
            var fromNode = GuidToModel(fromNodeGuid);
            var toNode = GuidToModel(toNodeGuid);
            if (fromNode is null || toNode is null) return false;

            var result = await RemoteConnectAsync(fromNode, toNode, connectionType);
            return result;
        }

        /// <summary>
        /// 创建节点之间的参数来源关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点</param>
        /// <param name="toNodeGuid">目标节点</param>
        /// <param name="fromNodeJunctionType">起始节点控制点（result控制点）</param>
        /// <param name="toNodeJunctionType">目标节点控制点（argData控制点）</param>
        /// <param name="argIndex">目标节点的第几个参数</param>
        /// <param name="connectionArgSourceType">调用目标节点对应方法时，对应参数来源类型</param>
        /// <returns></returns>
        public async Task<bool> ConnectArgSourceNodeAsync(string fromNodeGuid,
                                                 string toNodeGuid,
                                                 JunctionType fromNodeJunctionType,
                                                 JunctionType toNodeJunctionType,
                                                 ConnectionArgSourceType connectionArgSourceType,
                                                 int argIndex)
        {

            // 获取起始节点与目标节点
            var fromNode = GuidToModel(fromNodeGuid);
            var toNode = GuidToModel(toNodeGuid);
            if (fromNode is null || toNode is null) return false;
            (var type, var state) = CheckConnect(fromNode, toNode, fromNodeJunctionType, toNodeJunctionType);
            if (!state)
            {
                SereinEnv.WriteLine(InfoType.WARN, "出现非预期的连接行为");
                return false; // 出现不符预期的连接行为，忽略此次连接行为
            }

            if (type == JunctionOfConnectionType.Arg)
            {
                // 从起始节点“返回值”控制点，连接到目标节点“方法入参”控制点
                if (fromNodeJunctionType == JunctionType.ArgData)
                {
                    // 如果 起始控制点 是“方法入参”，需要反转 from to 节点
                    (fromNode, toNode) = (toNode, fromNode);
                }

                // 确定方法入参关系
                state = await ConnectArgSourceOfNodeAsync(fromNode, toNode, connectionArgSourceType, argIndex);  // 本地环境进行连接
            }
            return state;

        }


        /// <summary>
        /// 移除连接节点之间参数传递的关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点Guid</param>
        /// <param name="toNodeGuid">目标节点Guid</param>
        /// <param name="argIndex">连接到第几个参数</param>
        public async Task<bool> RemoveConnectArgSourceAsync(string fromNodeGuid, string toNodeGuid, int argIndex)
        {
            // 获取起始节点与目标节点
            var fromNode = GuidToModel(fromNodeGuid);
            var toNode = GuidToModel(toNodeGuid);
            if (fromNode is null || toNode is null) return false;
            var result = await RemoteConnectAsync(fromNode, toNode, argIndex);
            return result;
        }


        /// <summary>
        /// 获取方法描述
        /// </summary>

        public bool TryGetMethodDetailsInfo(string assemblyName, string methodName, out MethodDetailsInfo? mdInfo)
        {
            var isPass = FlowLibraryManagement.TryGetMethodDetails(assemblyName, methodName, out var md);
            if (!isPass || md is null)
            {
                mdInfo = null;
                return false;
            }
            else
            {
                mdInfo = md?.ToInfo();
                return true;
            }
        }

        /// <summary>
        /// <para>通过方法名称获取对应的Emit委托</para>
        /// <para>方法无入参时需要传入空数组，void方法自动返回null</para>
        /// <para>普通方法：Func&lt;object,object[],object&gt;</para>
        /// <para>异步方法：Func&lt;object,object[],Task&gt;</para>
        /// <para>异步有返回值方法：Func&lt;object,object[],Task&lt;object&gt;&gt;</para>
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="delegateDetails"></param>
        /// <returns></returns>
        public bool TryGetDelegateDetails(string assemblyName, string methodName, out DelegateDetails? delegateDetails)
        {
            return FlowLibraryManagement.TryGetDelegateDetails(assemblyName, methodName, out delegateDetails);
        }


        /// <summary>
        /// 移动了某个节点(远程插件使用）
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public void MoveNode(string nodeGuid, double x, double y)
        {
            NodeModelBase? nodeModel = GuidToModel(nodeGuid);
            if (nodeModel is null) return;
            nodeModel.Position.X = x;

            nodeModel.Position.Y = y;
            UIContextOperation?.Invoke(() => OnNodeMoved?.Invoke(new NodeMovedEventArgs(nodeGuid, x, y)));
           
        }

        /// <summary>
        /// 设置起点控件
        /// </summary>
        /// <param name="newNodeGuid"></param>
        public void SetStartNode(string newNodeGuid)
        {
            var newStartNodeModel = GuidToModel(newNodeGuid);
            if (newStartNodeModel is null) return;
            SetStartNode(newStartNodeModel);
        }

        /// <summary>
        /// 中断指定节点，并指定中断等级。
        /// </summary>
        /// <param name="nodeGuid">被中断的目标节点Guid</param>
        /// <param name="interruptClass">中断级别</param>
        /// <returns>操作是否成功</returns>
        public Task<bool> SetNodeInterruptAsync(string nodeGuid, bool isInterrupt)
        {
           

            var nodeModel = GuidToModel(nodeGuid);
            if (nodeModel is null) 
                return Task.FromResult(false);
            if (!isInterrupt)
            {
                nodeModel.CancelInterrupt();
            }
            else if (isInterrupt)
            {
                nodeModel.DebugSetting.CancelInterruptCallback?.Invoke();
                nodeModel.DebugSetting.GetInterruptTask = async () =>
                {
                    TriggerInterrupt(nodeGuid, "", InterruptTriggerEventArgs.InterruptTriggerType.Monitor);
                    var result = await ChannelFlowInterrupt.GetOrCreateChannelAsync(nodeGuid);
                    return result;
                };
                nodeModel.DebugSetting.CancelInterruptCallback = () =>
                {
                    //nodeModel.DebugSetting.IsInterrupt = false;
                    ChannelFlowInterrupt.TriggerSignal(nodeGuid);
                };

            }
            
            //nodeModel.DebugSetting.IsInterrupt = true; 
            if (OperatingSystem.IsWindows())
            {

                UIContextOperation?.Invoke(() => OnNodeInterruptStateChange?.Invoke(new NodeInterruptStateChangeEventArgs(nodeGuid, isInterrupt))); 
            }
            
            return Task.FromResult(true);
        }


        /// <summary>
        /// 添加表达式中断
        /// </summary>
        /// <param name="key">如果是节点，传入Guid；如果是对象，传入类型FullName</param>
        /// <param name="expression">合法的条件表达式</param>
        /// <returns></returns>
        public Task<bool> AddInterruptExpressionAsync(string key, string expression)
        {
            if (string.IsNullOrEmpty(expression)) return Task.FromResult(false);
            if (dictMonitorObjExpInterrupt.TryGetValue(key, out var condition))
            {
                condition.Clear(); // 暂时
                condition.Add(expression);// 暂时
            }
            else
            {
                var exps = new List<string>();
                exps.Add(expression);
                dictMonitorObjExpInterrupt.TryAdd(key, exps);
            }
            return Task.FromResult(true);
        }

        /// <summary>
        /// 要监视的对象，以及与其关联的表达式
        /// </summary>
        private ConcurrentDictionary<string, List<string>> dictMonitorObjExpInterrupt = [];

        /// <summary>
        /// 设置对象的监视状态
        /// </summary>
        /// <param name="key">如果是节点，传入Guid；如果是对象，传入类型FullName</param>
        /// <param name="isMonitor">ture监视对象；false取消对象监视</param>
        /// <returns></returns>
        public void SetMonitorObjState(string key, bool isMonitor)
        {
            if (string.IsNullOrEmpty(key)) { return; }
            var isExist = dictMonitorObjExpInterrupt.ContainsKey(key);
            if (isExist)
            {
                if (!isMonitor) // 对象存在且需要不监视
                {
                    dictMonitorObjExpInterrupt.Remove(key, out _);
                }
            }
            else
            {
                if (isMonitor) // 对象不存在且需要监视，添加在集合中。
                {
                    dictMonitorObjExpInterrupt.TryAdd(key, new List<string>()); ;
                }
            }
        }

        /// <summary>
        /// 检查一个对象是否处于监听状态，如果是，则传出与该对象相关的表达式（用于中断），如果不是，则返回false。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="exps"></param>
        /// <returns></returns>
        public Task<(bool, string[])> CheckObjMonitorStateAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                var data = (false, Array.Empty<string>());
                return Task.FromResult(data);
            }
            else
            {
                var isMonitor = dictMonitorObjExpInterrupt.TryGetValue(key, out var exps);

                if (exps is null)
                {
                    var data = (isMonitor, Array.Empty<string>());
                    return Task.FromResult(data);
                }
                else
                {
                    var data = (isMonitor, exps.ToArray());
                    return Task.FromResult(data);
                }


            }
            
            //if (exps is null)
            //{
            //    var data = (isMonitor, Array.Empty<string>());
            //    return Task.FromResult(data);
            //}
            //else
            //{
            //    var data = (isMonitor, exps.ToArray());
            //    return Task.FromResult(data);
            //}

        }

        /// <summary>
        /// 启动器调用，运行到某个节点时触发了监视对象的更新（对象预览视图将会自动更新）
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="monitorData"></param>
        /// <param name="sourceType"></param>
        public void MonitorObjectNotification(string nodeGuid, object monitorData, MonitorObjectEventArgs.ObjSourceType sourceType)
        {
            OnMonitorObjectChange?.Invoke(new MonitorObjectEventArgs(nodeGuid, monitorData, sourceType));
        }

        /// <summary>
        /// 启动器调用，节点触发了中断。
        /// </summary>
        /// <param name="nodeGuid">节点</param>
        /// <param name="expression">表达式</param>
        /// <param name="type">类型，0用户主动的中断，1表达式中断</param>
        public void TriggerInterrupt(string nodeGuid, string expression, InterruptTriggerEventArgs.InterruptTriggerType type)
        {
            OnInterruptTrigger?.Invoke(new InterruptTriggerEventArgs(nodeGuid, expression, type));
        }


        /// <summary>
        /// 环境执行中断
        /// </summary>
        /// <returns></returns>
        public async Task<CancelType> GetOrCreateGlobalInterruptAsync()
        {
            IsGlobalInterrupt = true;
            var result = await ChannelFlowInterrupt.GetOrCreateChannelAsync(EnvName);
            return result;
        }

        /// <summary>
        /// 记录节点更改数据，防止重复更改
        /// </summary>
        public HashSet<(string, string, object)> NodeValueChangeLogger = new HashSet<(string, string, object)>();

        /// <summary>
        /// 数据更改通知（来自远程）
        /// </summary>
        /// <param name="nodeGuid">发生在哪个节点</param>
        /// <param name="path">属性路径</param>
        /// <param name="value">变化后的属性值</param>
        /// <returns></returns>
        public async Task NotificationNodeValueChangeAsync(string nodeGuid, string path, object value)
        {
            var nodeModel = GuidToModel(nodeGuid);
            if (nodeModel is null) return;
            SerinExpressionEvaluator.Evaluate($"@Set .{path} = {value}", nodeModel, out _); // 更改对应的数据

            //if (NodeValueChangeLogger.Remove((nodeGuid, path, value)))
            //{
            //    // 说明存在过重复的修改
            //    return;
            //}
            //NodeValueChangeLogger.Add((nodeGuid, path, value));

            //lock (NodeValueChangeLogger)
            //{

            //    Interlocked.Add(ref i, 1);
            //    Console.WriteLine(i);
            //    var getExp = $"@Get .{path}";
            //    var setExp = $"@Set .{path} = {value}"; // 生成 set 表达式
            //    var oldValue = SerinExpressionEvaluator.Evaluate(getExp, nodeModel, out _);
            //    if(oldValue != value)
            //    {
            //        Console.WriteLine($"旧值：{getExp},result : {oldValue}");
            //        SerinExpressionEvaluator.Evaluate(setExp, nodeModel, out _); // 更改对应的数据
            //        Console.WriteLine($"新值：{getExp},result : {SerinExpressionEvaluator.Evaluate(getExp, nodeModel, out _)}");
            //    }

            //}



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
            var nodeModel = GuidToModel(nodeGuid);
            if (nodeModel is null) return false;

            var argInfo = nodeModel.MethodDetails.ParameterDetailss
                                .Where(pd => !string.IsNullOrEmpty(pd.ArgDataSourceNodeGuid))
                                .Select(pd => (pd.ArgDataSourceNodeGuid, pd.ArgDataSourceType, pd.Index))
                                .ToArray();

            #region 暂时移除连接关系
            foreach((var fromGuid, var type, var index) in argInfo)
            {
                await UIContextOperation.InvokeAsync(() =>
                    OnNodeConnectChange?.Invoke(
                            new NodeConnectChangeEventArgs(
                                fromGuid, // 从哪个节点开始
                                nodeModel.Guid, // 连接到那个节点
                                JunctionOfConnectionType.Arg,
                                index, // 参数
                                type, // 连接线的样式类型
                                NodeConnectChangeEventArgs.ConnectChangeType.Remote // 是创建连接还是删除连接
                            ))); // 通知UI 
            }
            for (int i = 0; i < argInfo.Length; i++)
            {
                ParameterDetails? pd = nodeModel.MethodDetails.ParameterDetailss[i];
                var fromNode = GuidToModel(pd.ArgDataSourceNodeGuid);
                if (fromNode is null) continue;
                var argSourceGuid = pd.ArgDataSourceNodeGuid;
                var argSourceType = pd.ArgDataSourceType;
            }
            #endregion

            bool isPass;
            if (isAdd)
            {
                isPass = nodeModel.MethodDetails.AddParamsArg(paramIndex);
            }
            else
            {
                isPass = nodeModel.MethodDetails.RemoveParamsArg(paramIndex);
            }

            await Task.Delay(200);
            foreach ((var fromGuid, var type, var index) in argInfo)
            {
                await UIContextOperation.InvokeAsync(() =>
                    OnNodeConnectChange?.Invoke(
                            new NodeConnectChangeEventArgs(
                                fromGuid, // 从哪个节点开始
                                nodeModel.Guid, // 连接到那个节点
                                JunctionOfConnectionType.Arg,
                                index, // 参数
                                type, // 连接线的样式类型
                                NodeConnectChangeEventArgs.ConnectChangeType.Create // 是创建连接还是删除连接
                            ))); // 通知UI 

            }
            return isPass;
        }


        /// <summary>
        /// Guid 转 NodeModel
        /// </summary>
        /// <param name="nodeGuid">节点Guid</param>
        /// <returns>节点Model</returns>
        /// <exception cref="ArgumentNullException">无法获取节点、Guid/节点为null时报错</exception>
        private NodeModelBase? GuidToModel(string nodeGuid)
        {
            if (string.IsNullOrEmpty(nodeGuid))
            {
                //throw new ArgumentNullException("not contains - Guid没有对应节点:" + (nodeGuid));
                return null;
            }
            if (!NodeModels.TryGetValue(nodeGuid, out NodeModelBase? nodeModel) || nodeModel is null)
            {
                //throw new ArgumentNullException("null - Guid存在对应节点,但节点为null:" + (nodeGuid));
                return null;
            }
            return nodeModel;
        }

        #endregion

        #region 流程依赖类库的接口


        /// <summary>
        /// 添加或更新全局数据
        /// </summary>
        /// <param name="keyName">数据名称</param>
        /// <param name="data">数据集</param>
        /// <returns></returns>
        public object AddOrUpdateGlobalData(string keyName, object data)
        {
            SereinEnv.AddOrUpdateFlowGlobalData(keyName, data);
            return data;
        }

        /// <summary>
        /// 获取全局数据
        /// </summary>
        /// <param name="keyName">数据名称</param>
        /// <returns></returns>
        public object? GetGlobalData(string keyName)
        {
            return SereinEnv.GetFlowGlobalData(keyName);
        }


        /// <summary>
        /// 运行时加载
        /// </summary>
        /// <param name="file">文件名</param>
        /// <returns></returns>
        public bool LoadNativeLibraryOfRuning(string file)
        {
            return NativeDllHelper.LoadDll(file);
        }

        /// <summary>
        /// 运行时加载指定目录下的类库
        /// </summary>
        /// <param name="path">目录</param>
        /// <param name="isRecurrence">是否递归加载</param>
        public void LoadAllNativeLibraryOfRuning(string path, bool isRecurrence = true)
        {
            NativeDllHelper.LoadAllDll(path, isRecurrence);
        }

        #endregion

        #region 私有方法

        #region 暂时注释
        /*
        /// <summary>
        /// 加载指定路径的DLL文件
        /// </summary>
        /// <param name="dllPath"></param>
        private void LoadDllNodeInfo(string dllPath)
        {

            var fileName = Path.GetFileName(dllPath);
            AssemblyLoadContext flowAlc = new AssemblyLoadContext(fileName, true);
            flowAlc.LoadFromAssemblyPath(dllPath); // 加载指定路径的程序集

            foreach(var assemblt in flowAlc.Assemblies)
            {
                (var registerTypes, var mdlist) = LoadAssembly(assemblt);
                if (mdlist.Count > 0)
                {
                    var nodeLibraryInfo = new NodeLibraryInfo
                    {
                        //Assembly = assembly,
                        AssemblyName = assemblt.FullName,
                        FileName = Path.GetFileName(dllPath),
                        FilePath = dllPath,
                    };

                    LibraryInfos.TryAdd(nodeLibraryInfo.AssemblyName, nodeLibraryInfo);
                    MethodDetailsOfLibraryInfos.TryAdd(nodeLibraryInfo, mdlist);

                    foreach (var md in mdlist)
                    {
                        MethodDetailss.TryAdd(md.MethodName, md);
                    }

                    foreach (var kv in registerTypes)
                    {
                        if (!AutoRegisterTypes.TryGetValue(kv.Key, out var types))
                        {
                            types = new List<Type>();
                            AutoRegisterTypes.Add(kv.Key, types);
                        }
                        types.AddRange(kv.Value);
                    }
                    var mdInfos = mdlist.Select(md => md.ToInfo()).ToList(); // 转换成方法信息

                    if (OperatingSystem.IsWindows())
                    {
                        UIContextOperation?.Invoke(() => OnDllLoad?.Invoke(new LoadDllEventArgs(nodeLibraryInfo, mdInfos))); // 通知UI创建dll面板显示

                    }
                }


            }


           

        }*/ 
        #endregion

        /// <summary>
        /// 移除连接关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点Model</param>
        /// <param name="toNodeGuid">目标节点Model</param>
        /// <param name="connectionType">连接关系</param>
        /// <exception cref="NotImplementedException"></exception>
        private async Task<bool> RemoteConnectAsync(NodeModelBase fromNode, NodeModelBase toNode, ConnectionInvokeType connectionType)
        {
            fromNode.SuccessorNodes[connectionType].Remove(toNode);
            toNode.PreviousNodes[connectionType].Remove(fromNode);


            if (OperatingSystem.IsWindows())
            {
                await UIContextOperation.InvokeAsync(() => OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(
                                                                          fromNode.Guid,
                                                                          toNode.Guid,
                                                                          JunctionOfConnectionType.Invoke,
                                                                          connectionType,
                                                                          NodeConnectChangeEventArgs.ConnectChangeType.Remote)));
            }
            return true;
        }
         /// <summary>
        /// 移除连接关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点Model</param>
        /// <param name="toNodeGuid">目标节点Model</param>
        /// <param name="connectionType">连接关系</param>
        /// <exception cref="NotImplementedException"></exception>
        private async Task<bool> RemoteConnectAsync(NodeModelBase fromNode, NodeModelBase toNode, int argIndex)
        {
            if (string.IsNullOrEmpty(toNode.MethodDetails.ParameterDetailss[argIndex].ArgDataSourceNodeGuid))
            {
                return false;
            }
            toNode.MethodDetails.ParameterDetailss[argIndex].ArgDataSourceNodeGuid = null;
            toNode.MethodDetails.ParameterDetailss[argIndex].ArgDataSourceType = ConnectionArgSourceType.GetPreviousNodeData; // 恢复默认值

            if (OperatingSystem.IsWindows())
            {
                await UIContextOperation.InvokeAsync(() => OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(
                                                                          fromNode.Guid,
                                                                          toNode.Guid,
                                                                          JunctionOfConnectionType.Arg,
                                                                          argIndex,
                                                                          ConnectionArgSourceType.GetPreviousNodeData,
                                                                          NodeConnectChangeEventArgs.ConnectChangeType.Remote)));
            }
            return true;
        }


        /// <summary>
        /// 创建节点
        /// </summary>
        /// <param name="nodeBase"></param>
        private bool TryAddNode(NodeModelBase nodeModel)
        {
            nodeModel.Guid ??= Guid.NewGuid().ToString();
            NodeModels[nodeModel.Guid] = nodeModel;

            // 如果是触发器，则需要添加到专属集合中
            if (nodeModel is SingleFlipflopNode flipflopNode)
            {
                var guid = flipflopNode.Guid;
                if (!FlipflopNodes.Exists(it => it.Guid.Equals(guid)))
                {
                    FlipflopNodes.Add(flipflopNode);
                }
            }
            return true;
        }

        /// <summary>
        /// 检查连接
        /// </summary>
        /// <param name="fromNode">发起连接的起始节点</param>
        /// <param name="toNode">要连接的目标节点</param>
        /// <param name="fromNodeJunctionType">发起连接节点的控制点类型</param>
        /// <param name="toNodeJunctionType">被连接节点的控制点类型</param>
        /// <returns></returns>
        public static (JunctionOfConnectionType,bool) CheckConnect(NodeModelBase fromNode,
                                                                    NodeModelBase toNode, 
                                                                    JunctionType fromNodeJunctionType,
                                                                    JunctionType toNodeJunctionType)
        {
            var type = JunctionOfConnectionType.None;
            var state = false;
            if (fromNodeJunctionType == JunctionType.Execute)
            {
                if (toNodeJunctionType == JunctionType.NextStep && !fromNode.Guid.Equals(toNode.Guid))
                {
                    // “方法执行”控制点拖拽到“下一节点”控制点，且不是同一个节点， 添加方法执行关系
                    type = JunctionOfConnectionType.Invoke;
                    state = true;
                }
                //else if (toNodeJunctionType == JunctionType.ArgData && fromNode.Guid.Equals(toNode.Guid)) 
                //{
                //    // “方法执行”控制点拖拽到“方法入参”控制点，且是同一个节点，则添加获取参数关系，表示生成入参参数时自动从该节点的上一节点获取flowdata
                //    type = JunctionOfConnectionType.Arg;
                //    state = true;
                //}
            }
            else if (fromNodeJunctionType == JunctionType.NextStep && !fromNode.Guid.Equals(toNode.Guid))
            {
                // “下一节点”控制点只能拖拽到“方法执行”控制点，且不能是同一个节点
                if (toNodeJunctionType == JunctionType.Execute && !fromNode.Guid.Equals(toNode.Guid))
                {
                    type = JunctionOfConnectionType.Invoke;
                    state = true;
                }
            }
            else if (fromNodeJunctionType == JunctionType.ArgData)
            {
                //if (toNodeJunctionType == JunctionType.Execute && fromNode.Guid.Equals(toNode.Guid)) // 添加获取参数关系
                //{
                //    // “方法入参”控制点拖拽到“方法执行”控制点，且是同一个节点，则添加获取参数关系，生成入参参数时自动从该节点的上一节点获取flowdata
                //    type = JunctionOfConnectionType.Arg;
                //    state = true;
                //}
                if(toNodeJunctionType == JunctionType.ReturnData && !fromNode.Guid.Equals(toNode.Guid))
                {
                    // “”控制点拖拽到“方法返回值”控制点，且不是同一个节点，添加获取参数关系，生成参数时从目标节点获取flowdata
                    type = JunctionOfConnectionType.Arg;
                    state = true;
                }
            }
            else if (fromNodeJunctionType == JunctionType.ReturnData)
            {
                if (toNodeJunctionType == JunctionType.ArgData && !fromNode.Guid.Equals(toNode.Guid))
                {
                    // “方法返回值”控制点拖拽到“方法入参”控制点，且不是同一个节点，添加获取参数关系，生成参数时从目标节点获取flowdata
                    type = JunctionOfConnectionType.Arg;
                    state = true;
                }
            }
            // 剩下的情况都是不符预期的连接行为，忽略。
            return (type,state);
        }

        /// <summary>
        /// 连接节点
        /// </summary>
        /// <param name="fromNode">起始节点</param>
        /// <param name="toNode">目标节点</param>
        /// <param name="invokeType">连接关系</param>
        private bool ConnectInvokeOfNode(NodeModelBase fromNode, NodeModelBase toNode, ConnectionInvokeType invokeType)
        {
            if (fromNode is null || toNode is null || fromNode == toNode)
            {
                return false;
            }

            var ToExistOnFrom = true;
            var FromExistInTo = true;
            ConnectionInvokeType[] ct = [ConnectionInvokeType.IsSucceed,
                                   ConnectionInvokeType.IsFail,
                                   ConnectionInvokeType.IsError,
                                   ConnectionInvokeType.Upstream];

            if (toNode is SingleFlipflopNode flipflopNode)
            {
                flowStarter?.TerminateGlobalFlipflopRuning(flipflopNode); // 假设被连接的是全局触发器，尝试移除
            }

            var isPass = false;
            foreach (ConnectionInvokeType ctType in ct)
            {
                var FToTo = fromNode.SuccessorNodes[ctType].Where(it => it.Guid.Equals(toNode.Guid)).ToArray();
                var ToOnF = toNode.PreviousNodes[ctType].Where(it => it.Guid.Equals(fromNode.Guid)).ToArray();
                ToExistOnFrom = FToTo.Length > 0;
                FromExistInTo = ToOnF.Length > 0;
                if (ToExistOnFrom && FromExistInTo)
                {
                    SereinEnv.WriteLine(InfoType.WARN, "起始节点已与目标节点存在连接");
                    isPass = false;
                }
                else
                {
                    // 检查是否可能存在异常
                    if (!ToExistOnFrom && FromExistInTo)
                    {
                        SereinEnv.WriteLine(InfoType.WARN, "目标节点不是起始节点的子节点，起始节点却是目标节点的父节点");
                        isPass = false;
                    }
                    else if (ToExistOnFrom && !FromExistInTo)
                    {
                        //
                        SereinEnv.WriteLine(InfoType.WARN, " 起始节点不是目标节点的父节点，目标节点却是起始节点的子节点");
                        isPass =  false;
                    }
                    else
                    {
                        isPass = true;
                    }
                }
            }
            if (isPass)
            {

                fromNode.SuccessorNodes[invokeType].Add(toNode); // 添加到起始节点的子分支
                toNode.PreviousNodes[invokeType].Add(fromNode); // 添加到目标节点的父分支
                if (OperatingSystem.IsWindows())
                {

                    UIContextOperation?.Invoke(() => 
                        OnNodeConnectChange?.Invoke(
                                new NodeConnectChangeEventArgs(
                                    fromNode.Guid, // 从哪个节点开始
                                    toNode.Guid, // 连接到那个节点
                                    JunctionOfConnectionType.Invoke, 
                                    invokeType, // 连接线的样式类型
                                    NodeConnectChangeEventArgs.ConnectChangeType.Create // 是创建连接还是删除连接
                                ))); // 通知UI 
                }
                // Invoke
                // GetResult
                return true;
            }
            else
            {
                return false;
            }


        }

        /// <summary>
        /// 连接节点参数
        /// </summary>
        /// <param name="fromNode"></param>
        /// <param name="toNode"></param>
        /// <param name="connectionArgSourceType"></param>
        /// <param name="argIndex"></param>
        /// <returns></returns>
        private async Task<bool> ConnectArgSourceOfNodeAsync(NodeModelBase fromNode,
                                                             NodeModelBase toNode,
                                                             ConnectionArgSourceType connectionArgSourceType,
                                                             int argIndex)
        {
            var toNodeArgSourceGuid = toNode.MethodDetails.ParameterDetailss[argIndex].ArgDataSourceNodeGuid;
            if (!string.IsNullOrEmpty(toNodeArgSourceGuid))
            {
                await RemoteConnectAsync(fromNode, toNode, argIndex);
            }
            toNode.MethodDetails.ParameterDetailss[argIndex].ArgDataSourceNodeGuid = fromNode.Guid;
            toNode.MethodDetails.ParameterDetailss[argIndex].ArgDataSourceType = connectionArgSourceType;
            await UIContextOperation.InvokeAsync(() =>
                        OnNodeConnectChange?.Invoke(
                                new NodeConnectChangeEventArgs(
                                    fromNode.Guid, // 从哪个节点开始
                                    toNode.Guid, // 连接到那个节点
                                    JunctionOfConnectionType.Arg,
                                    argIndex, // 连接线的样式类型
                                    connectionArgSourceType,
                                    NodeConnectChangeEventArgs.ConnectChangeType.Create // 是创建连接还是删除连接
                                ))); // 通知UI 
            return true;
        }


        /// <summary>
        /// 更改起点节点
        /// </summary>
        /// <param name="newStartNode"></param>
        /// <param name="oldStartNode"></param>
        private void SetStartNode(NodeModelBase newStartNode)
        {
            var oldNodeGuid = StartNode?.Guid;
            StartNode = newStartNode;
            if (OperatingSystem.IsWindows())
            {
                UIContextOperation?.Invoke(() => OnStartNodeChange?.Invoke(new StartNodeChangeEventArgs(oldNodeGuid, StartNode.Guid))); 
            }
           
        }

        ///// <summary>
        ///// 输出内容
        ///// </summary>
        ///// <param name="msg"></param>
        //private void Output(string msg)
        //{
        //    if (OperatingSystem.IsWindows())
        //    {
        //        UIContextOperation?.Invoke(() => OnEnvOut?.Invoke(msg)); 
        //    }
            
        //}

        #endregion

        #region 视觉效果

        /// <summary>
        /// 定位节点
        /// </summary>
        /// <param name="nodeGuid"></param>
        public void NodeLocated(string nodeGuid)
        {
            if (OperatingSystem.IsWindows())
            {
                UIContextOperation?.Invoke(() => OnNodeLocated?.Invoke(new NodeLocatedEventArgs(nodeGuid))); 
            }
           
        }

        #endregion

        #region IOC容器相关
        ISereinIOC ISereinIOC.Reset()
        {
            sereinIOC.Reset();
            return this;
        }

        ISereinIOC ISereinIOC.Register(Type type, params object[] parameters)
        {
            sereinIOC.Register(type, parameters);
            return this;
        }

        ISereinIOC ISereinIOC.Register<T>(params object[] parameters)
        {
            sereinIOC.Register<T>(parameters);
            return this;
        }

        ISereinIOC ISereinIOC.Register<TService, TImplementation>(params object[] parameters)
        {
            sereinIOC.Register<TService, TImplementation>(parameters);
            return this;
        }

        //T ISereinIOC.GetOrRegisterInstantiate<T>()
        //{
        //    return sereinIOC.GetOrRegisterInstantiate<T>();

        //}

        //object ISereinIOC.GetOrRegisterInstantiate(Type type)
        //{
        //    return sereinIOC.GetOrRegisterInstantiate(type);
        //}

        object ISereinIOC.Get(Type type)
        {
            return sereinIOC.Get(type);

        }
        T ISereinIOC.Get<T>()
        {
            return (T)sereinIOC.Get(typeof(T));
        }
        T ISereinIOC.Get<T>(string key)
        {
            return sereinIOC.Get<T>(key);
        }


        bool ISereinIOC.CustomRegisterInstance(string key, object instance, bool needInjectProperty)
        {
            return sereinIOC.CustomRegisterInstance(key, instance, needInjectProperty);
        }

        object ISereinIOC.Instantiate(Type type)
        {
            return sereinIOC.Instantiate(type);
        }
        T ISereinIOC.Instantiate<T>()
        {
            return sereinIOC.Instantiate<T>();
        }
        ISereinIOC ISereinIOC.Build()
        {
            sereinIOC.Build();
            return this;
        }

        ISereinIOC ISereinIOC.Run<T>(Action<T> action)
        {
            sereinIOC.Run(action);
            return this;
        }

        ISereinIOC ISereinIOC.Run<T1, T2>(Action<T1, T2> action)
        {
            sereinIOC.Run(action);
            return this;
        }

        ISereinIOC ISereinIOC.Run<T1, T2, T3>(Action<T1, T2, T3> action)
        {
            sereinIOC.Run(action);
            return this;
        }

        ISereinIOC ISereinIOC.Run<T1, T2, T3, T4>(Action<T1, T2, T3, T4> action)
        {
            sereinIOC.Run(action);
            return this;
        }

        ISereinIOC ISereinIOC.Run<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> action)
        {
            sereinIOC.Run(action);
            return this;
        }

        ISereinIOC ISereinIOC.Run<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> action)
        {
            sereinIOC.Run(action);
            return this;
        }

        ISereinIOC ISereinIOC.Run<T1, T2, T3, T4, T5, T6, T7>(Action<T1, T2, T3, T4, T5, T6, T7> action)
        {
            sereinIOC.Run(action);
            return this;
        }

        ISereinIOC ISereinIOC.Run<T1, T2, T3, T4, T5, T6, T7, T8>(Action<T1, T2, T3, T4, T5, T6, T7, T8> action)
        {
            sereinIOC.Run(action);
            return this;
        }
        #endregion


    }


   









}
