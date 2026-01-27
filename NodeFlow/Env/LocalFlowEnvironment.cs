using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Model.Nodes;
using Serein.NodeFlow.Services;
using Serein.NodeFlow.Tool;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Serein.NodeFlow.Env
{

    /// <summary>
    /// 运行环境
    /// </summary>
    internal partial class LocalFlowEnvironment : IFlowEnvironment
    {
        /// <summary>
        /// 节点的命名空间
        /// </summary>
        public const string SpaceName = $"{nameof(Serein)}.{nameof(NodeFlow)}.{nameof(Model)}";
        /*public const string ThemeKey = "theme";
        public const string DataKey = "data";
        public const string MsgIdKey = "msgid";
        */
        /// <summary>
        /// 流程运行环境
        /// </summary>
        public LocalFlowEnvironment(IFlowEnvironment flowEnvironment,
                                    IFlowEnvironmentEvent flowEnvironmentEvent,
                                    IFlowLibraryService flowLibraryManagement,
                                    FlowOperationService flowOperationService,
                                    FlowModelService flowModelService,
                                    UIContextOperation uIContextOperation,
                                    IFlowControl flowControl,
                                    IFlowEdit flowEdit,
                                    ISereinIOC sereinIOC,
                                    NodeMVVMService nodeMVVMService)
        {
            Event = flowEnvironmentEvent;
            NodeMVVMManagement = nodeMVVMService;
            FlowEdit = flowEdit;
            IOC = sereinIOC;
            FlowControl = flowControl;
            FlowLibraryService = flowLibraryManagement;
            UIContextOperation = uIContextOperation;
            _flowModelService = flowModelService;
            _flowOperationService = flowOperationService;
            _IsGlobalInterrupt = false;
            _flowEnvIOC = sereinIOC;
        }



        #region 远程管理

        //private MsgControllerOfServer clientMsgManage;

        /// <summary>
        /// <para>表示是否正在控制远程</para>
        /// <para>Local control remote env</para>
        /// </summary>
        public bool IsControlRemoteEnv { get; set; }

        /// <summary>
        /// 打开远程管理
        /// </summary>
        /// <param name="port"></param>
        public void StartRemoteServerAsync(int port = 7525)
        {
            /*if (clientMsgManage is null)
            {
                clientMsgManage = new MsgControllerOfServer(this);
                //clientMsgManage = new MsgControllerOfServer(this,"123456");
            }
            _ = clientMsgManage.StartRemoteServerAsync(port);*/
        }

        /// <summary>
        /// 结束远程管理
        /// </summary>
        public void StopRemoteServer()
        {
            /*try
            {
                clientMsgManage.StopRemoteServer();
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(InfoType.ERROR, "结束远程管理异常：" + ex);
            }*/
        }

        #endregion

        #region 属性

        /// <summary>
        /// 当前环境
        /// </summary>
        public IFlowEnvironment CurrentEnv { get => this; }

        /// <summary>
        /// 流程事件
        /// </summary>
        public IFlowEnvironmentEvent Event { get; set; }

        /// <summary>
        /// 流程编辑接口
        /// </summary>
        public IFlowEdit FlowEdit { get; set; }

        /// <summary>
        /// 流程控制接口
        /// </summary>
        public IFlowControl FlowControl { get; set; }

        /// <summary>
        /// 通过程序集名称管理动态加载的程序集，用于节点创建提供方法描述，流程运行时提供Emit委托
        /// </summary>
        public IFlowLibraryService FlowLibraryService { get; set; }

        /// <summary>
        /// UI线程操作类
        /// </summary>
        public UIContextOperation? UIContextOperation { get; private set; }

        /// <summary>
        /// 节点MVVM管理服务
        /// </summary>
        public NodeMVVMService NodeMVVMManagement { get; private set; }

        /// <summary>
        /// 信息输出等级
        /// </summary>
        public InfoClass InfoClass { get; set; } = InfoClass.Debug;

        /// <summary>
        /// 如果没有全局触发器，且没有循环分支，流程执行完成后自动为 Completion 。
        /// </summary>
        public RunState FlowState { get; set; } = RunState.NoStart;

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
        public bool _IsGlobalInterrupt { get; set; }

        /// <summary>
        /// <para>单例模式IOC容器，内部维护了一个实例字典，默认使用类型的FullName作为Key，如果以“接口-实现类”的方式注册，那么将使用接口类型的FullName作为Key。</para>
        /// <para>当某个类型注册绑定成功后，将不会因为其它地方尝试注册相同类型的行为导致类型被重新创建。</para>
        /// </summary>
        public ISereinIOC IOC
        {
            get
            {
                if (flowRunIOC is null)
                {
                    flowRunIOC = new SereinIOC();
                }
                return flowRunIOC;
            }
            set
            {
                flowRunIOC = value;
            }
        }

        #endregion

        #region 私有变量

        /// <summary>
        /// 流程运行时的IOC容器
        /// </summary>
        private ISereinIOC? flowRunIOC;

        /// <summary>
        /// local环境的IOC容器，主要用于注册本地环境的服务
        /// </summary>
        private ISereinIOC _flowEnvIOC;

      

        /// <summary>
        /// 流程节点操作服务
        /// </summary>
        private readonly FlowOperationService _flowOperationService;

        /// <summary>
        /// 流程画布、节点实体管理服务
        /// </summary>
        private readonly FlowModelService _flowModelService;



        #endregion

        #region 环境对外接口

        /// <summary>
        /// 输出信息
        /// </summary>
        /// <param name="message">日志内容</param>
        /// <param name="type">日志类别</param>
        /// <param name="class">日志级别</param>
        public void WriteLine(InfoType type, string message, InfoClass @class = InfoClass.General)
        {
            if (@class >= this.InfoClass)
            {
                Event.OnEnvOutput(type, message);
            }
            //Console.WriteLine($"{DateTime.UtcNow} [{type}] : {message}{Environment.NewLine}");

        }



        /// <summary>
        /// 保存项目
        /// </summary>
        public void SaveProject()
        {
            var project = GetProjectInfoAsync();
            Task.Run(async () =>
            {
                await SereinEnv.TriggerEvent(() =>
                {
                    Event.OnProjectSaving(new ProjectSavingEventArgs(project));
                });
            });
           
        }

        /// <summary>
        /// 加载项目文件
        /// </summary>
        /// <param name="filePath"></param>
        public void LoadProject(string filePath)
        {
            _ = Task.Run(async () =>
            {
                await this.LoadProjetAsync((filePath));
            });
            /*string content = System.IO.File.ReadAllText(filePath); // 读取整个文件内容
            var FlowProjectData = JsonHelper.Deserialize<SereinProjectData>(content);
            var FileDataPath = System.IO.Path.GetDirectoryName(filePath)!;   //  filePath;//

            this.ProjectFileLocation = filePath;

            var projectData = FlowProjectData;
            // 加载项目配置文件
            var dllPaths = projectData.Librarys.Select(it => it.FilePath).ToList();
            List<MethodDetails> methodDetailss = [];

            // 遍历依赖项中的特性注解，生成方法详情
            foreach (var dllPath in dllPaths)
            {
                string cleanedRelativePath = dllPath.TrimStart('.', '\\');
                var tmpPath = Path.Combine(FileDataPath, cleanedRelativePath);
                var dllFilePath = Path.GetFullPath(tmpPath);
                LoadLibrary(dllFilePath);  // 加载项目文件时加载对应的程序集
            }



            _ = Task.Run(async () =>
            {
                // 加载画布
                foreach (var canvasInfo in projectData.Canvass)
                {
                    await LoadCanvasAsync(canvasInfo);
                }
                var nodeInfos = projectData.Nodes.ToList();
                await FlowEdit.LoadNodeInfosAsync(nodeInfos); // 加载节点信息
                                                              // 加载画布
                foreach (var canvasInfo in projectData.Canvass)
                {
                    FlowEdit.SetStartNode(canvasInfo.Guid, canvasInfo.StartNode); // 设置起始节点
                }

                Event.OnProjectLoaded(new ProjectLoadedEventArgs());
            });*/
        }
        

        public async Task LoadProjetAsync(string filePath)
        {
            var content =  await System.IO.File.ReadAllTextAsync(filePath); // 读取整个文件内容
            var flowProjectData = JsonHelper.Deserialize<SereinProjectData>(content);
            var fileDataPath = System.IO.Path.GetDirectoryName(filePath)!;   //  filePath;//
            if(flowProjectData is null)
            {
                return;
            }
            var projectData = flowProjectData ?? throw new ArgumentNullException(nameof(flowProjectData));

            if (!FlowLibraryService.IsLoadedBaseLibrary)
            {
                var baseLibrary = FlowLibraryService.LoadBaseLibrary();
                if (baseLibrary.MethodInfos.Count > 0 && UIContextOperation is not null)
                { 
                    await UIContextOperation.InvokeAsync(() => Event.OnDllLoad(new LoadDllEventArgs(baseLibrary))); // 通知UI创建dll面板显示
                }

                if (_flowModelService.GetAllCanvasModel().Count == 0)
                {
                    // 创建第一个画布
                    FlowEdit.CreateCanvas("Default", 1920, 1080);
                }
               
            }
           

            
            
            
            this.ProjectFileLocation = filePath;

            // 加载项目配置文件
            var dllPaths = projectData.Librarys.Select(it => it.FilePath).ToList();

            // 遍历依赖项中的特性注解，生成方法详情
            foreach (var dllPath in dllPaths)
            {
                var cleanedRelativePath = dllPath.TrimStart('.', '\\');
                var tmpPath = Path.Combine(filePath, cleanedRelativePath);
                var dllFilePath = Path.GetFullPath(tmpPath);
                LoadLibrary(dllFilePath);  // 加载项目文件时加载对应的程序集
            }



            // 加载画布
            foreach (var canvasInfo in projectData.Canvass)
            {
                await LoadCanvasAsync(canvasInfo);
            }
            await FlowEdit.LoadNodeInfosAsync(projectData.Nodes.ToList()); // 加载节点信息
            // 加载画布
            foreach (var canvasInfo in projectData.Canvass)
            {
                FlowEdit.SetStartNode(canvasInfo.Guid, canvasInfo.StartNode); // 设置起始节点
            }
            Event.OnProjectLoaded(new ProjectLoadedEventArgs());
        }

        /// <summary>
        /// 序列化当前项目的依赖信息、节点信息
        /// </summary>
        /// <returns></returns>
        public SereinProjectData GetProjectInfoAsync()
        {
            var projectData = new SereinProjectData()
            {
                Librarys = this.FlowLibraryService.GetAllLibraryInfo().ToArray(),
                Nodes = _flowModelService.GetAllNodeModel()
                                         .Select(node => node.ToInfo())
                                         .Where(info => info is not null)
                                         .ToArray(),
                Canvass = _flowModelService.GetAllCanvasModel()
                                           .Where(canvas => canvas.Nodes.Count != 0 && !"Default".Equals(canvas.Name))
                                           .Select(canvas => canvas.ToInfo())
                                           .ToArray(),
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

                var libraryInfo = FlowLibraryService.LoadFlowLibrary(dllPath);
                if (libraryInfo is not null && libraryInfo.MethodInfos.Count > 0)
                {
                    UIContextOperation?.Invoke(() => Event.OnDllLoad(new LoadDllEventArgs(libraryInfo))); // 通知UI创建dll面板显示
                }
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(InfoType.ERROR, $"无法加载DLL文件：{ex.Message}");
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
            var groupedNodes = _flowModelService.GetAllNodeModel().Where(node => !string.IsNullOrWhiteSpace(node.MethodDetails.AssemblyName) && node.MethodDetails.AssemblyName.Equals(assemblyName)).ToArray();
            if (groupedNodes.Length == 0)
            {
                var isPass = FlowLibraryService.UnloadLibrary(assemblyName);
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

        private async Task<FlowCanvasDetails> LoadCanvasAsync(FlowCanvasDetailsInfo info)
        {
            var model = new FlowCanvasDetails(this);
            model.LoadInfo(info);
            _flowModelService.AddCanvasModel(model);

            if(UIContextOperation is null)
            {
                Event.OnCanvasCreated(new CanvasCreateEventArgs(model));
            }
            else
            {
                await UIContextOperation.InvokeAsync(() =>
                {
                    Event.OnCanvasCreated(new CanvasCreateEventArgs(model));
                });
            }

            return model;
        }


        /// <summary>
        /// 获取方法描述
        /// </summary>

        public bool TryGetMethodDetailsInfo(string assemblyName, string methodName, out MethodDetailsInfo? mdInfo)
        {
            var isPass = FlowLibraryService.TryGetMethodDetails(assemblyName, methodName, out var md);
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
        /// <param name="assemblyName"></param>
        /// <param name="methodName"></param>
        /// <param name="delegateDetails"></param>
        /// <returns></returns>
        public bool TryGetDelegateDetails(string assemblyName, string methodName, out DelegateDetails? delegateDetails)
        {
            return FlowLibraryService.TryGetDelegateDetails(assemblyName, methodName, out delegateDetails);
        }

        /// <summary>
        /// 设置在UI线程操作的线程上下文
        /// </summary>
        /// <param name="uiContextOperation"></param>
        public void SetUIContextOperation(UIContextOperation uiContextOperation)
        {
            if (uiContextOperation is null)
            {
                return;
            }
            this.UIContextOperation = uiContextOperation;
            IOC.Register<UIContextOperation>(() => uiContextOperation);
            IOC.Build();
            OnUIContextOperationSet();
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
                //SerinExpressionEvaluator.Evaluate($"@Set .{path} = {value}", nodeModel, out _); // 更改对应的数据
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
        /// 从Guid获取画布
        /// </summary>
        /// <param name="nodeGuid">画布Guid</param>
        /// <param name="canvasDetails">画布实体</param>
        /// <returns>是否获取成功</returns>
        /// <exception cref="ArgumentNullException">无法获取节点、Guid/节点为null时报错</exception>
        public bool TryGetCanvasModel(string nodeGuid,[NotNullWhen(true)] out FlowCanvasDetails? canvasDetails)
        {
            if (string.IsNullOrEmpty(nodeGuid))
            {
                canvasDetails = null;
                return false;
            }
            return _flowModelService.TryGetCanvasModel(nodeGuid, out canvasDetails);

        }

        /// <summary>
        /// 从Guid获取节点
        /// </summary>
        /// <param name="nodeGuid">节点Guid</param>
        /// <param name="nodeModel">节点Guid</param>
        /// <returns>节点Model</returns>
        /// <exception cref="ArgumentNullException">无法获取节点、Guid/节点为null时报错</exception>
        public bool TryGetNodeModel(string nodeGuid, [NotNullWhen(true)] out IFlowNode? nodeModel)
        {
            if (string.IsNullOrEmpty(nodeGuid))
            {
                nodeModel = null;
                return false;
            }
            return _flowModelService.TryGetNodeModel(nodeGuid, out nodeModel);

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

        /// <summary>
        /// 设置了 UIContextOperation 需要立刻执行的方法
        /// </summary>
        private void OnUIContextOperationSet()
        {
           
        }

        Task IFlowEnvironment.StartRemoteServerAsync(int port)
        {
            throw new NotImplementedException();
        }
    }


}
