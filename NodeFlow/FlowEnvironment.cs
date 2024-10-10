
using Newtonsoft.Json.Linq;
using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.Entity;
using Serein.Library.Enums;
using Serein.Library.Utils;
using Serein.NodeFlow.Base;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Tool;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Xml.Linq;
using static Serein.Library.Utils.ChannelFlowInterrupt;
using static Serein.NodeFlow.FlowStarter;

namespace Serein.NodeFlow
{
    /*

         脱离wpf平台独立运行。
        加载文件。
        创建节点对象，设置节点属性，确定连接关系，设置起点。

        ↓抽象↓

        wpf依赖于运行环境，而不是运行环境依赖于wpf。

        运行环境实现以下功能：
        ①从项目文件加载数据，生成项目文件对象。
        ②运行项目，调试项目，中止项目，终止项目。
        ③自动包装数据类型，在上下文中传递数据。

     */



   

    /// <summary>
    /// 运行环境
    /// </summary>
    public class FlowEnvironment : IFlowEnvironment, ISereinIOC
    {
        public FlowEnvironment()
        {
            sereinIOC = new SereinIOC();
            ChannelFlowInterrupt = new ChannelFlowInterrupt();
            //LoadedAssemblyPaths = new List<string>();
            //LoadedAssemblies = new List<Assembly>();
            //MethodDetailss = new List<MethodDetails>();
            //Nodes = new Dictionary<string, NodeModelBase>();
            //FlipflopNodes = new List<SingleFlipflopNode>();
            IsGlobalInterrupt = false;
            flowStarter = null;

            sereinIOC.OnIOCMembersChanged += e => this?.OnIOCMembersChanged?.Invoke(e) ; // 监听IOC容器的注册
        }

        

        /// <summary>
        /// 节点的命名空间
        /// </summary>
        public const string SpaceName = $"{nameof(Serein)}.{nameof(Serein.NodeFlow)}.{nameof(Serein.NodeFlow.Model)}";

        #region 环境接口事件
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
        public event NodeRemoteHandler? OnNodeRemote;

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
        public event NodeLocatedHandler? OnNodeLocate;
        #endregion

        #region 属性

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

        public ISereinIOC IOC { get => this; }

        #endregion

        #region 私有变量
        /// <summary>
        /// 容器管理
        /// </summary>
        private readonly SereinIOC sereinIOC;

        /// <summary>
        /// 存储加载的程序集路径
        /// </summary>

        /// <summary>
        /// 存储加载的程序集
        /// </summary>
        private List<NodeLibrary> NodeLibrarys { get; } = [];

        /// <summary>
        /// 存储所有方法信息
        /// </summary>
        //private  MethodDetailss { get; } = [];
        private Dictionary<NodeLibrary, List<MethodDetails>> MethodDetailss { get; } = [];

        /// <summary>
        /// 环境加载的节点集合
        /// Node Guid - Node Model
        /// </summary>
        private Dictionary<string, NodeModelBase> Nodes { get; } = [];

        /// <summary>
        /// 存放触发器节点（运行时全部调用）
        /// </summary>
        private List<SingleFlipflopNode> FlipflopNodes { get; } = [];
        private Dictionary<RegisterSequence,List<Type>> AutoRegisterTypes { get; } = [];

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

        #region 对外暴露的接口

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
            foreach(var mds in MethodDetailss.Values)
            {
                var initMds = mds.Where(it => it.MethodDynamicType == NodeType.Init);
                var loadMds = mds.Where(it => it.MethodDynamicType == NodeType.Loading);
                var exitMds = mds.Where(it => it.MethodDynamicType == NodeType.Exit);
                initMethods.AddRange(initMds);
                loadMethods.AddRange(loadMds);
                exitMethods.AddRange(exitMds);
            }
            this.IOC.Reset(); // 开始运行时清空ioc中注册的实例
            this.IOC.CustomRegisterInstance(typeof(IFlowEnvironment).FullName,this);
            



            await flowStarter.RunAsync(this, nodes, AutoRegisterTypes, initMethods, loadMethods, exitMethods);

            if (flowStarter?.FlipFlopState == RunState.NoStart)
            {
                this.Exit(); // 未运行触发器时，才会调用结束方法
            }
            flowStarter = null;
        }

