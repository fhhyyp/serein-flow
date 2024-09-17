﻿
using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.Entity;
using Serein.Library.Enums;
using Serein.Library.Utils;
using Serein.NodeFlow.Base;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Tool;
using System.Diagnostics;
using System.Net.Mime;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
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


    /// <summary>
    /// 运行环境
    /// </summary>
    public class FlowEnvironment : IFlowEnvironment
    {
        /// <summary>
        /// 节点的命名空间
        /// </summary>
        public const string NodeSpaceName = $"{nameof(Serein)}.{nameof(Serein.NodeFlow)}.{nameof(Serein.NodeFlow.Model)}";

        /// <summary>
        /// 加载Dll
        /// </summary>
        public event LoadDLLHandler OnDllLoad;
        /// <summary>
        /// 项目加载完成
        /// </summary>
        public event ProjectLoadedHandler OnProjectLoaded;
        /// <summary>
        /// 节点连接属性改变事件
        /// </summary>
        public event NodeConnectChangeHandler OnNodeConnectChange;
        /// <summary>
        /// 节点创建事件
        /// </summary>
        public event NodeCreateHandler OnNodeCreate;
        /// <summary>
        /// 移除节点事件
        /// </summary>
        public event NodeRemoteHandler OnNodeRemote;
        /// <summary>
        /// 起始节点变化事件
        /// </summary>
        public event StartNodeChangeHandler OnStartNodeChange;
        /// <summary>
        /// 流程运行完成时间
        /// </summary>
        public event FlowRunCompleteHandler OnFlowRunComplete;

        private FlowStarter? flowStarter = null;

        /// <summary>
        /// 一种轻量的IOC容器
        /// </summary>
        // public SereinIoc SereinIoc { get; } = new SereinIoc();

        /// <summary>
        /// 存储加载的程序集路径
        /// </summary>
        public List<string> LoadedAssemblyPaths { get; } = [];

        /// <summary>
        /// 存储加载的程序集
        /// </summary>
        public List<Assembly> LoadedAssemblies { get; } = [];

        /// <summary>
        /// 存储所有方法信息
        /// </summary>
        public List<MethodDetails> MethodDetailss { get; } = [];


        public Dictionary<string, NodeModelBase> Nodes { get; } = [];

        public List<NodeModelBase> Regions { get; } = [];

        /// <summary>
        /// 存放触发器节点（运行时全部调用）
        /// </summary>
        public List<SingleFlipflopNode> FlipflopNodes { get; } = [];

        /// <summary>
        /// 私有属性
        /// </summary>
        private NodeModelBase _startNode;

        /// <summary>
        /// 起始节点
        /// </summary>
        public NodeModelBase StartNode
        {
            get
            {
                return _startNode;
            }
            set
            {
                if (_startNode is not null)
                { 
                    _startNode.IsStart = false;
                }
                value.IsStart = true;
                _startNode = value;
            }
        }

        /// <summary>
        /// 异步运行
        /// </summary>
        /// <returns></returns>
        public async Task StartAsync()
        {
            flowStarter = new FlowStarter();
            List<SingleFlipflopNode> flipflopNodes = Nodes.Values.Where(it => it.MethodDetails?.MethodDynamicType == NodeType.Flipflop && it.IsStart == false)
                                                                 .Select(it => (SingleFlipflopNode)it)
                                                                 .Where(node => node is SingleFlipflopNode flipflopNode && flipflopNode.NotExitPreviousNode())
                                                                 .ToList();// 获取需要再运行开始之前启动的触发器节点
            var runMethodDetailess = Nodes.Values.Select(item => item.MethodDetails).ToList(); // 获取环境中所有节点的方法信息
            var initMethods = MethodDetailss.Where(it => it.MethodDynamicType == NodeType.Init).ToList();
            var loadingMethods = MethodDetailss.Where(it => it.MethodDynamicType == NodeType.Loading).ToList();
            var exitMethods = MethodDetailss.Where(it => it.MethodDynamicType == NodeType.Exit).ToList();


            await flowStarter.RunAsync(StartNode,
                                           this,
                                           runMethodDetailess,
                                           initMethods,
                                           loadingMethods,
                                           exitMethods,
                                           flipflopNodes);

            if(flowStarter?.FlipFlopState == RunState.NoStart)
            {
                this.Exit(); // 未运行触发器时，才会调用结束方法
            }
            flowStarter = null;
        }
        public void Exit()
        {
            flowStarter?.Exit();
            OnFlowRunComplete?.Invoke(new FlowEventArgs());
        }

        /// <summary>
        /// 清除所有
        /// </summary>
        public void ClearAll()
        {
            LoadedAssemblyPaths.Clear();
            LoadedAssemblies.Clear();
            MethodDetailss.Clear();

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

        #region 对外暴露的接口

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
                (var assembly, var list) = LoadAssembly(dllFilePath);
                if (assembly is not null && list.Count > 0)
                {
                    MethodDetailss.AddRange(list); // 暂存方法描述
                    OnDllLoad?.Invoke(new LoadDLLEventArgs(assembly, list)); // 通知UI创建dll面板显示
                }
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
                if(controlType == NodeControlType.None)
                {
                    continue;
                }
                else
                {
                    TryGetMethodDetails(nodeInfo.MethodName, out MethodDetails? methodDetails); // 加载项目时尝试获取方法信息
                    methodDetails ??= new MethodDetails();
                    var nodeModel = CreateNode(controlType, methodDetails);
                    nodeModel.LoadInfo(nodeInfo); // 创建节点model
                    if (nodeModel is null)
                    {
                        continue;
                    }
                    TryAddNode(nodeModel);
                    if(nodeInfo.ChildNodeGuids?.Length > 0)
                    {
                        regionChildNodes.Add((nodeModel,nodeInfo.ChildNodeGuids));
                        OnNodeCreate?.Invoke(new NodeCreateEventArgs(nodeModel, nodeInfo.Position));
                    }
                    else
                    {
                        ordinaryNodes.Add((nodeModel, nodeInfo.Position));
                    }
                }
            }
            // 加载区域的子项
            foreach((NodeModelBase region, string[] childNodeGuids) item in regionChildNodes)
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
                                                                                      info.guids.Select(guid => Nodes[guid])
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
                Librarys = LoadedAssemblies.Select(assemblies => assemblies.ToLibrary()).ToArray(),
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
            (var assembly, var list) = LoadAssembly(dllPath);
            if (assembly is not null && list.Count > 0)
            {
                MethodDetailss.AddRange(list);
                OnDllLoad?.Invoke(new LoadDLLEventArgs(assembly, list));
            }
        }
        /// <summary>
        /// 运行时创建节点
        /// </summary>
        /// <param name="nodeBase"></param>
        public void CreateNode(NodeControlType nodeControlType, Position position, MethodDetails? methodDetails = null)
        {
            var nodeModel = CreateNode(nodeControlType, methodDetails);
            TryAddNode(nodeModel);

            if(flowStarter?.FlowState != RunState.Completion 
                && nodeControlType == NodeControlType.Flipflop 
                && nodeModel is SingleFlipflopNode flipflopNode)
            {
                // 当前添加节点属于触发器，且当前正在运行，则加载到运行环境中
                flowStarter?.AddFlipflopInRuning(flipflopNode, this);
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
            if (!Nodes.TryGetValue(nodeGuid, out NodeModelBase? remoteNode))
            {
                return;
            }
            if (remoteNode is null)
            {
                return;
            }
            if (remoteNode.IsStart)
            {
                return;
            }


            // 遍历所有父节点，从那些父节点中的子节点集合移除该节点
            foreach (var pnc in remoteNode.PreviousNodes)
            {
                var pCType = pnc.Key; // 连接类型
                for (int i = 0; i < pnc.Value.Count; i++)
                {
                    NodeModelBase? pNode = pnc.Value[i];
                    pNode.SuccessorNodes[pCType].RemoveAt(i);
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
                    //remoteNode.SuccessorNodes[connectionType].RemoveAt(i);

                    //OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(remoteNode.Guid,
                    //                                                toNode.Guid,
                    //                                                connectionType,
                    //                                                NodeConnectChangeEventArgs.ConnectChangeType.Remote)); // 通知UI

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
            if (!Nodes.TryGetValue(fromNodeGuid, out NodeModelBase? fromNode) || !Nodes.TryGetValue(toNodeGuid, out NodeModelBase? toNode))
            {
                return;
            }
            if (fromNode is null || toNode is null)
            {
                return;
            }
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
            if (!Nodes.TryGetValue(fromNodeGuid, out NodeModelBase? fromNode) || !Nodes.TryGetValue(toNodeGuid, out NodeModelBase? toNode))
            {
                return;
            }
            if (fromNode is null || toNode is null)
            {
                return;
            }
            RemoteConnect(fromNode, toNode, connectionType);

            //fromNode.SuccessorNodes[connectionType].Remove(toNode);
            //toNode.PreviousNodes[connectionType].Remove(fromNode);
            //OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(fromNodeGuid,
            //                                                              toNodeGuid,
            //                                                              connectionType,
            //                                                              NodeConnectChangeEventArgs.ConnectChangeType.Remote));
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
            if(toNode is SingleFlipflopNode flipflopNode)
            {
                if (flowStarter?.FlowState != RunState.Completion 
                    && flipflopNode.NotExitPreviousNode())
                {
                    // 被父节点移除连接关系的子节点若为触发器，且无上级节点，则当前流程正在运行，则加载到运行环境中
                    flowStarter?.AddFlipflopInRuning(flipflopNode, this);
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
            var isPass = false;
            if (!string.IsNullOrEmpty(name))
            {
                md = MethodDetailss.FirstOrDefault(it => it.MethodName == name);
                return md != null;
            }
            else
            {
                md = null;
                return false;
            }
            

        }

        /// <summary>
        /// 设置起点控件
        /// </summary>
        /// <param name="newNodeGuid"></param>
        public void SetStartNode(string newNodeGuid)
        {
            if (string.IsNullOrEmpty(newNodeGuid))
            {
                return;
            }
            if (Nodes.TryGetValue(newNodeGuid, out NodeModelBase? newStartNodeModel))
            {
                if (newStartNodeModel != null)
                {
                    SetStartNode(newStartNodeModel);
                    //var oldNodeGuid = "";
                    //if(StartNode != null)
                    //{
                    //    oldNodeGuid = StartNode.Guid;
                    //    StartNode.IsStart = false;
                    //}
                    //newStartNodeModel.IsStart = true;
                    //StartNode = newStartNodeModel;
                    //OnStartNodeChange?.Invoke(new StartNodeChangeEventArgs(oldNodeGuid, newNodeGuid));
                }
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 加载指定路径的DLL文件
        /// </summary>
        /// <param name="dllPath"></param>
        private (Assembly?, List<MethodDetails>) LoadAssembly(string dllPath)
        {
            try
            {
                Assembly assembly = Assembly.LoadFrom(dllPath); // 加载DLL文件
                Type[] types = assembly.GetTypes(); // 获取程序集中的所有类型

                List<Type> scanTypes = assembly.GetTypes().Where(t => t.GetCustomAttribute<DynamicFlowAttribute>()?.Scan == true).ToList();
                if (scanTypes.Count == 0)
                {
                    return (null, []);
                }

                List<MethodDetails> methodDetails = new List<MethodDetails>();
                // 遍历扫描的类型
                foreach (var item in scanTypes)
                {
                    // 加载DLL，创建 MethodDetails、实例作用对象、委托方法
                    var itemMethodDetails = MethodDetailsHelperTmp.GetList(item, false);
                    methodDetails.AddRange(itemMethodDetails);
                    //foreach (var md in itemMethodDetails)
                    //{
                    //    // var instanceType =  
                    //    // Activator.CreateInstance(md.ActingInstanceType);
                    //    // SereinIoc.RegisterInstantiate(md.ActingInstance);
                    //    SereinIoc.Register(md.ActingInstanceType);
                    //}
                }
                LoadedAssemblies.Add(assembly); // 将加载的程序集添加到列表中
                LoadedAssemblyPaths.Add(dllPath); // 记录加载的DLL路径
                return (assembly, methodDetails);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return (null, []);
            }
        }


        /// <summary>
        /// 创建节点
        /// </summary>
        /// <param name="nodeBase"></param>
        private NodeModelBase CreateNode(NodeControlType nodeControlType,MethodDetails? methodDetails = null)
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

            if (nodeType == null)
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
            if (fromNode == null || toNode == null || fromNode == toNode)
            {
                return;
            }

            var ToExistOnFrom = true;
            var FromExistInTo = true;
            ConnectionType[] ct = [ConnectionType.IsSucceed,
                                   ConnectionType.IsFail,
                                   ConnectionType.IsError,
                                   ConnectionType.Upstream];
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

        #region 网络交互

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


        public static Type? ControlTypeToModel(this NodeControlType nodeControlType )
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
                if(node.PreviousNodes[ctType].Count > 0)
                {
                    return false;
                }
            }
            return true;
        }
    }



}
