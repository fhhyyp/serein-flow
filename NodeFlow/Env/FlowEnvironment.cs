
using Newtonsoft.Json;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.Library.Utils.SereinExpression;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Tool;
using System.Collections.Concurrent;
using System.Reflection;
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
            this.UIContextOperation = uiContextOperation; // 本地环境需要存放视图管理
        }

        #region 远程管理

        private MsgControllerOfServer clientMsgManage;


        /// <summary>
        /// <para>表示是否正在控制远程</para>
        /// <para>Local control remote env</para>
        /// </summary>
        public bool IsLcR { get; set; }
        /// <summary>
        /// <para>表示是否受到远程控制</para>
        /// <para>Remote control local env</para>
        /// </summary>
        public bool IsRcL { get; set; }



        /// <summary>
        /// 打开远程管理
        /// </summary>
        /// <param name="port"></param>
        public async Task StartRemoteServerAsync(int port = 7525)
        {
            if (clientMsgManage is null)
            {
                clientMsgManage = new MsgControllerOfServer(this);
                //clientMsgManage = new MsgControllerOfServer(this, "token");
            }
            await clientMsgManage.StartRemoteServerAsync(port);
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
                Console.WriteLine("结束远程管理异常：" + ex);
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
        /// Library 与 MethodDetailss的依赖关系
        /// </summary>
        public ConcurrentDictionary<NodeLibrary, List<MethodDetails>> MethodDetailsOfLibrarys { get; } = [];

        /// <summary>
        /// 存储已加载的程序集
        /// </summary>
        public ConcurrentDictionary<string, NodeLibrary> Librarys { get; } = [];

        /// <summary>
        /// 存储已加载的方法信息。描述所有DLL中NodeAction特性的方法的原始副本
        /// </summary>
        public ConcurrentDictionary<string, MethodDetails> MethodDetailss { get; } = [];

        
        /// <summary>
        /// 容器管理
        /// </summary>
        private readonly SereinIOC sereinIOC;

        /// <summary>
        /// 环境加载的节点集合
        /// Node Guid - Node Model
        /// </summary>
        private Dictionary<string, NodeModelBase> Nodes { get; } = [];

        /// <summary>
        /// 存放触发器节点（运行时全部调用）
        /// </summary>
        private List<SingleFlipflopNode> FlipflopNodes { get; } = [];

        /// <summary>
        /// 从dll中加载的类的注册类型
        /// </summary>
        private Dictionary<RegisterSequence, List<Type>> AutoRegisterTypes { get; } = [];

        /// <summary>
        /// 存放委托
        /// 
        /// md.Methodname - delegate
        /// </summary>

        private ConcurrentDictionary<string, DelegateDetails> MethodDelegates { get; } = [];

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

        /// <summary>
        /// 重定向Console输出
        /// </summary>
        public void SetConsoleOut()
        {
            var logTextWriter = new LogTextWriter(msg => Output(msg));
            Console.SetOut(logTextWriter);
        }

        /// <summary>
        /// 使用JSON处理库输出对象信息
        /// </summary>
        /// <param name="obj"></param>
        public void WriteLineObjToJson(object obj)
        {
            var msg = JsonConvert.SerializeObject(obj);
            if (OperatingSystem.IsWindows())
            {
                UIContextOperation?.Invoke(() => OnEnvOut?.Invoke(msg + Environment.NewLine)); 
            }

        }

        /// <summary>
        /// 异步运行
        /// </summary>
        /// <returns></returns>
        public async Task StartAsync()
        {
            ChannelFlowInterrupt?.CancelAllTasks();
            flowStarter = new FlowStarter();
            var nodes = Nodes.Values.ToList();

            List<MethodDetails> initMethods = [];
            List<MethodDetails> loadMethods = [];
            List<MethodDetails> exitMethods = [];
            //foreach(var mds in MethodDetailss.Values)
            //{
            //    var initMds = mds.Where(it => it.MethodDynamicType == NodeType.Init);
            //    var loadMds = mds.Where(it => it.MethodDynamicType == NodeType.Loading);
            //    var exitMds = mds.Where(it => it.MethodDynamicType == NodeType.Exit);
            //    initMethods.AddRange(initMds);
            //    loadMethods.AddRange(loadMds);
            //    exitMethods.AddRange(exitMds);
            //}
            var initMds = MethodDetailss.Values.Where(it => it.MethodDynamicType == NodeType.Init);
            var loadMds = MethodDetailss.Values.Where(it => it.MethodDynamicType == NodeType.Loading);
            var exitMds = MethodDetailss.Values.Where(it => it.MethodDynamicType == NodeType.Exit);
            initMethods.AddRange(initMds);
            loadMethods.AddRange(loadMds);
            exitMethods.AddRange(exitMds);

            IOC.Reset(); // 开始运行时清空ioc中注册的实例

            IOC.CustomRegisterInstance(typeof(IFlowEnvironment).FullName, this);
            if (this.UIContextOperation is not null)
                IOC.CustomRegisterInstance(typeof(UIContextOperation).FullName, this.UIContextOperation, false);

            await flowStarter.RunAsync(this, nodes, AutoRegisterTypes, initMethods, loadMethods, exitMethods);

            if (FlipFlopState == RunState.Completion)
            {
                ExitFlow(); // 未运行触发器时，才会调用结束方法
            }
            flowStarter = null;
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
                return;
            }
            if (FlowState == RunState.Running || FlipFlopState == RunState.Running)
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
        /// 退出
        /// </summary>
        public void ExitFlow()
        {
            ChannelFlowInterrupt?.CancelAllTasks();
            flowStarter?.Exit();

            foreach (var node in Nodes.Values)
            {
                if (node is not null)
                {
                    node.ReleaseFlowData(); // 退出时释放对象计数
                }
            }
            UIContextOperation?.Invoke(() => OnFlowRunComplete?.Invoke(new FlowEventArgs()));

           

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
        // [AutoSocketHandle]
        public async Task<FlowEnvInfo> GetEnvInfoAsync()
        {
            Dictionary<NodeLibrary, List<MethodDetailsInfo>> LibraryMds = [];

            foreach (var mdskv in MethodDetailsOfLibrarys)
            {
                var library = mdskv.Key;
                var mds = mdskv.Value;
                foreach (var md in mds)
                {
                    if (!LibraryMds.TryGetValue(library, out var t_mds))
                    {
                        t_mds = new List<MethodDetailsInfo>();
                        LibraryMds[library] = t_mds;
                    }
                    var mdInfo = md.ToInfo();
                    mdInfo.LibraryName = library.FullName;
                    t_mds.Add(mdInfo);
                }
            }

            LibraryMds[] libraryMdss = LibraryMds.Select(kv => new LibraryMds
            {
                LibraryName = kv.Key.FullName,
                Mds = kv.Value.ToArray()
            }).ToArray();
            var project = await GetProjectInfoAsync();
            Console.WriteLine("已将当前环境信息发送到远程客户端");
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
        /// 加载项目文件
        /// </summary>
        /// <param name="flowEnvInfo">环境信息</param>
        /// <param name="filePath"></param>
        public void LoadProject(FlowEnvInfo flowEnvInfo, string filePath)
        {
            var projectData = flowEnvInfo.Project;
            // 加载项目配置文件
            var dllPaths = projectData.Librarys.Select(it => it.FileName).ToList();
            List<MethodDetails> methodDetailss = [];

            // 遍历依赖项中的特性注解，生成方法详情
            foreach (var dllPath in dllPaths)
            {
                var dllFilePath = Path.GetFullPath(Path.Combine(filePath, dllPath));
                LoadDllNodeInfo(dllFilePath);
            }

            List<(NodeModelBase, string[])> regionChildNodes = new List<(NodeModelBase, string[])>();
            List<(NodeModelBase, PositionOfUI)> ordinaryNodes = new List<(NodeModelBase, PositionOfUI)>();
            // 加载节点
            foreach (var nodeInfo in projectData.Nodes)
            {
                var controlType = FlowFunc.GetNodeControlType(nodeInfo);
                if (controlType == NodeControlType.None)
                {
                    continue;
                }
                else
                {
                    MethodDetails? methodDetails = null;
                    if (!string.IsNullOrEmpty(nodeInfo.MethodName))
                    {
                        MethodDetailss.TryGetValue(nodeInfo.MethodName, out methodDetails);// 加载项目时尝试获取方法信息
                    }
                    else
                    {

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
            }
            // 加载区域子项
            foreach ((NodeModelBase region, string[] childNodeGuids) item in regionChildNodes)
            {
                foreach (var childNodeGuid in item.childNodeGuids)
                {
                    Nodes.TryGetValue(childNodeGuid, out NodeModelBase? childNode);
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
            foreach (var nodeInfo in projectData.Nodes)
            {
                if (!Nodes.TryGetValue(nodeInfo.Guid, out NodeModelBase? fromNode))
                {
                    // 不存在对应的起始节点
                    continue;
                }


                List<(ConnectionType connectionType, string[] guids)> allToNodes = [(ConnectionType.IsSucceed,nodeInfo.TrueNodes),
                                                                                    (ConnectionType.IsFail,   nodeInfo.FalseNodes),
                                                                                    (ConnectionType.IsError,  nodeInfo.ErrorNodes),
                                                                                    (ConnectionType.Upstream, nodeInfo.UpstreamNodes)];

                List<(ConnectionType, NodeModelBase[])> fromNodes = allToNodes.Where(info => info.guids.Length > 0)
                                                                     .Select(info => (info.connectionType,
                                                                                      info.guids.Where(guid => Nodes.ContainsKey(guid)).Select(guid => Nodes[guid])
                                                                                        .ToArray()))
                                                                     .ToList();
                // 遍历每种类型的节点分支（四种）
                foreach ((ConnectionType connectionType, NodeModelBase[] toNodes) item in fromNodes)
                {
                    // 遍历当前类型分支的节点（确认连接关系）
                    foreach (var toNode in item.toNodes)
                    {
                        ConnectNodeAsync(fromNode, toNode, item.connectionType); // 加载时确定节点间的连接关系
                    }
                }
            }

            SetStartNode(projectData.StartNode);
            UIContextOperation?.Invoke(() => OnProjectLoaded?.Invoke(new ProjectLoadedEventArgs()));
            

        }

        /// <summary>
        /// 加载远程环境
        /// </summary>
        /// <param name="addres">远程环境地址</param>
        /// <param name="port">远程环境端口</param>
        /// <param name="token">密码</param>
        public async Task<(bool, RemoteEnvControl)> ConnectRemoteEnv(string addres, int port, string token)
        {
            if (IsLcR)
            {
                await Console.Out.WriteLineAsync($"当前已经连接远程环境");
                return (false, null);
            }
            // 没有连接远程环境，可以重新连接

            var controlConfiguration = new RemoteEnvControl.ControlConfiguration
            {
                Addres = addres,
                Port = port,
                Token = token,
                ThemeJsonKey = FlowEnvironment.ThemeKey,
                MsgIdJsonKey = FlowEnvironment.MsgIdKey,
                DataJsonKey = FlowEnvironment.DataKey,
            };
            var remoteEnvControl = new RemoteEnvControl(controlConfiguration);
            var result = await remoteEnvControl.ConnectAsync();
            if (!result)
            {
                await Console.Out.WriteLineAsync("连接失败，请检查地址与端口是否正确");
                return (false, null);
            }
            await Console.Out.WriteLineAsync("连接成功，开始验证Token");
            IsLcR = true;
            return (true, remoteEnvControl);
        }

        /// <summary>
        /// 退出远程环境
        /// </summary>
        public void ExitRemoteEnv()
        {
            IsLcR = false;
        }

        /// <summary>
        /// 序列化当前项目的依赖信息、节点信息
        /// </summary>
        /// <returns></returns>
        public Task<SereinProjectData> GetProjectInfoAsync()
        {
            var projectData = new SereinProjectData()
            {
                Librarys = Librarys.Values.Select(lib => lib.ToLibrary()).ToArray(),
                Nodes = Nodes.Values.Select(node => node.ToInfo()).Where(info => info is not null).ToArray(),
                StartNode = Nodes.Values.FirstOrDefault(it => it.IsStart)?.Guid,
            };
            return Task.FromResult(projectData);
        }


        /// <summary>
        /// 从文件路径中加载DLL
        /// </summary>
        /// <param name="dllPath"></param>
        /// <returns></returns> 
        // [AutoSocketHandle]
        public void LoadDll(string dllPath)
        {
            LoadDllNodeInfo(dllPath);
        }

        /// <summary>
        /// 移除DLL
        /// </summary>
        /// <param name="assemblyFullName"></param>
        /// <returns></returns>
        public bool RemoteDll(string assemblyFullName)
        {
            var library = Librarys.Values.FirstOrDefault(nl => assemblyFullName.Equals(nl.FullName));
            if (library is null)
            {
                return false;
            }
            var groupedNodes = Nodes.Values
                .Where(node => node.MethodDetails is not null)
                .ToArray()
                .GroupBy(node => node.MethodDetails!.MethodName)
                .ToDictionary(
                key => key.Key,
                group => group.Count());


            if (Nodes.Count == 0)
            {
                return true; // 当前无节点，可以直接删除
            }

            if (MethodDetailsOfLibrarys.TryGetValue(library, out var mds)) // 存在方法
            {
                foreach (var md in mds)
                {
                    if (groupedNodes.TryGetValue(md.MethodName, out int count))
                    {
                        if (count > 0)
                        {
                            return false; // 创建过相关的节点，无法移除
                        }
                    }
                }
                // 开始移除相关信息
                foreach (var md in mds)
                {
                    MethodDetailss.TryRemove(md.MethodName, out _);
                }
                MethodDetailsOfLibrarys.TryRemove(library, out _);
                return true;
            }
            else
            {
                return true;
            }
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
                if (MethodDetailss.TryGetValue(methodDetailsInfo.MethodName, out var methodDetails))
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

            //if (remoteNode.IsStart)
            //{
            //    return;
            //}
            if (remoteNode is SingleFlipflopNode flipflopNode)
            {
                flowStarter?.TerminateGlobalFlipflopRuning(flipflopNode); // 假设被移除的是全局触发器，尝试从启动器移除
            }


            // 遍历所有父节点，从那些父节点中的子节点集合移除该节点
            foreach (var pnc in remoteNode.PreviousNodes)
            {
                var pCType = pnc.Key; // 连接类型
                for (int i = 0; i < pnc.Value.Count; i++)
                {
                    NodeModelBase? pNode = pnc.Value[i];
                    pNode.SuccessorNodes[pCType].Remove(remoteNode);

                    UIContextOperation?.Invoke(() => OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(pNode.Guid,
                                                                    remoteNode.Guid,
                                                                    pCType,
                                                                    NodeConnectChangeEventArgs.ConnectChangeType.Remote))); // 通知UI

                }
            }

            // 遍历所有子节点，从那些子节点中的父节点集合移除该节点
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
            Nodes.Remove(nodeGuid);
            UIContextOperation?.Invoke(() => OnNodeRemove?.Invoke(new NodeRemoveEventArgs(nodeGuid)));
            return true;
        }

        /// <summary>
        /// 连接节点
        /// </summary>
        /// <param name="fromNodeGuid">起始节点</param>
        /// <param name="toNodeGuid">目标节点</param>
        /// <param name="connectionType">连接关系</param>
        public async Task<bool> ConnectNodeAsync(string fromNodeGuid, string toNodeGuid, ConnectionType connectionType)
        {
            // 获取起始节点与目标节点
            var fromNode = GuidToModel(fromNodeGuid);
            var toNode = GuidToModel(toNodeGuid);
            if (fromNode is null || toNode is null) return false;

            // 开始连接
            return await ConnectNodeAsync(fromNode, toNode, connectionType); // 外部调用连接方法

        }

        /// <summary>
        /// 移除连接关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点Guid</param>
        /// <param name="toNodeGuid">目标节点Guid</param>
        /// <param name="connectionType">连接关系</param>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<bool> RemoveConnectAsync(string fromNodeGuid, string toNodeGuid, ConnectionType connectionType)
        {
            // 获取起始节点与目标节点
            var fromNode = GuidToModel(fromNodeGuid);
            var toNode = GuidToModel(toNodeGuid);
            if (fromNode is null || toNode is null) return false;
            var result = await RemoteConnectAsync(fromNode, toNode, connectionType);
            return result;
        }



        /// <summary>
        /// 获取方法描述
        /// </summary>

        public bool TryGetMethodDetailsInfo(string name, out MethodDetailsInfo? md)
        {
            if (!string.IsNullOrEmpty(name))
            {
                foreach (var t_md in MethodDetailss.Values)
                {
                    md = t_md.ToInfo();
                    if (md != null)
                    {
                        return true;
                    }
                }
                md = null;
                return false;
            }
            else
            {
                md = null;
                return false;
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
        public bool TryGetDelegateDetails(string methodName, out DelegateDetails? delegateDetails)
        {

            if (!string.IsNullOrEmpty(methodName) && MethodDelegates.TryGetValue(methodName, out delegateDetails))
            {
                return delegateDetails != null;
            }
            else
            {
                delegateDetails = null;
                return false;
            }
        }


        /// <summary>
        /// 移动了某个节点(远程插件使用）
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public void MoveNode(string nodeGuid, double x, double y)
        {
            var nodeModel = GuidToModel(nodeGuid);
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
        public Task<bool> SetNodeInterruptAsync(string nodeGuid, InterruptClass interruptClass)
        {
           

            var nodeModel = GuidToModel(nodeGuid);
            if (nodeModel is null) 
                return Task.FromResult(false);
            if (interruptClass == InterruptClass.None)
            {
                nodeModel.CancelInterrupt();
            }
            else if (interruptClass == InterruptClass.Branch)
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
                    ChannelFlowInterrupt.TriggerSignal(nodeGuid);
                };

            }
            else if (interruptClass == InterruptClass.Global) // 全局……做不了omg
            {
                return Task.FromResult(false);
            }
            nodeModel.DebugSetting.InterruptClass = interruptClass; 
            if (OperatingSystem.IsWindows())
            {

                UIContextOperation?.Invoke(() => OnNodeInterruptStateChange?.Invoke(new NodeInterruptStateChangeEventArgs(nodeGuid, interruptClass))); 
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
            if (NodeValueChangeLogger.Remove((nodeGuid, path, value)))
            {
                // 说明存在过重复的修改
                return;
            }
            NodeValueChangeLogger.Add((nodeGuid, path, value));
            var setExp = $"@Set .{path} = {value}"; // 生成 set 表达式
            //var getExp = $"@Get .{path}";
            SerinExpressionEvaluator.Evaluate(setExp, nodeModel, out _); // 更改对应的数据
            //var getResult = SerinExpressionEvaluator.Evaluate(getExp, nodeModel, out _);
            //Console.WriteLine($"Set表达式：{setExp},result : {getResult}");
          

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
            if (!Nodes.TryGetValue(nodeGuid, out NodeModelBase? nodeModel) || nodeModel is null)
            {
                //throw new ArgumentNullException("null - Guid存在对应节点,但节点为null:" + (nodeGuid));
                return null;
            }
            return nodeModel;
        }

        #endregion

        #region 私有方法


        /// <summary>
        /// 加载指定路径的DLL文件
        /// </summary>
        /// <param name="dllPath"></param>

        private void LoadDllNodeInfo(string dllPath)
        {
            (var nodeLibrary, var registerTypes, var mdlist) = LoadAssembly(dllPath);
            if (nodeLibrary is not null && mdlist.Count > 0)
            {
                Librarys.TryAdd(nodeLibrary.FullName, nodeLibrary);
                MethodDetailsOfLibrarys.TryAdd(nodeLibrary, mdlist);

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
                    UIContextOperation?.Invoke(() => OnDllLoad?.Invoke(new LoadDllEventArgs(nodeLibrary, mdInfos))); // 通知UI创建dll面板显示

                }
            }

        }

        /// <summary>
        /// 移除连接关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点Model</param>
        /// <param name="toNodeGuid">目标节点Model</param>
        /// <param name="connectionType">连接关系</param>
        /// <exception cref="NotImplementedException"></exception>
        private async Task<bool> RemoteConnectAsync(NodeModelBase fromNode, NodeModelBase toNode, ConnectionType connectionType)
        {
            fromNode.SuccessorNodes[connectionType].Remove(toNode);
            toNode.PreviousNodes[connectionType].Remove(fromNode);


            if (OperatingSystem.IsWindows())
            {
                await UIContextOperation.InvokeAsync(() => OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(fromNode.Guid,
                                                                          toNode.Guid,
                                                                          connectionType,
                                                                          NodeConnectChangeEventArgs.ConnectChangeType.Remote)));
            }
            //else if (OperatingSystem.IsLinux())
            //{

            //}

            return true;
        }

        private (NodeLibrary?, Dictionary<RegisterSequence, List<Type>>, List<MethodDetails>) LoadAssembly(string dllPath)
        {
            try
            {
                Assembly assembly = Assembly.LoadFrom(dllPath); // 加载DLL文件
                Type[] types = assembly.GetTypes(); // 获取程序集中的所有类型

                Dictionary<RegisterSequence, List<Type>> autoRegisterTypes = new Dictionary<RegisterSequence, List<Type>>();
                foreach (Type type in types)
                {
                    var autoRegisterAttribute = type.GetCustomAttribute<AutoRegisterAttribute>();
                    if (autoRegisterAttribute is not null)
                    {
                        if (!autoRegisterTypes.TryGetValue(autoRegisterAttribute.Class, out var valus))
                        {
                            valus = new List<Type>();
                            autoRegisterTypes.Add(autoRegisterAttribute.Class, valus);
                        }
                        valus.Add(type);
                    }

                }


                //Dictionary<Sequence, Type> autoRegisterTypes = assembly.GetTypes().Where(t => t.GetCustomAttribute<AutoRegisterAttribute>() is not null).ToList();



                List<(Type, string)> scanTypes = types.Select(t =>
                {
                    if (t.GetCustomAttribute<DynamicFlowAttribute>() is DynamicFlowAttribute dynamicFlowAttribute
                       && dynamicFlowAttribute.Scan == true)
                    {
                        return (t, dynamicFlowAttribute.Name);
                    }
                    else
                    {
                        return (null, null);
                    }
                }).Where(it => it.t is not null).ToList();
                if (scanTypes.Count == 0)
                {
                    return (null, [], []);
                }

                List<MethodDetails> methodDetails = new List<MethodDetails>();
                // 遍历扫描的类型
                foreach ((var type, var flowName) in scanTypes)
                {
                    // 加载DLL，创建 MethodDetails、实例作用对象、委托方法
                    var assemblyName = type.Assembly.GetName().Name;
                    if (string.IsNullOrEmpty(assemblyName))
                    {
                        continue;
                    }
                    var methods = NodeMethodDetailsHelper.GetMethodsToProcess(type);
                    foreach (var method in methods)
                    {
                        (var md, var del) = NodeMethodDetailsHelper.CreateMethodDetails(type, method, assemblyName);
                        if (md is null || del is null)
                        {
                            Console.WriteLine($"无法加载方法信息：{assemblyName}-{type}-{method}");
                            continue;
                        }
                        md.MethodTips = flowName + md.MethodTips;
                        if (MethodDelegates.TryAdd(md.MethodName, del))
                        {
                            methodDetails.Add(md);
                        }
                        else
                        {
                            Console.WriteLine($"节点委托创建失败：{md.MethodName}");
                        }
                    }

                    //methodDetails.AddRange(itemMethodDetails);
                }

                var nodeLibrary = new NodeLibrary
                {
                    FullName = assembly.GetName().FullName,
                    Assembly = assembly,
                    FileName = Path.GetFileName(dllPath),
                    FilePath = dllPath,
                };
                //LoadedAssemblies.Add(assembly); // 将加载的程序集添加到列表中
                //LoadedAssemblyPaths.Add(dllPath); // 记录加载的DLL路径
                return (nodeLibrary, autoRegisterTypes, methodDetails);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return (null, [], []);
            }
        }



        /// <summary>
        /// 创建节点
        /// </summary>
        /// <param name="nodeBase"></param>

        private bool TryAddNode(NodeModelBase nodeModel)
        {
            nodeModel.Guid ??= Guid.NewGuid().ToString();
            Nodes[nodeModel.Guid] = nodeModel;

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
        /// 连接节点
        /// </summary>
        /// <param name="fromNode">起始节点</param>
        /// <param name="toNode">目标节点</param>
        /// <param name="connectionType">连接关系</param>
        private async Task<bool> ConnectNodeAsync(NodeModelBase fromNode, NodeModelBase toNode, ConnectionType connectionType)
        {
            if (fromNode is null || toNode is null || fromNode == toNode)
            {
                return false;
            }

            var ToExistOnFrom = true;
            var FromExistInTo = true;
            ConnectionType[] ct = [ConnectionType.IsSucceed,
                                   ConnectionType.IsFail,
                                   ConnectionType.IsError,
                                   ConnectionType.Upstream];

            if (toNode is SingleFlipflopNode flipflopNode)
            {
                flowStarter?.TerminateGlobalFlipflopRuning(flipflopNode); // 假设被连接的是全局触发器，尝试移除
            }

            var isPass = false;
            foreach (ConnectionType ctType in ct)
            {
                var FToTo = fromNode.SuccessorNodes[ctType].Where(it => it.Guid.Equals(toNode.Guid)).ToArray();
                var ToOnF = toNode.PreviousNodes[ctType].Where(it => it.Guid.Equals(fromNode.Guid)).ToArray();
                ToExistOnFrom = FToTo.Length > 0;
                FromExistInTo = ToOnF.Length > 0;
                if (ToExistOnFrom && FromExistInTo)
                {
                    Console.WriteLine("起始节点已与目标节点存在连接");
                    isPass = false;
                }
                else
                {
                    // 检查是否可能存在异常
                    if (!ToExistOnFrom && FromExistInTo)
                    {
                        Console.WriteLine("目标节点不是起始节点的子节点，起始节点却是目标节点的父节点");
                        isPass = false;
                    }
                    else if (ToExistOnFrom && !FromExistInTo)
                    {
                        //
                        Console.WriteLine(" 起始节点不是目标节点的父节点，目标节点却是起始节点的子节点");
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
                fromNode.SuccessorNodes[connectionType].Add(toNode); // 添加到起始节点的子分支
                toNode.PreviousNodes[connectionType].Add(fromNode); // 添加到目标节点的父分支
                if (OperatingSystem.IsWindows())
                {
                    UIContextOperation?.Invoke(() => OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(fromNode.Guid,
                                                                                   toNode.Guid,
                                                                                   connectionType,
                                                                                   NodeConnectChangeEventArgs.ConnectChangeType.Create))); // 通知UI 
                }

                return true;
            }
            else
            {
                return false;
            }


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

        /// <summary>
        /// 输出内容
        /// </summary>
        /// <param name="msg"></param>
        private void Output(string msg)
        {
            if (OperatingSystem.IsWindows())
            {
                UIContextOperation?.Invoke(() => OnEnvOut?.Invoke(msg)); 
            }
            
        }

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