        public async Task StartFlowInSelectNodeAsync(string startNodeGuid)
        {
            if (flowStarter is null)
            {
                return;
            }
            if (flowStarter.FlowState == RunState.Running || flowStarter.FlipFlopState == RunState.Running)
            {
                NodeModelBase? nodeModel = GuidToModel(startNodeGuid);
                if (nodeModel is null || nodeModel is SingleFlipflopNode)
                {
                    return;
                }
                await flowStarter.StartFlowInSelectNodeAsync(nodeModel);
            }
            else
            {
                return;
            }
        }

        /// <summary>
        /// 退出
        /// </summary>
        public void Exit()
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


            OnFlowRunComplete?.Invoke(new FlowEventArgs());

            GC.Collect();
        }

        /// <summary>
        /// 清除所有
        /// </summary>
        public void ClearAll()
        {
            //LoadedAssemblyPaths.Clear();
            NodeLibrarys.Clear();
            MethodDetailss.Clear();

        }


        /// <summary>
        /// 加载项目文件
        /// </summary>
        /// <param name="project"></param>
        /// <param name="filePath"></param>
        public void LoadProject(SereinProjectData project, string filePath)
        {
            // 加载项目配置文件
            var dllPaths = project.Librarys.Select(it => it.Path).ToList();
            List<MethodDetails> methodDetailss = [];

            // 遍历依赖项中的特性注解，生成方法详情
            foreach (var dll in dllPaths)
            {
                var dllFilePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(filePath, dll));
                LoadDllNodeInfo(dllFilePath);
            }
            // 方法加载完成，缓存到运行环境中。
            //MethodDetailss.AddRange(methodDetailss);
            //methodDetailss.Clear();


            List<(NodeModelBase, string[])> regionChildNodes = new List<(NodeModelBase, string[])>();
            List<(NodeModelBase, Position)> ordinaryNodes = new List<(NodeModelBase, Position)>();
            // 加载节点
            foreach (var nodeInfo in project.Nodes)
            {
                var controlType = GetNodeControlType(nodeInfo);
                if (controlType == NodeControlType.None)
                {
                    continue;
                }
                else
                {
                    TryGetMethodDetails(nodeInfo.MethodName, out MethodDetails? methodDetails); // 加载项目时尝试获取方法信息
                    if(controlType == NodeControlType.ExpOp || controlType == NodeControlType.ExpOp)
                    {
                        methodDetails ??= new MethodDetails();
                    }
                    if(methodDetails is null)
                    {
                        continue; // 节点对应的方法不存在于DLL中
                    }


                    var nodeModel = CreateNode(controlType, methodDetails);
                    nodeModel.LoadInfo(nodeInfo); // 创建节点model
                    if (nodeModel is null)
                    {
                        nodeInfo.Guid = string.Empty;
                        continue;
                    }
                    TryAddNode(nodeModel);
                    if (nodeInfo.ChildNodeGuids?.Length > 0)
                    {
                        regionChildNodes.Add((nodeModel, nodeInfo.ChildNodeGuids));
                        OnNodeCreate?.Invoke(new NodeCreateEventArgs(nodeModel, nodeInfo.Position));
                    }
                    else
                    {
                        ordinaryNodes.Add((nodeModel, nodeInfo.Position));
                    }
                }
            }
            // 加载区域的子项
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
                    // 存在节点
                    OnNodeCreate?.Invoke(new NodeCreateEventArgs(childNode, true, item.region.Guid));
                }
            }
            // 加载节点
            foreach ((NodeModelBase nodeModel, Position position) item in ordinaryNodes)
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
                OnNodeCreate?.Invoke(new NodeCreateEventArgs(item.nodeModel, item.position));
            }



            // 确定节点之间的连接关系
            foreach (var nodeInfo in project.Nodes)
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
                        ConnectNode(fromNode, toNode, item.connectionType); // 加载时确定节点间的连接关系
                    }
                }
            }

            SetStartNode(project.StartNode);
            OnProjectLoaded?.Invoke(new ProjectLoadedEventArgs());
        }
            
        /// <summary>
        /// 保存项目为项目文件
        /// </summary>
        /// <returns></returns>
        public SereinProjectData SaveProject()
        {
            var projectData = new SereinProjectData()
            {
                Librarys = NodeLibrarys.Select(assemblies => assemblies.Assembly.ToLibrary()).ToArray(),
                Nodes = Nodes.Values.Select(node => node.ToInfo()).Where(info => info is not null).ToArray(),
                StartNode = Nodes.Values.FirstOrDefault(it => it.IsStart)?.Guid,
            };
            return projectData;
        }

        /// <summary>
        /// 从文件路径中加载DLL
        /// </summary>
        /// <param name="dllPath"></param>
        /// <returns></returns> 
        public void LoadDll(string dllPath)
        {
            LoadDllNodeInfo(dllPath);
        }

        public bool RemoteDll(string assemblyFullName)
        {
            var library  = NodeLibrarys.FirstOrDefault(nl => assemblyFullName.Equals(nl.Assembly.FullName));
            if(library is null)
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
            if(Nodes.Count == 0)
            {
                return true; // 当前无节点，可以直接删除
            }

            if (MethodDetailss.TryGetValue(library,out var mds)) // 存在方法
            {
                foreach(var md in mds)
                {
                    if(groupedNodes.TryGetValue(md.MethodName,out int count))
                    {
                        if (count > 0)
                        {
                            return false; // 创建过相关的节点
                        }
                    }
                }
                MethodDetailss.Remove(library);
                return true; // 没有创建相关的节点
            }
            else
            {
                return true; 
            }
        }


        /// <summary>
        /// 运行时创建节点
        /// </summary>
        /// <param name="nodeControlType"></param>
        /// <param name="position"></param>
        /// <param name="methodDetails"></param>
        public void CreateNode(NodeControlType nodeControlType, Position position, MethodDetails? methodDetails = null)
        {
            var nodeModel = CreateNode(nodeControlType, methodDetails);
            TryAddNode(nodeModel);

            if (flowStarter?.FlowState != RunState.Completion
                && nodeControlType == NodeControlType.Flipflop
                && nodeModel is SingleFlipflopNode flipflopNode)
            {
                _ = flowStarter?.RunGlobalFlipflopAsync(this, flipflopNode);  // 当前添加节点属于触发器，且当前正在运行，则加载到运行环境中
            }

            // 通知UI更改
            OnNodeCreate?.Invoke(new NodeCreateEventArgs(nodeModel, position));
            // 因为需要UI先布置了元素，才能通知UI变更特效
            // 如果不存在流程起始控件，默认设置为流程起始控件
            if (StartNode is null)
            {
                SetStartNode(nodeModel);
            }
        }

        /// <summary>
        /// 移除节点
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void RemoteNode(string nodeGuid)
        {
            var remoteNode = GuidToModel(nodeGuid);
            if (remoteNode is null) return;
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

                    OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(pNode.Guid,
                                                                    remoteNode.Guid,
                                                                    pCType,
                                                                    NodeConnectChangeEventArgs.ConnectChangeType.Remote)); // 通知UI
                }
            }

            // 遍历所有子节点，从那些子节点中的父节点集合移除该节点
            foreach (var snc in remoteNode.SuccessorNodes)
            {
                var connectionType = snc.Key; // 连接类型
                for (int i = 0; i < snc.Value.Count; i++)
                {
                    NodeModelBase? toNode = snc.Value[i];

                    RemoteConnect(remoteNode, toNode, connectionType);

                }
            }

            // 从集合中移除节点
            Nodes.Remove(nodeGuid);
            OnNodeRemote?.Invoke(new NodeRemoteEventArgs(nodeGuid));
        }

        /// <summary>
        /// 连接节点
        /// </summary>
        /// <param name="fromNode">起始节点</param>
        /// <param name="toNode">目标节点</param>
        /// <param name="connectionType">连接关系</param>
        public void ConnectNode(string fromNodeGuid, string toNodeGuid, ConnectionType connectionType)
        {
            // 获取起始节点与目标节点
            var fromNode = GuidToModel(fromNodeGuid);
            var toNode = GuidToModel(toNodeGuid);
            if (fromNode is null) return;
            if (toNode is null) return;
            // 开始连接
            ConnectNode(fromNode, toNode, connectionType); // 外部调用连接方法

        }

        /// <summary>
        /// 移除连接关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点Guid</param>
        /// <param name="toNodeGuid">目标节点Guid</param>
        /// <param name="connectionType">连接关系</param>
        /// <exception cref="NotImplementedException"></exception>
        public void RemoteConnect(string fromNodeGuid, string toNodeGuid, ConnectionType connectionType)
        {
            // 获取起始节点与目标节点
            var fromNode = GuidToModel(fromNodeGuid);
            var toNode = GuidToModel(toNodeGuid);
            if (fromNode is null) return;
            if (toNode is null) return;
            RemoteConnect(fromNode, toNode, connectionType);

        }

        /// <summary>
        /// 移除连接关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点Model</param>
        /// <param name="toNodeGuid">目标节点Model</param>
        /// <param name="connectionType">连接关系</param>
        /// <exception cref="NotImplementedException"></exception>
        private void RemoteConnect(NodeModelBase fromNode, NodeModelBase toNode, ConnectionType connectionType)
        {
            fromNode.SuccessorNodes[connectionType].Remove(toNode);
            toNode.PreviousNodes[connectionType].Remove(fromNode);

            if (toNode is SingleFlipflopNode flipflopNode) // 子节点为触发器
            {
                if (flowStarter?.FlowState != RunState.Completion 
                    && flipflopNode.NotExitPreviousNode()) // 正在运行，且该触发器没有上游节点
                {
                    flowStarter?.RunGlobalFlipflopAsync(this, flipflopNode);  // 被父节点移除连接关系的子节点若为触发器，且无上级节点，则当前流程正在运行，则加载到运行环境中
                }
            }

            // 通知UI
            OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(fromNode.Guid,
                                                                          toNode.Guid,
                                                                          connectionType,
                                                                          NodeConnectChangeEventArgs.ConnectChangeType.Remote));
        }

        /// <summary>
        /// 获取方法描述
        /// </summary>
        public bool TryGetMethodDetails(string name, out MethodDetails? md)
        {
            if (!string.IsNullOrEmpty(name))
            {
                foreach(var mds in MethodDetailss.Values)
                {
                    md = mds.FirstOrDefault(it => it.MethodName == name);
                    if(md != null)
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
        public bool SetNodeInterrupt(string nodeGuid, InterruptClass interruptClass)
        {
            var nodeModel = GuidToModel(nodeGuid);
            if (nodeModel is null) return false;
            if (interruptClass == InterruptClass.None)
            {
                nodeModel.CancelInterrupt();
            }
            else if (interruptClass == InterruptClass.Branch)
            {
                nodeModel.DebugSetting.CancelInterruptCallback?.Invoke();
                nodeModel.DebugSetting.GetInterruptTask = () =>
                {
                    TriggerInterrupt(nodeGuid, "", InterruptTriggerEventArgs.InterruptTriggerType.Monitor);
                    return ChannelFlowInterrupt.GetOrCreateChannelAsync(nodeGuid);
                };
                nodeModel.DebugSetting.CancelInterruptCallback = () =>
                {
                    ChannelFlowInterrupt.TriggerSignal(nodeGuid);
                };

            }
            else if (interruptClass == InterruptClass.Global) // 全局……做不了omg
            {
                return false;
            }
            nodeModel.DebugSetting.InterruptClass = interruptClass;
            OnNodeInterruptStateChange?.Invoke(new NodeInterruptStateChangeEventArgs(nodeGuid, interruptClass));
            return true;
        }


        /// <summary>
        /// 添加表达式中断
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="expression"></param>
        /// <returns></returns>
        public bool AddInterruptExpression(string key, string expression)
        {
            if(string.IsNullOrEmpty(expression)) return false;
            if (dictMonitorObjExpInterrupt.TryGetValue(key, out var condition))
            {
                condition.Clear(); // 暂时
                condition.Add(expression);// 暂时
                return true;
            }
            else
            {
                var exps = new List<string>();
                exps.Add(expression);
                dictMonitorObjExpInterrupt.TryAdd(key, exps);
                return true;
            }
        }
        //public bool AddInterruptExpression(string nodeGuid, string expression)
        //{
        //    var nodeModel = GuidToModel(nodeGuid);
        //    if (nodeModel is null) return false;
        //    if (string.IsNullOrEmpty(expression))
        //    {
        //        nodeModel.DebugSetting.InterruptExpressions.Clear(); //  传入空表达式时清空
        //        return true;
        //    }

        //    if (nodeModel.DebugSetting.InterruptExpressions.Contains(expression))
        //    {
        //        Console.WriteLine("表达式已存在");
        //        return false;
        //    }
        //    else
        //    {
        //        nodeModel.DebugSetting.InterruptExpressions.Clear();// 暂时删除，等UI做好了
        //        nodeModel.DebugSetting.InterruptExpressions.Add(expression);
        //        return true;
        //    }
        //}


        /// <summary>
        /// 监视节点的数据（暂时注释）
        /// </summary>
        /// <param name="nodeGuid">需要监视的节点Guid</param>
        //public void SetMonitorObjState(string nodeGuid, bool isMonitor)
        //{
        //    var nodeModel = GuidToModel(nodeGuid);
        //    if (nodeModel is null) return;
        //    nodeModel.DebugSetting.IsMonitorFlowData = isMonitor;

        //    if (isMonitor)
        //    {
        //        var obj = nodeModel.GetFlowData();
        //         if(obj is not null)
        //        {
        //            FlowDataNotification(nodeGuid, obj);

        //        }
        //    }
        //    else
        //    {
        //        // 不再监视的节点清空表达式
        //        nodeModel.DebugSetting.InterruptExpressions.Clear();
        //    }
        //}

        /// <summary>
        /// 要监视的对象，以及与其关联的表达式
        /// </summary>
        private ConcurrentDictionary<string, List<string>> dictMonitorObjExpInterrupt = [];

        /// <summary>
        /// 设置对象的监视状态
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="isMonitor"></param>
        /// <returns></returns>
        public void SetMonitorObjState(string key,  bool isMonitor)
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
        /// <param name="nodeGuid"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public bool CheckObjMonitorState(string key, out List<string>? exps)
        {
            if (string.IsNullOrEmpty(key)) { exps = null; return false; }
            return dictMonitorObjExpInterrupt.TryGetValue(key, out exps);
        }

        /// <summary>
        /// 启动器调用，节点数据更新通知
        /// </summary>
        /// <param name="nodeGuid"></param>
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


        public Task<CancelType> GetOrCreateGlobalInterruptAsync()
        {
            IsGlobalInterrupt = true;
            return ChannelFlowInterrupt.GetOrCreateChannelAsync(this.EnvName);
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
                MethodDetailss.Add(nodeLibrary, mdlist);
                NodeLibrarys.Add(nodeLibrary);

                foreach(var kv in registerTypes)
                {
                   if (!AutoRegisterTypes.TryGetValue(kv.Key, out var types))
                    {
                        types = new List<Type>();
                        AutoRegisterTypes.Add(kv.Key, types);
                    }
                    types.AddRange(kv.Value);
                }

                OnDllLoad?.Invoke(new LoadDllEventArgs(nodeLibrary, mdlist)); // 通知UI创建dll面板显示
            }

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
                        if(!autoRegisterTypes.TryGetValue(autoRegisterAttribute.Class,out var valus))
                        {
                            valus = new List<Type>();
                            autoRegisterTypes.Add(autoRegisterAttribute.Class, valus);
                        }
                        valus.Add(type);
                    }
  
                }
                

                //Dictionary<Sequence, Type> autoRegisterTypes = assembly.GetTypes().Where(t => t.GetCustomAttribute<AutoRegisterAttribute>() is not null).ToList();



                List<(Type, string)> scanTypes = types.Select(t => {
                    if (t.GetCustomAttribute<DynamicFlowAttribute>() is DynamicFlowAttribute dynamicFlowAttribute
                       && dynamicFlowAttribute.Scan == true)
                    {
                        return (t, dynamicFlowAttribute.Name);
                    }
                    else
                    {
                        return (null, null);
                    }
                }).Where(it => it.t is not null) .ToList();
                if (scanTypes.Count == 0)
                {
                    return (null, [], []);
                }

                List<MethodDetails> methodDetails = new List<MethodDetails>();
                // 遍历扫描的类型
                foreach ((var type,var flowName ) in scanTypes)
                {
                    // 加载DLL，创建 MethodDetails、实例作用对象、委托方法
                    var assemblyName = type.Assembly.GetName().Name;
                    if (string.IsNullOrEmpty(assemblyName))
                    {
                        continue;
                    }
                    var methods = MethodDetailsHelper.GetMethodsToProcess(type);
                    foreach(var method in methods)
                    {
                        (var md, var del) = MethodDetailsHelper.CreateMethodDetails(type, method, assemblyName);
                        if(md is null || del is null)
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
                    Assembly = assembly,
                    Path = dllPath,
                };
                //LoadedAssemblies.Add(assembly); // 将加载的程序集添加到列表中
                //LoadedAssemblyPaths.Add(dllPath); // 记录加载的DLL路径
                return (nodeLibrary, autoRegisterTypes , methodDetails);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return (null, [],[]);
            }
        }

        /// <summary>
        /// 运行环节加载了项目文件，需要创建节点控件
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <param name="methodDetailss"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private NodeControlType GetNodeControlType(NodeInfo nodeInfo)
        {
            // 创建控件实例
            NodeControlType controlType = nodeInfo.Type switch
            {
                $"{NodeStaticConfig.NodeSpaceName}.{nameof(SingleActionNode)}" => NodeControlType.Action,// 动作节点控件
                $"{NodeStaticConfig.NodeSpaceName}.{nameof(SingleFlipflopNode)}" => NodeControlType.Flipflop, // 触发器节点控件

                $"{NodeStaticConfig.NodeSpaceName}.{nameof(SingleConditionNode)}" => NodeControlType.ExpCondition,// 条件表达式控件
                $"{NodeStaticConfig.NodeSpaceName}.{nameof(SingleExpOpNode)}" => NodeControlType.ExpOp, // 操作表达式控件

                $"{NodeStaticConfig.NodeSpaceName}.{nameof(CompositeConditionNode)}" => NodeControlType.ConditionRegion, // 条件区域控件
                _ => NodeControlType.None,
            };

            return controlType;
        }


        /// <summary>
        /// 创建节点
        /// </summary>
        /// <param name="nodeBase"></param>
        private NodeModelBase CreateNode(NodeControlType nodeControlType, MethodDetails? methodDetails = null)
        {
            // 确定创建的节点类型
            Type? nodeType = nodeControlType switch
            {
                NodeControlType.Action => typeof(SingleActionNode),
                NodeControlType.Flipflop => typeof(SingleFlipflopNode),

                NodeControlType.ExpOp => typeof(SingleExpOpNode),
                NodeControlType.ExpCondition => typeof(SingleConditionNode),
                NodeControlType.ConditionRegion => typeof(CompositeConditionNode),
                _ => null
            };

            if (nodeType is null)
            {
                throw new Exception($"节点类型错误[{nodeControlType}]");
            }
            // 生成实例
            var nodeObj = Activator.CreateInstance(nodeType);
            if (nodeObj is not NodeModelBase nodeBase)
            {
                throw new Exception($"无法创建目标节点类型的实例[{nodeControlType}]");
            }

            // 配置基础的属性
            nodeBase.ControlType = nodeControlType;
            if (methodDetails != null)
            {
                var md = methodDetails.Clone();
                nodeBase.DisplayName = md.MethodTips;
                nodeBase.MethodDetails = md;
            }

            // 如果是触发器，则需要添加到专属集合中
            if (nodeControlType == NodeControlType.Flipflop && nodeBase is SingleFlipflopNode flipflopNode)
            {
                var guid = flipflopNode.Guid;
                if (!FlipflopNodes.Exists(it => it.Guid.Equals(guid)))
                {
                    FlipflopNodes.Add(flipflopNode);
                }
            }

            return nodeBase;
        }

        private bool TryAddNode(NodeModelBase nodeModel)
        {
            nodeModel.Guid ??= Guid.NewGuid().ToString();
            Nodes[nodeModel.Guid] = nodeModel;
            return true;
        }

        /// <summary>
        /// 连接节点
        /// </summary>
        /// <param name="fromNode">起始节点</param>
        /// <param name="toNode">目标节点</param>
        /// <param name="connectionType">连接关系</param>
        private void ConnectNode(NodeModelBase fromNode, NodeModelBase toNode, ConnectionType connectionType)
        {
            if (fromNode is null || toNode is null || fromNode == toNode)
            {
                return;
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
            foreach (ConnectionType ctType in ct)
            {
                var FToTo = fromNode.SuccessorNodes[ctType].Where(it => it.Guid.Equals(toNode.Guid)).ToArray();
                var ToOnF = toNode.PreviousNodes[ctType].Where(it => it.Guid.Equals(fromNode.Guid)).ToArray();
                ToExistOnFrom = FToTo.Length > 0;
                FromExistInTo = ToOnF.Length > 0;
                if (ToExistOnFrom && FromExistInTo)
                {
                    Console.WriteLine("起始节点已与目标节点存在连接");
                    return;
                }
                else
                {
                    // 检查是否可能存在异常
                    if (!ToExistOnFrom && FromExistInTo)
                    {
                        Console.WriteLine("目标节点不是起始节点的子节点，起始节点却是目标节点的父节点");
                        return;
                    }
                    else if (ToExistOnFrom && !FromExistInTo)
                    {
                        //
                        Console.WriteLine(" 起始节点不是目标节点的父节点，目标节点却是起始节点的子节点");
                        return;
                    }
                    else // if (!ToExistOnFrom && !FromExistInTo)
                    {
                        // 可以正常连接
                    }
                }
            }


            fromNode.SuccessorNodes[connectionType].Add(toNode); // 添加到起始节点的子分支
            toNode.PreviousNodes[connectionType].Add(fromNode); // 添加到目标节点的父分支
            OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(fromNode.Guid,
                                                                    toNode.Guid,
                                                                    connectionType,
                                                                    NodeConnectChangeEventArgs.ConnectChangeType.Create)); // 通知UI
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
            OnStartNodeChange?.Invoke(new StartNodeChangeEventArgs(oldNodeGuid, StartNode.Guid));
        }


        #endregion

        #region 视觉效果

        /// <summary>
        /// 定位节点
        /// </summary>
        /// <param name="nodeGuid"></param>
        public void NodeLocated(string nodeGuid)
        {
            OnNodeLocate?.Invoke(new NodeLocatedEventArgs(nodeGuid));
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
        

        void ISereinIOC.CustomRegisterInstance(string key, object instance, bool needInjectProperty)
        {
            sereinIOC.CustomRegisterInstance(key, instance, needInjectProperty);
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
    public static class FlowFunc
    {
        public static Library.Entity.Library ToLibrary(this Assembly assembly)
        {
            return new Library.Entity.Library
            {
                Name = assembly.GetName().Name,
                Path = assembly.Location,
            };
        }

        public static ConnectionType ToContentType(this FlipflopStateType flowStateType)
        {
            return flowStateType switch
            {
                FlipflopStateType.Succeed => ConnectionType.IsSucceed,
                FlipflopStateType.Fail => ConnectionType.IsFail,
                FlipflopStateType.Error => ConnectionType.IsError,
                FlipflopStateType.Cancel => ConnectionType.None,
                _ => throw new NotImplementedException("未定义的流程状态")
            };
        }


        public static Type? ControlTypeToModel(this NodeControlType nodeControlType)
        {
            // 确定创建的节点类型
            Type? nodeType = nodeControlType switch
            {
                NodeControlType.Action => typeof(SingleActionNode),
                NodeControlType.Flipflop => typeof(SingleFlipflopNode),

                NodeControlType.ExpOp => typeof(SingleExpOpNode),
                NodeControlType.ExpCondition => typeof(SingleConditionNode),
                NodeControlType.ConditionRegion => typeof(CompositeConditionNode),
                _ => null
            };
            return nodeType;
        }
        public static NodeControlType ModelToControlType(this NodeControlType nodeControlType)
        {
            var type = nodeControlType.GetType();
            NodeControlType controlType = type switch
            {
                Type when type == typeof(SingleActionNode) => NodeControlType.Action,
                Type when type == typeof(SingleFlipflopNode) => NodeControlType.Flipflop,

                Type when type == typeof(SingleExpOpNode) => NodeControlType.ExpOp,
                Type when type == typeof(SingleConditionNode) => NodeControlType.ExpCondition,
                Type when type == typeof(CompositeConditionNode) => NodeControlType.ConditionRegion,
                _ => NodeControlType.None,
            };
            return controlType;
        }

        public static bool NotExitPreviousNode(this SingleFlipflopNode node)
        {
            ConnectionType[] ct = [ConnectionType.IsSucceed,
                                   ConnectionType.IsFail,
                                   ConnectionType.IsError,
                                   ConnectionType.Upstream];
            foreach (ConnectionType ctType in ct)
            {
                if (node.PreviousNodes[ctType].Count > 0)
                {
                    return false;
                }
            }
            return true;
        }
    }



}
