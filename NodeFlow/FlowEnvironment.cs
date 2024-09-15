using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.Entity;
using Serein.Library.Enums;
using Serein.Library.Utils;
using Serein.NodeFlow.Base;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Tool;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;

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
        /// 加载Dll
        /// </summary>
        public event LoadDLLHandler OnDllLoad;
        /// <summary>
        /// 加载节点
        /// </summary>
        public event LoadNodeHandler OnLoadNode;
        /// <summary>
        /// 连接节点
        /// </summary>
        public event NodeConnectChangeHandler OnNodeConnectChange;
        /// <summary>
        /// 创建节点
        /// </summary>
        public event NodeCreateHandler OnNodeCreate;
        public event NodeRemoteHandler OnNodeRemote;
        public event StartNodeChangeHandler OnStartNodeChange;
        public event FlowRunCompleteHandler OnFlowRunComplete;

        private FlowStarter? nodeFlowStarter = null;

        /// <summary>
        /// 节点的命名空间
        /// </summary>
        public const string NodeSpaceName = $"{nameof(Serein)}.{nameof(Serein.NodeFlow)}.{nameof(Serein.NodeFlow.Model)}";

        /// <summary>
        /// 一种轻量的IOC容器
        /// </summary>
        public SereinIoc SereinIoc { get; } = new SereinIoc();

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


        public Dictionary<string,NodeModelBase> Nodes { get; } = [];

        public List<NodeModelBase> Regions { get; } = [];

        /// <summary>
        /// 存放触发器节点（运行时全部调用）
        /// </summary>
        public List<SingleFlipflopNode> FlipflopNodes { get; } = [];

        private NodeModelBase? _startNode = null;

        /// <summary>
        /// 起始节点
        /// </summary>
        public NodeModelBase StartNode { 
            get 
            { 
                return _startNode;
            }
            set {
                if(_startNode is null)
                {
                    value.IsStart = true;
                    _startNode = value;
                }
                else
                {
                    _startNode.IsStart = false;
                    value.IsStart = true;
                    _startNode = value;
                }
            } }

        /// <summary>
        /// 异步运行
        /// </summary>
        /// <returns></returns>
        public async Task StartAsync()
        {
            nodeFlowStarter = new FlowStarter(SereinIoc);
            var nodes = Nodes.Values.ToList();
            var flipflopNodes = nodes.Where(it => it.MethodDetails?.MethodDynamicType == NodeType.Flipflop
                                               && it.IsStart == false)
                                     .Select(it => it as SingleFlipflopNode)
                                     .ToList();

            await nodeFlowStarter.RunAsync(StartNode, this, MethodDetailss, flipflopNodes);
        }
        public void Exit()
        {
            nodeFlowStarter?.Exit();
            nodeFlowStarter = null;
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


        #region 对外暴露的接口

        /// <summary>
        /// 获取方法描述
        /// </summary>
        public bool TryGetMethodDetails(string name, out MethodDetails? md)
        {
            md = MethodDetailss.FirstOrDefault(it => it.MethodName == name);
            if(md == null)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 加载项目文件
        /// </summary>
        /// <param name="projectFile"></param>
        /// <param name="filePath"></param>
        public void LoadProject(SereinOutputFileData projectFile, string filePath)
        {
            // 加载项目配置文件
            var dllPaths = projectFile.Librarys.Select(it => it.Path).ToList();
            List<MethodDetails> methodDetailss = [];

            // 遍历依赖项中的特性注解，生成方法详情
            foreach (var dll in dllPaths)
            {
                var dllFilePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(filePath, dll));
                (var assembly, var list) = LoadAssembly(dllFilePath); 
                if (assembly is not null && list.Count > 0)
                {
                    methodDetailss.AddRange(methodDetailss); // 暂存方法描述
                    OnDllLoad?.Invoke(new LoadDLLEventArgs(assembly, methodDetailss)); // 通知UI创建dll面板显示
                }
            }
            // 方法加载完成，缓存到运行环境中。
            MethodDetailss.AddRange(methodDetailss); 
            methodDetailss.Clear();


            // 加载节点
            foreach (var nodeInfo in projectFile.Nodes)
            {
                if (TryGetMethodDetails(nodeInfo.MethodName, out MethodDetails? methodDetails))
                {
                    OnLoadNode?.Invoke(new LoadNodeEventArgs(nodeInfo, methodDetails));
                }
            }

            // 确定节点之间的连接关系
            foreach (var nodeInfo in projectFile.Nodes)
            {
                if (!Nodes.TryGetValue(nodeInfo.Guid, out NodeModelBase fromNode))
                {
                    // 不存在对应的起始节点
                    continue;
                }
                

                List<(ConnectionType, string[])> nodeGuids = [(ConnectionType.IsSucceed,nodeInfo.TrueNodes),
                                                              (ConnectionType.IsFail,   nodeInfo.FalseNodes),
                                                              (ConnectionType.IsError,  nodeInfo.ErrorNodes),
                                                              (ConnectionType.Upstream, nodeInfo.UpstreamNodes)];

                List<(ConnectionType, NodeModelBase[])> nodes = nodeGuids.Where(info => info.Item2.Length > 0)
                                                                     .Select(info => (info.Item1,
                                                                                      info.Item2.Select(guid => Nodes[guid])
                                                                                                .ToArray()))
                                                                     .ToList();
                // 遍历每种类型的节点分支（四种）
                foreach ((ConnectionType connectionType, NodeModelBase[] nodeBases) item in nodes)
                {
                    // 遍历当前类型分支的节点（确认连接关系）
                    foreach (var node in item.nodeBases)
                    {
                        ConnectNode(fromNode, node, item.connectionType); // 加载时确定节点间的连接关系
                    }
                }


            }

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
            if(fromNode is null || toNode is null)
            {
                return ;
            } 
            // 开始连接
            ConnectNode(fromNode, toNode, connectionType); // 外部调用连接方法

        }

        /// <summary>
        /// 创建节点
        /// </summary>
        /// <param name="nodeBase"></param>
        public void CreateNode(NodeControlType nodeControlType, MethodDetails? methodDetails = null)
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
            if(nodeType == null)
            {
                return;
            }
            // 生成实例
            var nodeObj = Activator.CreateInstance(nodeType);
            if (nodeObj is not NodeModelBase nodeBase)
            {
                return;
            }

            // 配置基础的属性
            nodeBase.ControlType = nodeControlType;
            nodeBase.Guid = Guid.NewGuid().ToString();
            if (methodDetails != null)
            {
                var md = methodDetails.Clone();
                nodeBase.DisplayName = md.MethodTips;
                nodeBase.MethodDetails = md;
            }
            Nodes[nodeBase.Guid] = nodeBase;

            // 如果是触发器，则需要添加到专属集合中
            if (nodeControlType == NodeControlType.Flipflop && nodeBase is SingleFlipflopNode flipflopNode )
            {
                var guid = flipflopNode.Guid;
                if (!FlipflopNodes.Exists(it => it.Guid.Equals(guid)))
                {
                    FlipflopNodes.Add(flipflopNode);
                }
            }
           
            // 通知UI更改
            OnNodeCreate?.Invoke(new NodeCreateEventArgs(nodeBase));
            // 因为需要UI先布置了元素，才能通知UI变更特效
            // 如果不存在流程起始控件，默认设置为流程起始控件
            if (StartNode is null)
            {
                SetStartNode(nodeBase);
            }
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
        /// 保存项目为项目文件
        /// </summary>
        /// <returns></returns>
        public SereinOutputFileData SaveProject()
        {
            var projectData = new SereinOutputFileData()
            {
                Librarys = LoadedAssemblies.Select(assemblies => assemblies.ToLibrary()).ToArray(),
                Nodes = Nodes.Values.Select(node => node.ToInfo()).Where(info => info is not null).ToArray(),
                StartNode = Nodes.Values.FirstOrDefault(it => it.IsStart)?.Guid,
            };
            return projectData;
        }

        /// <summary>
        /// 移除连接关系
        /// </summary>
        /// <param name="fromNodeGuid"></param>
        /// <param name="toNodeGuid"></param>
        /// <param name="connectionType"></param>
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

            fromNode.SuccessorNodes[connectionType].Remove(toNode);
            toNode.PreviousNodes[connectionType].Remove(fromNode);
            OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(fromNodeGuid,
                                                                          toNodeGuid,
                                                                          connectionType,
                                                                          NodeConnectChangeEventArgs.ChangeTypeEnum.Remote));
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
            foreach(var pnc in remoteNode.PreviousNodes)
            {
                var pCType = pnc.Key; // 连接类型
                for (int i = 0; i < pnc.Value.Count; i++)
                {
                    NodeModelBase? pNode = pnc.Value[i];
                    pNode.SuccessorNodes[pCType].RemoveAt(i);
                    OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(pNode.Guid,
                                                                    remoteNode.Guid,
                                                                    pCType,
                                                                    NodeConnectChangeEventArgs.ChangeTypeEnum.Remote)); // 通知UI
                }
            }

            // 遍历所有子节点，从那些子节点中的父节点集合移除该节点
            foreach (var snc in remoteNode.SuccessorNodes)
            {
                var sCType = snc.Key; // 连接类型
                for (int i = 0; i < snc.Value.Count; i++)
                {
                    NodeModelBase? sNode = snc.Value[i];
                    remoteNode.SuccessorNodes[sCType].RemoveAt(i);
                    OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(remoteNode.Guid,
                                                                    sNode.Guid,
                                                                    sCType,
                                                                    NodeConnectChangeEventArgs.ChangeTypeEnum.Remote)); // 通知UI

                }
            }

            // 从集合中移除节点
            Nodes.Remove(nodeGuid);
            OnNodeRemote?.Invoke(new NodeRemoteEventArgs(nodeGuid));
        }

        /// <summary>
        /// 设置起点控件
        /// </summary>
        /// <param name="newNodeGuid"></param>
        public void SetStartNode(string newNodeGuid)
        {
            if(Nodes.TryGetValue(newNodeGuid, out NodeModelBase? newStartNodeModel))
            {
                if(newStartNodeModel != null)
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
                    foreach(var md in itemMethodDetails)
                    {
                        // var instanceType =  
                        // Activator.CreateInstance(md.ActingInstanceType);
                        // SereinIoc.RegisterInstantiate(md.ActingInstance);
                        SereinIoc.Register(md.ActingInstanceType);
                    }
                    methodDetails.AddRange(itemMethodDetails);
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
                                                                    NodeConnectChangeEventArgs.ChangeTypeEnum.Create)); // 通知UI
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

        #region 视觉元素交互

        #region 本地交互（WPF）

        #endregion

        #region 网络交互（通过Socket的方式进行操作）

        #endregion


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
    }



}
