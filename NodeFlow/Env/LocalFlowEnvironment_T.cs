using Serein.Library;
using Serein.Library.Api;
using Serein.Library.FlowNode;
using Serein.Library.Utils;
using Serein.Library.Utils.SereinExpression;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Model.Node;
using Serein.NodeFlow.Services;
using Serein.NodeFlow.Tool;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Reactive;
using System.Reflection;
using System.Text;

namespace Serein.NodeFlow.Env
{

    /// <summary>
    /// 运行环境
    /// </summary>
    public class LocalFlowEnvironment : IFlowEnvironment/*, IFlowEnvironmentEvent*/
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
        public LocalFlowEnvironment(IFlowEnvironmentEvent flowEnvironmentEvent,
                                    NodeMVVMService nodeMVVMService,
                                    FlowLibraryManagement flowLibraryManagement,
                                    FlowModelService flowModelService,
                                    ISereinIOC sereinIOC)
        {
            this.FlowModelService = flowModelService;
            this.Event = flowEnvironmentEvent;
            this.FlowLibraryManagement = flowLibraryManagement; // 实例化类库管理
            this.NodeMVVMManagement = nodeMVVMService;
            this.FlowEnvironmentIOC = sereinIOC;
            this.IsGlobalInterrupt = false;

            #region 注册基本节点类型
            NodeMVVMManagement.RegisterModel(NodeControlType.UI, typeof(SingleUINode)); // 动作节点
            NodeMVVMManagement.RegisterModel(NodeControlType.Action, typeof(SingleActionNode)); // 动作节点
            NodeMVVMManagement.RegisterModel(NodeControlType.Flipflop, typeof(SingleFlipflopNode)); // 触发器节点
            NodeMVVMManagement.RegisterModel(NodeControlType.ExpOp, typeof(SingleExpOpNode)); // 表达式节点
            NodeMVVMManagement.RegisterModel(NodeControlType.ExpCondition, typeof(SingleConditionNode)); // 条件表达式节点
            NodeMVVMManagement.RegisterModel(NodeControlType.GlobalData, typeof(SingleGlobalDataNode));  // 全局数据节点
            NodeMVVMManagement.RegisterModel(NodeControlType.Script, typeof(SingleScriptNode)); // 脚本节点
            NodeMVVMManagement.RegisterModel(NodeControlType.NetScript, typeof(SingleNetScriptNode)); // 脚本节点
            NodeMVVMManagement.RegisterModel(NodeControlType.FlowCall, typeof(SingleFlowCallNode)); // 流程调用节点
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
        /*/// <summary>
        /// 加载Dll
        /// </summary>
        public event LoadDllHandler? DllLoad;

        /// <summary>
        /// 移除DLL
        /// </summary>
        public event RemoteDllHandler? OnDllRemote;

        /// <summary>
        /// 项目加载完成
        /// </summary>
        public event ProjectLoadedHandler? ProjectLoaded;

        /// <summary>
        /// 项目准备保存
        /// </summary>
        public event ProjectSavingHandler? ProjectSaving;

        /// <summary>
        /// 节点连接属性改变事件
        /// </summary>
        public event NodeConnectChangeHandler? NodeConnectChanged;

        /// <summary>
        /// 节点创建事件
        /// </summary>
        public event NodeCreateHandler? NodeCreated;

        /// <summary>
        /// 移除节点事件
        /// </summary>
        public event NodeRemoveHandler? NodeRemoved;

        /// <summary>
        /// 节点放置事件
        /// </summary>
        public event NodePlaceHandler NodePlace;

        /// <summary>
        /// 节点取出事件
        /// </summary>
        public event NodeTakeOutHandler NodeTakeOut;

        /// <summary>
        /// 起始节点变化事件
        /// </summary>
        public event StartNodeChangeHandler? StartNodeChanged;

        /// <summary>
        /// 流程运行完成事件
        /// </summary>
        public event FlowRunCompleteHandler? FlowRunComplete;

        /// <summary>
        /// 被监视的对象改变事件
        /// </summary>
        public event MonitorObjectChangeHandler? MonitorObjectChanged;

        /// <summary>
        /// 节点中断状态改变事件
        /// </summary>
        public event NodeInterruptStateChangeHandler? NodeInterruptStateChanged;

        /// <summary>
        /// 节点触发了中断
        /// </summary>
        public event ExpInterruptTriggerHandler? InterruptTriggered;

        /// <summary>
        /// 容器改变
        /// </summary>
        public event IOCMembersChangedHandler? IOCMembersChanged;

        /// <summary>
        /// 节点需要定位
        /// </summary>
        public event NodeLocatedHandler? NodeLocated;

        /// <summary>
        /// 运行环境输出
        /// </summary>
        public event EnvOutHandler? EnvOutput;

        /// <summary>
        /// 本地环境添加了画布
        /// </summary>
        public event CanvasCreateHandler CanvasCreated;

        /// <summary>
        /// 本地环境移除了画布
        /// </summary>
        public event CanvasRemoveHandler CanvasRemoved;
*/
        #endregion

        #region 属性
        /// <summary>
        /// 运行环境的IOC容器
        /// </summary>
        public ISereinIOC FlowEnvironmentIOC { get; set; }

        /// <summary>
        /// 当前环境
        /// </summary>
        public IFlowEnvironment CurrentEnv { get => this; }

        /// <summary>
        /// 流程事件
        /// </summary>
        public IFlowEnvironmentEvent Event { get; set; }

        /// <summary>
        /// UI线程操作类
        /// </summary>
        public UIContextOperation UIContextOperation { get; private set; }

        /// <summary>
        /// 节点视图模型管理类
        /// </summary>
        public NodeMVVMService NodeMVVMManagement { get; set; }

        /// <summary>
        /// 节点管理服务（）
        /// </summary>
        internal FlowModelService FlowModelService { get; set; } 

        /// <summary>
        /// 信息输出等级
        /// </summary>
        public InfoClass InfoClass { get; set; } = InfoClass.Trivial;

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
        /// 本地加载的项目文件路径
        /// </summary>
        public string ProjectFileLocation { get; set; } = string.Empty;

        /// <summary>
        /// 是否全局中断
        /// </summary>
        public bool IsGlobalInterrupt { get; set; }

        ///// <summary>
        ///// 流程中断器
        ///// </summary>
        //public ChannelFlowInterrupt ChannelFlowInterrupt { get; set; }

        /// <summary>
        /// <para>单例模式IOC容器，内部维护了一个实例字典，默认使用类型的FullName作为Key，如果以“接口-实现类”的方式注册，那么将使用接口类型的FullName作为Key。</para>
        /// <para>当某个类型注册绑定成功后，将不会因为其它地方尝试注册相同类型的行为导致类型被重新创建。</para>
        /// </summary>
        public ISereinIOC IOC
        {
            get
            {
                if (FlowTaskIOC is null)
                {
                    FlowTaskIOC = new SereinIOC();
                }
                return FlowTaskIOC;
            }
        }

        #endregion

        #region 私有变量

        /// <summary>
        /// IOC容器
        /// </summary>
        private ISereinIOC FlowTaskIOC { get; set; }

        /// <summary>
        /// 通过程序集名称管理动态加载的程序集，用于节点创建提供方法描述，流程运行时提供Emit委托
        /// </summary>
        private readonly FlowLibraryManagement FlowLibraryManagement;

        /// <summary>
        /// IOC对象容器管理
        /// </summary>
        private readonly SereinIOC sereinIOC;

        /// <summary>
        /// 本地运行环境缓存的持久化实例
        /// </summary>
        private Dictionary<Type, object> PersistennceInstance { get; } = new Dictionary<Type, object>();

        /// <summary>
        /// 环境加载的节点集合
        /// Node Guid - Node Model
        /// </summary>
        private Dictionary<string, IFlowNode> NodeModels { get; } = [];

        /// <summary>
        /// 运行环境加载的画布集合
        /// </summary>
        private Dictionary<string, FlowCanvasDetails> FlowCanvass { get; } = [];

        /// <summary>
        /// 存放触发器节点（运行时全部调用）
        /// </summary>
        private List<SingleFlipflopNode> FlipflopNodes { get; } = [];


        /// <summary>
        /// 流程任务管理
        /// </summary>
        private FlowWorkManagement? flowTaskManagement;

        #endregion

        #region 环境对外接口

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
                Event.OnEnvOutput(type, message);
            }
            //Console.WriteLine($"{DateTime.UtcNow} [{type}] : {message}{Environment.NewLine}");

        }

        /// <summary>
        /// 异步运行
        /// </summary>
        /// <returns></returns>
        public async Task<bool> StartFlowAsync(string[] canvasGuids)
        {
            #region 校验参数
            HashSet<string> guids = new HashSet<string>();
            bool isBreak = false;
            foreach (var canvasGuid in canvasGuids)
            {
                if (guids.Contains(canvasGuid))
                {
                    SereinEnv.WriteLine(InfoType.WARN, $"画布重复，停止运行。{canvasGuid}");
                    isBreak = true;
                }
                if (!FlowCanvass.ContainsKey(canvasGuid))
                {
                    SereinEnv.WriteLine(InfoType.WARN, $"画布不存在，停止运行。{canvasGuid}");
                    isBreak = true;
                }
                var count = NodeModels.Values.Count(n => n.CanvasDetails.Guid.Equals(canvasGuid));
                if (count == 0)
                {
                    SereinEnv.WriteLine(InfoType.WARN, $"画布没有节点，停止运行。{canvasGuid}");
                    isBreak = true;
                }
                else
                {
                    guids.Add(canvasGuid);
                }
            }
            if (isBreak)
            {
                guids.Clear();
                return false;
            }
            #endregion


            #region 初始化每个画布的数据，转换为流程任务
            Dictionary<string, FlowTask> flowTasks = [];
            foreach (var guid in guids)
            {
                if (!TryGetCanvasModel(guid, out var canvasModel))
                {
                    SereinEnv.WriteLine(InfoType.WARN, $"画布不存在，停止运行。{guid}");
                    return false;
                }
                var ft = new FlowTask();
                ft.GetNodes = () => NodeModels.Values.Where(node => node.CanvasDetails.Guid.Equals(guid)).ToList();
                if (canvasModel.StartNode?.Guid is null)
                {
                    SereinEnv.WriteLine(InfoType.WARN, $"画布不存在起始节点，将停止运行。{guid}");
                    return false;
                }
                ft.GetStartNode = () => canvasModel.StartNode;
                flowTasks.Add(guid, ft);
            }
            #endregion



            IOC.Reset();
            IOC.Register<IScriptFlowApi, ScriptFlowApi>(); // 注册脚本接口

            var flowTaskOptions = new FlowWorkOptions
            {
                Environment = this, // 流程
                Flows = flowTasks,
                FlowContextPool = new ObjectPool<IDynamicContext>(() => new DynamicContext(this)), // 上下文对象池
                AutoRegisterTypes = this.FlowLibraryManagement.GetaAutoRegisterType(), // 需要自动实例化的类型
                InitMds = this.FlowLibraryManagement.GetMdsOnFlowStart(NodeType.Init),
                LoadMds = this.FlowLibraryManagement.GetMdsOnFlowStart(NodeType.Loading),
                ExitMds = this.FlowLibraryManagement.GetMdsOnFlowStart(NodeType.Exit),
            };



            flowTaskManagement = new FlowWorkManagement(flowTaskOptions);
            var cts = new CancellationTokenSource();
            try
            {
                var t = await flowTaskManagement.RunAsync(cts.Token);
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(ex);
            }
            finally
            {

                SereinEnv.WriteLine(InfoType.INFO, $"流程运行完毕{Environment.NewLine}"); ;
            }
            flowTaskOptions = null;
            return true;


        }


        /// <summary>
        /// 从选定节点开始运行
        /// </summary>
        /// <param name="startNodeGuid"></param>
        /// <returns></returns>
        public async Task<bool> StartFlowFromSelectNodeAsync(string startNodeGuid)
        {

            if (!TryGetNodeModel(startNodeGuid, out var nodeModel) || nodeModel is SingleFlipflopNode)
            {
                return false;
            }
            var context = new DynamicContext(this);
            var cts = new CancellationTokenSource();
            await nodeModel.StartFlowAsync(context, cts.Token);
            cts.Cancel();
            cts.Dispose();
            return true;

           /* if (flowTaskManagement is null)
            {
                SereinEnv.WriteLine(InfoType.ERROR, "没有启动流程，无法运行单个节点");
                return false;
            }
            if (true || FlowState == RunState.Running || FlipFlopState == RunState.Running)
            {
                if (!TryGetNodeModel(startNodeGuid, out var nodeModel) || nodeModel is SingleFlipflopNode)
                {
                    return false;
                }
                var context = new DynamicContext(this);
                var cts = new CancellationTokenSource();
                await nodeModel.StartFlowAsync(context, cts.Token);
                cts.Cancel();
                cts.Dispose();
                return true;
            }
            else
            {
                return false;
            }*/
        }

        /*/// <summary>
        /// 单独运行一个节点
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<object> InvokeNodeAsync(string nodeGuid, IDynamicContext? context = null)
        {
            object result = Unit.Default;
            if (this.NodeModels.TryGetValue(nodeGuid, out var model))
            {
                CancellationTokenSource cts = new CancellationTokenSource();    
                result =  await model.ExecutingAsync(context, cts.Token);
                cts?.Cancel();
            }
            return result;
        }*/

        /// <summary>
        /// 结束流程
        /// </summary>
        public Task<bool> ExitFlowAsync()
        {
            flowTaskManagement?.Exit();
            UIContextOperation?.Invoke(() => Event.OnFlowRunComplete(new FlowEventArgs()));
            IOC.Reset();
            flowTaskManagement = null;
            GC.Collect();
            return Task.FromResult(true);
        }

        /// <summary>
        /// 激活全局触发器
        /// </summary>
        /// <param name="nodeGuid"></param>
        public void ActivateFlipflopNode(string nodeGuid)
        {
            if (!TryGetNodeModel(nodeGuid, out var nodeModel))
            {
                return;
            }
            if (nodeModel is null) return;
            if (flowTaskManagement is not null && nodeModel is SingleFlipflopNode flipflopNode) // 子节点为触发器
            {
                if (FlowState != RunState.Completion
                    && flipflopNode.NotExitPreviousNode()) // 正在运行，且该触发器没有上游节点
                {
                    _ = flowTaskManagement.RunGlobalFlipflopAsync(this, flipflopNode);// 被父节点移除连接关系的子节点若为触发器，且无上级节点，则当前流程正在运行，则加载到运行环境中

                }
            }
        }

        /// <summary>
        /// 关闭全局触发器
        /// </summary>
        /// <param name="nodeGuid"></param>
        public void TerminateFlipflopNode(string nodeGuid)
        {
            if (!TryGetNodeModel(nodeGuid, out var nodeModel))
            {
                return;
            }
            if (nodeModel is null) return;
            if (flowTaskManagement is not null && nodeModel is SingleFlipflopNode flipflopNode) // 子节点为触发器
            {
                flowTaskManagement.TerminateGlobalFlipflopRuning(flipflopNode);
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
        /// 保存项目
        /// </summary>
        public void SaveProject()
        {
            var project = GetProjectInfoAsync().GetAwaiter().GetResult();
            Event.OnProjectSaving(new ProjectSavingEventArgs(project));
        }

        /// <summary>
        /// 加载项目文件
        /// </summary>
        /// <param name="flowEnvInfo">环境信息</param>
        /// <param name="filePath"></param>
        public void LoadProject(FlowEnvInfo flowEnvInfo, string filePath)
        {
            this.ProjectFileLocation = filePath;
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



            _ = Task.Run(async () =>
            {
                // 加载画布
                foreach (var canvasInfo in projectData.Canvass)
                {
                    LoadCanvas(canvasInfo);
                }
                await LoadNodeInfosAsync(projectData.Nodes.ToList()); // 加载节点信息

                // 加载画布
                foreach (var canvasInfo in projectData.Canvass)
                {
                    await SetStartNodeAsync(canvasInfo.Guid, canvasInfo.StartNode); // 设置起始节点
                }
                //await SetStartNodeAsync("", projectData.StartNode); // 设置起始节点
            });

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
                ThemeJsonKey = LocalFlowEnvironment.ThemeKey,
                MsgIdJsonKey = LocalFlowEnvironment.MsgIdKey,
                DataJsonKey = LocalFlowEnvironment.DataKey,
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
        public async Task<SereinProjectData> GetProjectInfoAsync()
        {
            var projectData = new SereinProjectData()
            {
                Librarys = this.FlowLibraryManagement.GetAllLibraryInfo().ToArray(),
                Nodes = NodeModels.Values.Select(node => node.ToInfo()).Where(info => info is not null).ToArray(),
                Canvass = FlowCanvass.Values.Select(canvas => canvas.ToInfo()).ToArray(),
                //StartNode = NodeModels.Values.FirstOrDefault(it => it.IsStart)?.Guid,
            };

            return projectData;
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
                    UIContextOperation?.Invoke(() => Event.OnDllLoad(new LoadDllEventArgs(tmp.Item1, tmp.Item2))); // 通知UI创建dll面板显示
                }

                #endregion

                (var libraryInfo, var mdInfos) = FlowLibraryManagement.LoadLibraryOfPath(dllPath);
                if (mdInfos.Count > 0)
                {
                    UIContextOperation?.Invoke(() => Event.OnDllLoad(new LoadDllEventArgs(libraryInfo, mdInfos))); // 通知UI创建dll面板显示
                }
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(InfoType.ERROR, $"无法加载DLL文件：{ex}");
            }
        }

        /// <summary>
        /// 加载本地程序集
        /// </summary>
        /// <param name="flowLibrary"></param>
        public void LoadLibrary(FlowLibrary flowLibrary)
        {
            try
            {
                (var libraryInfo, var mdInfos) = FlowLibraryManagement.LoadLibraryOfPath(flowLibrary);
                if (mdInfos.Count > 0)
                {
                    UIContextOperation?.Invoke(() => Event.OnDllLoad(new LoadDllEventArgs(libraryInfo, mdInfos))); // 通知UI创建dll面板显示
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
        public bool TryUnloadLibrary(string assemblyName)
        {
            // 获取与此程序集相关的节点
            var groupedNodes = NodeModels.Values.Where(node => !string.IsNullOrWhiteSpace(node.MethodDetails.AssemblyName) && node.MethodDetails.AssemblyName.Equals(assemblyName)).ToArray();
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
                    IFlowNode? node = groupedNodes[i];
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

        private int _addCanvasCount = 0;

        /// <summary>
        /// 增加画布
        /// </summary>
        /// <param name="canvasName">画布名称</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <returns></returns>
        public async Task<FlowCanvasDetailsInfo> CreateCanvasAsync(string canvasName, int width, int height)
        {
            var info = new FlowCanvasDetailsInfo()
            {
                Guid = Guid.NewGuid().ToString(),
                Height = height,
                Width = width,
                ViewX = 0,
                ViewY = 0,
                ScaleY = 1,
                ScaleX = 1,
                Name = !string.IsNullOrWhiteSpace(canvasName) ? canvasName : $"流程图{_addCanvasCount++}",
            };
            var model = LoadCanvas(info);
            return info;
        }

        private FlowCanvasDetails LoadCanvas(FlowCanvasDetailsInfo info)
        {
            var model = new FlowCanvasDetails(this);
            model.LoadInfo(info);
            FlowCanvass.Add(model.Guid, model);
            UIContextOperation?.Invoke(() =>
            {
                Event.OnCanvasCreated(new CanvasCreateEventArgs(model));
            });
            return model;
        }

        /// <summary>
        /// 删除画布
        /// </summary>
        /// <param name="canvasGuid">画布Guid</param>
        /// <returns></returns>
        public async Task<bool> RemoveCanvasAsync(string canvasGuid)
        {

            if (!FlowCanvass.TryGetValue(canvasGuid, out var model))
            {
                return false;
            }
            var count = NodeModels.Values.Count(node => node.CanvasDetails.Guid.Equals(canvasGuid));
            if (count > 0)
            {
                SereinEnv.WriteLine(InfoType.WARN, "无法删除具有节点的画布");
                return false;
            }
            if (FlowCanvass.Remove(canvasGuid))
            {
                UIContextOperation?.Invoke(() =>
                {
                    Event.OnCanvasRemoved(new CanvasRemoveEventArgs(canvasGuid));
                });
                return true;
            }

            return false;
        }


        /// <summary>
        /// 从节点信息集合批量加载节点控件
        /// </summary>
        /// <param name="List<NodeInfo>">节点信息</param>
        /// <returns></returns>
        /// 
        public async Task LoadNodeInfosAsync(List<NodeInfo> nodeInfos)
        {
            #region 从NodeInfo创建NodeModel
            // 流程接口节点最后才创建

            List<NodeInfo> flowCallNodeInfos = [];
            foreach (NodeInfo? nodeInfo in nodeInfos)
            {
                if (nodeInfo.Type == nameof(NodeControlType.FlowCall))
                {
                    flowCallNodeInfos.Add(nodeInfo);
                }
                else
                {
                    if (!CreateNodeFromNodeInfo(nodeInfo))
                    {
                        SereinEnv.WriteLine(InfoType.WARN, $"节点创建失败。{Environment.NewLine}{nodeInfo}");
                        continue;
                    }
                }
            }

            // 创建流程接口节点
            foreach (NodeInfo? nodeInfo in flowCallNodeInfos)
            {
                if (!CreateNodeFromNodeInfo(nodeInfo))
                {
                    SereinEnv.WriteLine(InfoType.WARN, $"节点创建失败。{Environment.NewLine}{nodeInfo}");
                    continue;
                }
            }
            #endregion

            #region 重新放置节点

            List<NodeInfo> needPlaceNodeInfos = [];
            foreach (NodeInfo? nodeInfo in nodeInfos)
            {
                if (!string.IsNullOrEmpty(nodeInfo.ParentNodeGuid) &&
                    NodeModels.TryGetValue(nodeInfo.ParentNodeGuid, out var parentNode))
                {
                    needPlaceNodeInfos.Add(nodeInfo); // 需要重新放置的节点
                }
            }

            foreach (NodeInfo nodeInfo in needPlaceNodeInfos)
            {
                if (NodeModels.TryGetValue(nodeInfo.Guid, out var nodeModel) &&
                    NodeModels.TryGetValue(nodeInfo.ParentNodeGuid, out var containerNode)
                    && containerNode is INodeContainer nodeContainer)
                {
                    var result = nodeContainer.PlaceNode(nodeModel);
                    if (result)
                    {
                        UIContextOperation?.Invoke(() => Event.OnNodePlace(
                           new NodePlaceEventArgs(nodeInfo.CanvasGuid, nodeModel.Guid, containerNode.Guid)));
                    }


                }
            }
            #endregion

            #region 确定节点之间的方法调用关系
            foreach (var nodeInfo in nodeInfos)
            {
                var canvasGuid = nodeInfo.CanvasGuid;
                if (!TryGetNodeModel(nodeInfo.Guid, out var fromNodeModel))
                {
                    return;
                }
                if (fromNodeModel is null) continue;
                List<(ConnectionInvokeType connectionType, string[] guids)> allToNodes = [(ConnectionInvokeType.IsSucceed,nodeInfo.TrueNodes),
                                                                                    (ConnectionInvokeType.IsFail,   nodeInfo.FalseNodes),
                                                                                    (ConnectionInvokeType.IsError,  nodeInfo.ErrorNodes),
                                                                                    (ConnectionInvokeType.Upstream, nodeInfo.UpstreamNodes)];
                foreach ((ConnectionInvokeType connectionType, string[] toNodeGuids) item in allToNodes)
                {
                    // 遍历当前类型分支的节点（确认连接关系）
                    foreach (var toNodeGuid in item.toNodeGuids)
                    {
                        if (!TryGetNodeModel(toNodeGuid, out var toNodeModel))
                        {
                            return;
                        }
                        if (toNodeModel is null)
                        {
                            // 防御性代码，加载正常保存的项目文件不会进入这里
                            continue;
                        }
                        ;
                        var isSuccessful = ConnectInvokeOfNode(canvasGuid, fromNodeModel, toNodeModel, item.connectionType); // 加载时确定节点间的连接关系
                    }
                }


                //List<(ConnectionInvokeType connectionType, string[] guids)> allToNodes = [(ConnectionInvokeType.IsSucceed,nodeInfo.TrueNodes),
                //                                                                (ConnectionInvokeType.IsFail,   nodeInfo.FalseNodes),
                //                                                                (ConnectionInvokeType.IsError,  nodeInfo.ErrorNodes),
                //                                                                (ConnectionInvokeType.Upstream, nodeInfo.UpstreamNodes)];

                //List<(ConnectionInvokeType, NodeModelBase[])> fromNodes = allToNodes.Where(info => info.guids.Length > 0)
                //                                                     .Select(info => (info.connectionType,
                //                                                                      info.guids.Where(guid => NodeModels.ContainsKey(guid)).Select(guid => NodeModels[guid])
                //                                                                        .ToArray()))
                //                                                     .ToList();
                // 遍历每种类型的节点分支（四种）
                //foreach ((ConnectionInvokeType connectionType, NodeModelBase[] toNodes) item in nodeInfo)
                //{
                //    // 遍历当前类型分支的节点（确认连接关系）
                //    foreach (var toNode in item.toNodes)
                //    {
                //        _ = ConnectInvokeOfNode(fromNode, toNode, item.connectionType); // 加载时确定节点间的连接关系
                //    }
                //}
            }
            #endregion

            #region 确定节点之间的参数调用关系
            foreach (var toNode in NodeModels.Values)
            {
                var canvasGuid = toNode.CanvasDetails.Guid;
                if (toNode.MethodDetails.ParameterDetailss == null)
                {
                    continue;
                }
                for (var i = 0; i < toNode.MethodDetails.ParameterDetailss.Length; i++)
                {
                    var pd = toNode.MethodDetails.ParameterDetailss[i];
                    if (!string.IsNullOrEmpty(pd.ArgDataSourceNodeGuid)
                        && NodeModels.TryGetValue(pd.ArgDataSourceNodeGuid, out var fromNode))
                    {

                        await ConnectArgSourceOfNodeAsync(canvasGuid, fromNode, toNode, pd.ArgDataSourceType, pd.Index);
                    }
                }
            }
            #endregion


            UIContextOperation?.Invoke(() =>
            {
                Event.OnProjectLoaded(new ProjectLoadedEventArgs());
            });

            return;
        }

        /// <summary>
        /// 流程正在运行时创建节点
        /// </summary>
        /// <param name="canvasGuid">所属画布</param>
        /// <param name="nodeControlType">所属类型</param>
        /// <param name="position">所处位置</param>
        /// <param name="methodDetailsInfo">如果是表达式节点条件节点，该项为null</param>
        public Task<NodeInfo> CreateNodeAsync(string canvasGuid,
                                              NodeControlType nodeControlType,
                                              PositionOfUI position,
                                              MethodDetailsInfo? methodDetailsInfo = null)
        {
            if (!TryGetCanvasModel(canvasGuid, out var canvasModel))
            {
                return Task.FromResult<NodeInfo>(null);
            }
            IFlowNode? nodeModel;
            if (methodDetailsInfo is null
                || string.IsNullOrEmpty(methodDetailsInfo.AssemblyName)
                || string.IsNullOrEmpty(methodDetailsInfo.MethodName))
            {
                nodeModel = FlowNodeExtension.CreateNode(this, nodeControlType); // 加载基础节点
            }
            else
            {
                if (FlowLibraryManagement.TryGetMethodDetails(methodDetailsInfo.AssemblyName,  // 创建节点
                                                              methodDetailsInfo.MethodName,
                                                              out var methodDetails))
                {
                    nodeModel = FlowNodeExtension.CreateNode(this, nodeControlType, methodDetails); // 一般的加载节点方法
                }
                else
                {
                    return Task.FromResult<NodeInfo>(null);
                }
            }
            nodeModel.CanvasDetails = canvasModel;
            canvasModel.Nodes.Add(nodeModel); // 节点与画布互相绑定
            TryAddNode(nodeModel);
            nodeModel.Position = position; // 设置位置

            // 通知UI更改
            UIContextOperation?.Invoke(() => Event.OnNodeCreated(new NodeCreateEventArgs(canvasGuid, nodeModel, position)));
            var nodeInfo = nodeModel.ToInfo();
            return Task.FromResult(nodeInfo);
        }


        /// <summary>
        /// 将节点放置在容器中
        /// </summary>
        /// <returns></returns>
        public Task<bool> PlaceNodeToContainerAsync(string canvasGuid,
                                              string nodeGuid, string containerNodeGuid)
        {
            if (!FlowCanvass.ContainsKey(canvasGuid))
            {
                return Task.FromResult<bool>(false);
            }
            // 获取目标节点与容器节点
            if (!TryGetNodeModel(nodeGuid, out var nodeModel))
            {
                return Task.FromResult(false);
            }
            if (nodeModel.ContainerNode is INodeContainer tmpContainer)
            {
                SereinEnv.WriteLine(InfoType.WARN, $"节点放置失败，节点[{nodeGuid}]已经放置于容器节点[{((IFlowNode)tmpContainer).Guid}]");
                return Task.FromResult(false);
            }

            if (!TryGetNodeModel(containerNodeGuid, out var containerNode))
            {
                return Task.FromResult(false);
            }
            if (containerNode is not INodeContainer nodeContainer) return Task.FromResult(false);

            var result = nodeContainer.PlaceNode(nodeModel); // 放置在容器节点
            if (result)
            {
                UIContextOperation?.Invoke(() =>
                {
                    Event.OnNodePlace(new NodePlaceEventArgs(canvasGuid, nodeGuid, containerNodeGuid)); // 通知UI更改节点放置位置
                });
            }
            return Task.FromResult(result);

        }

        /// <summary>
        /// 将节点从容器节点中脱离
        /// </summary>
        /// <returns></returns>
        public Task<bool> TakeOutNodeToContainerAsync(string canvasGuid,
                                              string nodeGuid)
        {
            if (!FlowCanvass.ContainsKey(canvasGuid))
            {
                return Task.FromResult<bool>(false);
            }
            // 获取目标节点与容器节点
            if (!TryGetNodeModel(nodeGuid, out var nodeModel))
            {
                return Task.FromResult(false);
            }
            if (nodeModel.ContainerNode is not INodeContainer nodeContainer)
            {
                return Task.FromResult(false);
            }
            var result = nodeContainer.TakeOutNode(nodeModel); // 从容器节点取出
            if (result)
            {
                UIContextOperation?.Invoke(() =>
                {
                    Event.OnNodeTakeOut(new NodeTakeOutEventArgs(canvasGuid, nodeGuid)); // 重新放置在画布上
                });
            }
            return Task.FromResult(result);


        }



        /// <summary>
        /// 移除节点
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<bool> RemoveNodeAsync(string canvasGuid, string nodeGuid)
        {
            if (!TryGetCanvasModel(canvasGuid, out var canvasModel))
            {
                return false;
            }
            if (!TryGetNodeModel(nodeGuid, out var remoteNode))
            {
                return false;
            }

            if (remoteNode is SingleFlipflopNode flipflopNode)
            {
                flowTaskManagement?.TerminateGlobalFlipflopRuning(flipflopNode); // 假设被移除的是全局触发器，尝试从启动器移除
            }

            //remoteNode.Remove(); // 调用节点的移除方法

            // 遍历所有前置节点，从那些前置节点中的后继节点集合移除该节点
            foreach (var pnc in remoteNode.PreviousNodes)
            {
                var pCType = pnc.Key; // 连接类型
                for (int i = 0; i < pnc.Value.Count; i++)
                {
                    IFlowNode? pNode = pnc.Value[i];
                    pNode.SuccessorNodes[pCType].Remove(remoteNode);

                    UIContextOperation?.Invoke(() => Event.OnNodeConnectChanged(new NodeConnectChangeEventArgs(
                                                                    canvasGuid,
                                                                    pNode.Guid,
                                                                    remoteNode.Guid,
                                                                    JunctionOfConnectionType.Invoke,
                                                                    pCType, // 对应的连接关系
                                                                    NodeConnectChangeEventArgs.ConnectChangeType.Remove))); // 通知UI

                }
            }


            if (remoteNode.ControlType == NodeControlType.FlowCall)
            {

            }
            else
            {
                // 遍历所有后继节点，从那些后继节点中的前置节点集合移除该节点
                foreach (var snc in remoteNode.SuccessorNodes)
                {
                    var connectionType = snc.Key; // 连接类型
                    for (int i = 0; i < snc.Value.Count; i++)
                    {
                        IFlowNode? toNode = snc.Value[i];

                        await RemoteConnectAsync(canvasGuid, remoteNode, toNode, connectionType);

                    }
                }

            }



            // 从集合中移除节点，解除与画布的绑定关系
            NodeModels.Remove(nodeGuid);
            UIContextOperation?.Invoke(() => canvasModel.Nodes.Remove(remoteNode));

            UIContextOperation?.Invoke(() => Event.OnNodeRemoved(new NodeRemoveEventArgs(canvasGuid, nodeGuid)));
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
        public Task<bool> ConnectInvokeNodeAsync(string canvasGuid,
                                                 string fromNodeGuid,
                                                 string toNodeGuid,
                                                 JunctionType fromNodeJunctionType,
                                                 JunctionType toNodeJunctionType,
                                                 ConnectionInvokeType invokeType)
        {

            // 获取起始节点与目标节点
            if (!FlowCanvass.ContainsKey(canvasGuid) || !TryGetNodeModel(fromNodeGuid, out var fromNode) || !TryGetNodeModel(toNodeGuid, out var toNode))
            {
                return Task.FromResult(false);
            }

            if (fromNode is null || toNode is null) return Task.FromResult(false);
            (var type, var state) = CheckConnect(fromNode, toNode, fromNodeJunctionType, toNodeJunctionType);
            if (!state)
            {
                SereinEnv.WriteLine(InfoType.WARN, "出现非预期的连接行为");
                return Task.FromResult(false); // 出现不符预期的连接行为，忽略此次连接行为
            }

            if (type == JunctionOfConnectionType.Invoke)
            {
                if (fromNodeJunctionType == JunctionType.Execute)
                {
                    // 如果 起始控制点 是“方法调用”，需要反转 from to 节点
                    (fromNode, toNode) = (toNode, fromNode);
                }
                // 从起始节点“下一个方法”控制点，连接到目标节点“方法调用”控制点
                state = ConnectInvokeOfNode(canvasGuid, fromNode, toNode, invokeType); // 本地环境进行连接
            }
            return Task.FromResult(state);

        }


        /// <summary>
        /// 设置两个节点某个类型的方法调用关系为优先调用
        /// </summary>
        /// <param name="fromNodeGuid">起始节点</param>
        /// <param name="toNodeGuid">目标节点</param>
        /// <param name="connectionType">连接关系</param>
        /// <returns>是否成功调用</returns>
        public Task<bool> SetConnectPriorityInvoke(string fromNodeGuid, string toNodeGuid, ConnectionInvokeType connectionType)
        {
            // 获取起始节点与目标节点
            if (!TryGetNodeModel(fromNodeGuid, out var fromNode) || !TryGetNodeModel(toNodeGuid, out var toNode))
            {
                return Task.FromResult(false);
            }
            if (fromNode is null || toNode is null) return Task.FromResult(false);
            if (fromNode.SuccessorNodes.TryGetValue(connectionType, out var nodes))
            {
                var idx = nodes.IndexOf(toNode);
                if (idx > -1)
                {
                    nodes.RemoveAt(idx);
                    nodes.Insert(0, toNode);
                    return Task.FromResult(true);
                }
            }
            return Task.FromResult(false);
        }

        /// <summary>
        /// 移除连接节点之间方法调用的关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点Guid</param>
        /// <param name="toNodeGuid">目标节点Guid</param>
        /// <param name="connectionType">连接关系</param>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<bool> RemoveConnectInvokeAsync(string canvasGuid, string fromNodeGuid, string toNodeGuid, ConnectionInvokeType connectionType)
        {
            // 获取起始节点与目标节点
            if (!FlowCanvass.ContainsKey(canvasGuid) || !TryGetNodeModel(fromNodeGuid, out var fromNode) || !TryGetNodeModel(toNodeGuid, out var toNode))
            {
                return false;
            }
            if (fromNode is null || toNode is null) return false;

            var result = await RemoteConnectAsync(canvasGuid, fromNode, toNode, connectionType);
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
        public async Task<bool> ConnectArgSourceNodeAsync(string canvasGuid,
                                                string fromNodeGuid,
                                                 string toNodeGuid,
                                                 JunctionType fromNodeJunctionType,
                                                 JunctionType toNodeJunctionType,
                                                 ConnectionArgSourceType connectionArgSourceType,
                                                 int argIndex)
        {

            // 获取起始节点与目标节点
            if (!FlowCanvass.ContainsKey(canvasGuid) || !TryGetNodeModel(fromNodeGuid, out var fromNode) || !TryGetNodeModel(toNodeGuid, out var toNode))
            {
                return false;
            }
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
                state = await ConnectArgSourceOfNodeAsync(canvasGuid, fromNode, toNode, connectionArgSourceType, argIndex);  // 本地环境进行连接
            }
            return state;

        }


        /// <summary>
        /// 移除连接节点之间参数传递的关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点Guid</param>
        /// <param name="toNodeGuid">目标节点Guid</param>
        /// <param name="argIndex">连接到第几个参数</param>
        public async Task<bool> RemoveConnectArgSourceAsync(string canvasGuid, string fromNodeGuid, string toNodeGuid, int argIndex)
        {
            // 获取起始节点与目标节点
            if (!FlowCanvass.ContainsKey(canvasGuid) || !TryGetNodeModel(fromNodeGuid, out var fromNode) || !TryGetNodeModel(toNodeGuid, out var toNode))
            {
                return false;
            }
            if (fromNode is null || toNode is null) return false;
            var result = await RemoteConnectAsync(canvasGuid, fromNode, toNode, argIndex);
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
        /// 设置在UI线程操作的线程上下文
        /// </summary>
        /// <param name="uiContextOperation"></param>
        public void SetUIContextOperation(UIContextOperation uiContextOperation)
        {
            if (uiContextOperation is not null)
            {
                this.UIContextOperation = uiContextOperation;
            }
        }

        /// <inheritdoc/>
        public void UseExternalIOC(ISereinIOC ioc)
        {
            this.FlowTaskIOC = ioc; // 设置IOC容器
        }


        /// <summary>
        /// 设置起点控件
        /// </summary>
        /// <param name="canvasGuid">画布</param>
        /// <param name="newNodeGuid">节点Guid</param>
        public Task<string> SetStartNodeAsync(string canvasGuid, string newNodeGuid)
        {
            if (!TryGetCanvasModel(canvasGuid, out var canvasModel) || !TryGetNodeModel(newNodeGuid, out var newStartNodeModel))
            {
                return Task.FromResult(string.Empty);
            }
            SetStartNode(canvasModel, newStartNodeModel);
            return Task.FromResult(canvasModel.StartNode.Guid ?? string.Empty);
        }

        /// <summary>
        /// 启动器调用，运行到某个节点时触发了监视对象的更新（对象预览视图将会自动更新）
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="monitorData"></param>
        /// <param name="sourceType"></param>
        public void MonitorObjectNotification(string nodeGuid, object monitorData, MonitorObjectEventArgs.ObjSourceType sourceType)
        {
            Event.OnMonitorObjectChanged(new MonitorObjectEventArgs(nodeGuid, monitorData, sourceType));
        }

        /// <summary>
        /// 启动器调用，节点触发了中断。
        /// </summary>
        /// <param name="nodeGuid">节点</param>
        /// <param name="expression">表达式</param>
        /// <param name="type">类型，0用户主动的中断，1表达式中断</param>
        public void TriggerInterrupt(string nodeGuid, string expression, InterruptTriggerEventArgs.InterruptTriggerType type)
        {
            Event.OnInterruptTriggered(new InterruptTriggerEventArgs(nodeGuid, expression, type));
        }


        ///// <summary>
        ///// 环境执行中断
        ///// </summary>
        ///// <returns></returns>
        //public async Task InterruptNode()
        //{
        //    IsGlobalInterrupt = true;
        //    var result = await ChannelFlowInterrupt.GetOrCreateChannelAsync(EnvName);
        //    return result;
        //}

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
        public Task NotificationNodeValueChangeAsync(string nodeGuid, string path, object value)
        {
            // "NodeModel.Path"
            if (TryGetNodeModel(nodeGuid, out var nodeModel))
            {
                SerinExpressionEvaluator.Evaluate($"@Set .{path} = {value}", nodeModel, out _); // 更改对应的数据
            }


            return Task.CompletedTask;
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
        public Task<bool> ChangeParameter(string nodeGuid, bool isAdd, int paramIndex)
        {
            if (!TryGetNodeModel(nodeGuid, out var nodeModel))
            {
                return Task.FromResult(false);
            }
            if (nodeModel is null) return Task.FromResult(false);
            bool isPass;
            if (isAdd)
            {
                isPass = nodeModel.MethodDetails.AddParamsArg(paramIndex);
            }
            else
            {
                isPass = nodeModel.MethodDetails.RemoveParamsArg(paramIndex);
            }
            return Task.FromResult(isPass);
        }


        /// <summary>
        /// 从Guid获取画布
        /// </summary>
        /// <param name="nodeGuid">节点Guid</param>
        /// <returns>节点Model</returns>
        /// <exception cref="ArgumentNullException">无法获取节点、Guid/节点为null时报错</exception>
        public bool TryGetCanvasModel(string nodeGuid, out FlowCanvasDetails canvasDetails)
        {
            if (string.IsNullOrEmpty(nodeGuid))
            {
                canvasDetails = null;
                return false;
            }
            return FlowCanvass.TryGetValue(nodeGuid, out canvasDetails) && canvasDetails is not null;

        }

        /// <summary>
        /// 从Guid获取节点
        /// </summary>
        /// <param name="nodeGuid">节点Guid</param>
        /// <returns>节点Model</returns>
        /// <exception cref="ArgumentNullException">无法获取节点、Guid/节点为null时报错</exception>
        public bool TryGetNodeModel(string nodeGuid, out IFlowNode nodeModel)
        {
            if (string.IsNullOrEmpty(nodeGuid))
            {
                nodeModel = null;
                return false;
            }
            return NodeModels.TryGetValue(nodeGuid, out nodeModel) && nodeModel is not null;

        }

        #endregion

        #region 流程依赖类库的接口


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
            NativeDllHelper.LoadAllDll(path);
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
        /// 从节点信息创建节点，并返回状态指示是否创建成功
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        private bool CreateNodeFromNodeInfo(NodeInfo nodeInfo)
        {
            if (!EnumHelper.TryConvertEnum<NodeControlType>(nodeInfo.Type, out var controlType))
            {
                return false;
            }

            #region 获取方法描述
            MethodDetails? methodDetails;
            if (controlType == NodeControlType.FlowCall)
            {
                if (string.IsNullOrEmpty(nodeInfo.MethodName))
                {
                    methodDetails = new MethodDetails();
                    methodDetails.ParamsArgIndex = 0;
                    methodDetails.ParameterDetailss = new ParameterDetails[nodeInfo.ParameterData.Length];
                    for (int i = 0; i < methodDetails.ParameterDetailss.Length; i++)
                    {
                        var pdInfo = nodeInfo.ParameterData[i];
                        var t = new ParameterDetailsInfo();
                        var pd = new ParameterDetails(pdInfo, i);
                        methodDetails.ParameterDetailss[i] = pd;
                    }
                }
                else
                {
                    // 目标节点可能是方法节点
                    FlowLibraryManagement.TryGetMethodDetails(nodeInfo.AssemblyName, nodeInfo.MethodName, out methodDetails); // 加载项目时尝试获取方法信息
                }
            }
            else if (controlType.IsBaseNode())
            {
                // 加载基础节点
                methodDetails = new MethodDetails();

            }
            else
            {
                if (string.IsNullOrEmpty(nodeInfo.MethodName)) return false;
                // 加载方法节点
                FlowLibraryManagement.TryGetMethodDetails(nodeInfo.AssemblyName, nodeInfo.MethodName, out methodDetails); // 加载项目时尝试获取方法信息
            }
            #endregion

            var nodeModel = FlowNodeExtension.CreateNode(this, controlType, methodDetails); // 加载项目时创建节点
            if (nodeModel is null)
            {
                nodeInfo.Guid = string.Empty;
                return false;
            }
            if (FlowCanvass.TryGetValue(nodeInfo.CanvasGuid, out var canvasModel))
            {

                // 节点与画布互相绑定
                // 需要在UI线程上进行添加，否则会报 “不支持从调度程序线程以外的线程对其 SourceCollection 进行的更改”异常
                nodeModel.CanvasDetails = canvasModel;
                UIContextOperation?.Invoke(() => canvasModel.Nodes.Add(nodeModel));

                nodeModel.LoadInfo(nodeInfo); // 创建节点model
                TryAddNode(nodeModel); // 加载项目时将节点加载到环境中
            }
            else
            {
                SereinEnv.WriteLine(InfoType.ERROR, $"加载节点[{nodeInfo.Guid}]时发生异常，画布[{nodeInfo.CanvasGuid}]不存在");
                return false;
            }

            UIContextOperation?.Invoke(() =>
                Event.OnNodeCreated(new NodeCreateEventArgs(nodeInfo.CanvasGuid, nodeModel, nodeInfo.Position))); // 添加到UI上
            return true;
        }


        /// <summary>
        /// 移除连接关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点Model</param>
        /// <param name="toNodeGuid">目标节点Model</param>
        /// <param name="connectionType">连接关系</param>
        /// <exception cref="NotImplementedException"></exception>
        private async Task<bool> RemoteConnectAsync(string canvasGuid, IFlowNode fromNode, IFlowNode toNode, ConnectionInvokeType connectionType)
        {
            if (!FlowCanvass.ContainsKey(canvasGuid))
            {
                return false;
            }
            fromNode.SuccessorNodes[connectionType].Remove(toNode);
            toNode.PreviousNodes[connectionType].Remove(fromNode);


            if (OperatingSystem.IsWindows())
            {
                UIContextOperation?.Invoke(() => Event.OnNodeConnectChanged(
                    new NodeConnectChangeEventArgs(
                        canvasGuid,
                        fromNode.Guid,
                        toNode.Guid,
                        JunctionOfConnectionType.Invoke,
                        connectionType,
                        NodeConnectChangeEventArgs.ConnectChangeType.Remove)));
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
        private async Task<bool> RemoteConnectAsync(string canvasGuid, IFlowNode fromNode, IFlowNode toNode, int argIndex)
        {
            if (!FlowCanvass.ContainsKey(canvasGuid))
            {
                return false;
            }
            if (string.IsNullOrEmpty(toNode.MethodDetails.ParameterDetailss[argIndex].ArgDataSourceNodeGuid))
            {
                return false;
            }
            toNode.MethodDetails.ParameterDetailss[argIndex].ArgDataSourceNodeGuid = null;
            toNode.MethodDetails.ParameterDetailss[argIndex].ArgDataSourceType = ConnectionArgSourceType.GetPreviousNodeData; // 恢复默认值

            if (OperatingSystem.IsWindows())
            {
                UIContextOperation?.Invoke(() => Event.OnNodeConnectChanged(
                    new NodeConnectChangeEventArgs(
                        canvasGuid,
                        fromNode.Guid,
                        toNode.Guid,
                        argIndex,
                        JunctionOfConnectionType.Arg,
                        ConnectionArgSourceType.GetPreviousNodeData,
                        NodeConnectChangeEventArgs.ConnectChangeType.Remove)));
            }
            return true;
        }


        /// <summary>
        /// 创建节点
        /// </summary>
        /// <param name="nodeBase"></param>
        private bool TryAddNode(IFlowNode nodeModel)
        {
            nodeModel.Guid ??= Guid.NewGuid().ToString();
            NodeModels.TryAdd(nodeModel.Guid, nodeModel);


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
        public static (JunctionOfConnectionType, bool) CheckConnect(IFlowNode fromNode,
                                                                    IFlowNode toNode,
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
                if (toNodeJunctionType == JunctionType.ReturnData && !fromNode.Guid.Equals(toNode.Guid))
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
            return (type, state);
        }

        /// <summary>
        /// 连接节点
        /// </summary>
        /// <param name="fromNode">起始节点</param>
        /// <param name="toNode">目标节点</param>
        /// <param name="invokeType">连接关系</param>
        private bool ConnectInvokeOfNode(string canvasGuid, IFlowNode fromNode, IFlowNode toNode, ConnectionInvokeType invokeType)
        {
            if (fromNode.ControlType == NodeControlType.FlowCall)
            {
                SereinEnv.WriteLine(InfoType.ERROR, $"流程接口节点不可调用下一个节点。" +
                          $"{Environment.NewLine}流程节点:{fromNode.Guid}");
                return false;
            }
            if (!FlowCanvass.ContainsKey(canvasGuid))
            {
                return false;
            }
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
                flowTaskManagement?.TerminateGlobalFlipflopRuning(flipflopNode); // 假设被连接的是全局触发器，尝试移除
            }
            var isOverwriting = false;
            ConnectionInvokeType overwritingCt = ConnectionInvokeType.None;
            var isPass = false;
            foreach (ConnectionInvokeType ctType in ct)
            {
                var FToTo = fromNode.SuccessorNodes[ctType].Where(it => it.Guid.Equals(toNode.Guid)).ToArray();
                var ToOnF = toNode.PreviousNodes[ctType].Where(it => it.Guid.Equals(fromNode.Guid)).ToArray();
                ToExistOnFrom = FToTo.Length > 0;
                FromExistInTo = ToOnF.Length > 0;
                if (ToExistOnFrom && FromExistInTo)
                {
                    if (ctType == invokeType)
                    {
                        SereinEnv.WriteLine(InfoType.WARN, $"起始节点已与目标节点存在连接。" +
                           $"{Environment.NewLine}起始节点:{fromNode.Guid}" +
                           $"{Environment.NewLine}目标节点:{toNode.Guid}");
                        return false;
                    }
                    isOverwriting = true;
                    overwritingCt = ctType;
                }
                else
                {
                    // 检查是否可能存在异常
                    if (!ToExistOnFrom && FromExistInTo)
                    {
                        SereinEnv.WriteLine(InfoType.ERROR, $"起始节点不是目标节点的父节点，目标节点却是起始节点的子节点。" +
                            $"{Environment.NewLine}起始节点:{fromNode.Guid}" +
                            $"{Environment.NewLine}目标节点:{toNode.Guid}");
                        isPass = false;
                    }
                    else if (ToExistOnFrom && !FromExistInTo)
                    {
                        //
                        SereinEnv.WriteLine(InfoType.ERROR, $"起始节点不是目标节点的父节点，目标节点却是起始节点的子节点。" +
                            $"{Environment.NewLine}起始节点:{fromNode.Guid}" +
                            $"{Environment.NewLine}目标节点:{toNode.Guid}" +
                            $"");
                        isPass = false;
                    }
                    else
                    {
                        isPass = true;
                    }
                }
            }
            if (isPass)
            {
                if (isOverwriting) // 需要替换
                {
                    fromNode.SuccessorNodes[overwritingCt].Remove(toNode); // 从起始节点子分支中移除
                    toNode.PreviousNodes[overwritingCt].Remove(fromNode); // 从目标节点父分支中移除
                }
                fromNode.SuccessorNodes[invokeType].Add(toNode); // 添加到起始节点的子分支
                toNode.PreviousNodes[invokeType].Add(fromNode); // 添加到目标节点的父分支
                if (OperatingSystem.IsWindows())
                {

                    UIContextOperation?.Invoke(() =>
                        Event.OnNodeConnectChanged(
                                new NodeConnectChangeEventArgs(
                                     canvasGuid,
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
        private async Task<bool> ConnectArgSourceOfNodeAsync(string canvasGuid,
                                                            IFlowNode fromNode,
                                                             IFlowNode toNode,
                                                             ConnectionArgSourceType connectionArgSourceType,
                                                             int argIndex)
        {
            if (!FlowCanvass.ContainsKey(canvasGuid))
            {
                return false;
            }

            var toNodeArgSourceGuid = toNode.MethodDetails.ParameterDetailss[argIndex].ArgDataSourceNodeGuid;
            var toNodeArgSourceType = toNode.MethodDetails.ParameterDetailss[argIndex].ArgDataSourceType;
            if (fromNode.Guid == toNodeArgSourceGuid && toNodeArgSourceType == connectionArgSourceType)
            {
                SereinEnv.WriteLine(InfoType.INFO, $"节点之间已建立过连接关系，此次操作将不会执行" +
                    $"起始节点：{fromNode.Guid}" +
                    $"目标节点：{toNode.Guid}" +
                    $"参数索引：{argIndex}" +
                    $"参数类型：{connectionArgSourceType}");
                UIContextOperation?.Invoke(() =>
                        Event.OnNodeConnectChanged(
                                new NodeConnectChangeEventArgs(
                                    canvasGuid,
                                    fromNode.Guid, // 从哪个节点开始
                                    toNode.Guid, // 连接到那个节点
                                    argIndex, // 连接线的样式类型
                                    JunctionOfConnectionType.Arg,
                                    connectionArgSourceType,
                                    NodeConnectChangeEventArgs.ConnectChangeType.Create // 是创建连接还是删除连接
                                ))); // 通知UI 

                return true;
            }

            if (!string.IsNullOrEmpty(toNodeArgSourceGuid))
            {
                await RemoteConnectAsync(canvasGuid, fromNode, toNode, argIndex);
            }

            toNode.MethodDetails.ParameterDetailss[argIndex].ArgDataSourceNodeGuid = fromNode.Guid;
            toNode.MethodDetails.ParameterDetailss[argIndex].ArgDataSourceType = connectionArgSourceType;

            UIContextOperation?.Invoke(() =>
                        Event.OnNodeConnectChanged(
                                new NodeConnectChangeEventArgs(
                                    canvasGuid,
                                    fromNode.Guid, // 从哪个节点开始
                                    toNode.Guid, // 连接到那个节点
                                    argIndex, // 连接线的样式类型
                                    JunctionOfConnectionType.Arg,
                                    connectionArgSourceType,
                                    NodeConnectChangeEventArgs.ConnectChangeType.Create // 是创建连接还是删除连接
                                ))); // 通知UI 
            return true;
        }


        /// <summary>
        /// 更改起点节点
        /// </summary>
        /// <param name="cavnasModel">节点所在的画布</param>
        /// <param name="newStartNode">起始节点</param>
        private void SetStartNode(FlowCanvasDetails cavnasModel, IFlowNode newStartNode)
        {
            var oldNodeGuid = cavnasModel.StartNode?.Guid;
            /*if(TryGetNodeModel(oldNodeGuid, out var newStartNodeModel))
            {
                newStartNode.IsStart = false;
            }*/
            cavnasModel.StartNode = newStartNode;
            //newStartNode.IsStart = true;
            UIContextOperation?.Invoke(() => Event.OnStartNodeChanged(new StartNodeChangeEventArgs(cavnasModel.Guid, oldNodeGuid, cavnasModel.StartNode.Guid)));

        }


        #endregion

        #region 视觉效果

        /// <summary>
        /// 定位节点
        /// </summary>
        /// <param name="nodeGuid"></param>
        public void NodeLocate(string nodeGuid)
        {
            if (OperatingSystem.IsWindows())
            {
                UIContextOperation?.Invoke(() => Event.OnNodeLocated(new NodeLocatedEventArgs(nodeGuid)));
            }

        }

        #endregion


    }


}
