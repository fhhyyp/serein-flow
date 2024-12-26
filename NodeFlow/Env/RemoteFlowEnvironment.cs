using Newtonsoft.Json.Linq;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.FlowNode;
using Serein.Library.Utils;
using Serein.NodeFlow.Tool;
using Serein.Script.Node;
using System.Collections.Concurrent;
using System.IO;
using System.Security.AccessControl;
using System.Threading.Channels;

namespace Serein.NodeFlow.Env
{
    /// <summary>
    /// 远程流程环境
    /// </summary>
    public class RemoteFlowEnvironment : ChannelFlowTrigger<string>, IFlowEnvironment 
    {
        /// <summary>
        /// 连接到远程环境后切换到的环境接口实现
        /// </summary>
        /// <param name="remoteMsgUtil">连接到远程环境后，本地环境自动切换到对应的环境实体</param>
        /// <param name="uIContextOperation">远程环境下需要操作UI线程时，所提供的线程上下文封装工具</param>
        public RemoteFlowEnvironment(RemoteMsgUtil remoteMsgUtil, UIContextOperation uIContextOperation)
        {
            this.UIContextOperation = uIContextOperation;
            RemoteMsgUtil = remoteMsgUtil;
            msgClient = new MsgControllerOfClient(this, remoteMsgUtil.SendAsync); // 这里提供的是主动发送消息的方法
            remoteMsgUtil.EnvClient.MsgHandleHelper.AddModule(msgClient, (ex, send) =>
            {
                Console.WriteLine(ex);
            });
        }

        //private readonly Func<string, object?, Task> SendCommandAsync; 
        private readonly RemoteMsgUtil RemoteMsgUtil;
        private readonly MsgControllerOfClient msgClient;
        private readonly ConcurrentDictionary<string, MethodDetails> MethodDetailss = [];

        /// <summary>
        /// 环境加载的节点集合
        /// Node Guid - Node Model
        /// </summary>
        private Dictionary<string, NodeModelBase> NodeModels { get; } = [];

        public event LoadDllHandler OnDllLoad;
        public event ProjectLoadedHandler OnProjectLoaded;
        /// <summary>
        /// 项目准备保存
        /// </summary>
        public event ProjectSavingHandler? OnProjectSaving;
        public event NodeConnectChangeHandler OnNodeConnectChange;
        public event NodeCreateHandler OnNodeCreate;
        public event NodeRemoveHandler OnNodeRemove;
        public event NodePlaceHandler OnNodePlace;
        public event NodeTakeOutHandler OnNodeTakeOut;
        public event StartNodeChangeHandler OnStartNodeChange;
        public event FlowRunCompleteHandler OnFlowRunComplete;
        public event MonitorObjectChangeHandler OnMonitorObjectChange;
        public event NodeInterruptStateChangeHandler OnNodeInterruptStateChange;
        public event ExpInterruptTriggerHandler OnInterruptTrigger;
        public event IOCMembersChangedHandler OnIOCMembersChanged;
        public event NodeLocatedHandler OnNodeLocated;
        public event NodeMovedHandler OnNodeMoved;
        public event EnvOutHandler OnEnvOut;

        public ISereinIOC IOC => throw new NotImplementedException();

        public string EnvName => FlowEnvironment.SpaceName;

        public bool IsGlobalInterrupt => false;

        public bool IsControlRemoteEnv => true;

        /// <summary>
        /// 信息输出等级
        /// </summary>
        public InfoClass InfoClass { get; set; }

        public RunState FlowState { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public RunState FlipFlopState { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public IFlowEnvironment CurrentEnv => this;
        public UIContextOperation UIContextOperation { get; }

        /// <summary>
        /// 标示是否正在加载项目
        /// </summary>
        private bool IsLoadingProject = false;
        /// <summary>
        /// 表示是否正在加载节点
        /// </summary>
        private bool IsLoadingNode = false;

        //public void SetConsoleOut()
        //{
        //    var logTextWriter = new LogTextWriter(msg =>
        //    {
        //        OnEnvOut?.Invoke(msg);
        //    });
        //    Console.SetOut(logTextWriter);
        //}

        /// <summary>
        /// 输出信息
        /// </summary>
        /// <param name="message">日志内容</param>
        /// <param name="type">日志类别</param>
        /// <param name="class">日志级别</param>
        public void WriteLine(InfoType type,  string message, InfoClass @class = InfoClass.Trivial)
        {
            OnEnvOut?.Invoke(type, message);
        }

        public async Task StartRemoteServerAsync(int port = 7525)
        {
            this.WriteLine(InfoType.INFO, "远程环境尚未实现的接口：StartRemoteServerAsync");
            await Task.CompletedTask;
        }

        public void StopRemoteServer()
        {
            this.WriteLine(InfoType.INFO, "远程环境尚未实现的接口：StopRemoteServer");
        }

        /// <summary>
        /// 获取远程环境
        /// </summary>
        /// <returns></returns>
        public async Task<SereinProjectData> GetProjectInfoAsync()
        {
            var projectData = await msgClient.SendAndWaitDataAsync<SereinProjectData>(EnvMsgTheme.GetProjectInfo); // 等待服务器返回项目信息
            return projectData;
        }

        /// <summary>
        /// 保存项目
        /// </summary>
        public void SaveProject()
        {
            OnProjectSaving?.Invoke(new ProjectSavingEventArgs());
        }


        /// <summary>
        /// 远程环境下加载项目
        /// </summary>
        /// <param name="flowEnvInfo"></param>
        /// <param name="filePath"></param>
        public void LoadProject(FlowEnvInfo flowEnvInfo, string filePath)
        {
            this.WriteLine(InfoType.INFO, "加载远程环境");
            IsLoadingProject = true;
            #region DLL功能区创建
            var libmds = flowEnvInfo.LibraryMds;
            foreach (var lib in libmds)
            {
                NodeLibraryInfo nodeLibraryInfo = new NodeLibraryInfo
                {
                    AssemblyName = lib.AssemblyName,
                    FilePath = "Remote",
                    FileName = "Remote",
                };
                var mdInfos = lib.Mds.ToList();
                UIContextOperation?.Invoke(() => OnDllLoad?.Invoke(new LoadDllEventArgs(nodeLibraryInfo, mdInfos))); // 通知UI创建dll面板显示
                foreach (var mdInfo in mdInfos)
                {
                    MethodDetailss.TryAdd(mdInfo.MethodName, new MethodDetails(mdInfo)); // 从DLL读取时生成元数据
                }
            }
            #endregion


            LoadNodeInfos(flowEnvInfo.Project.Nodes.ToList()); // 加载节点
            _ = SetStartNodeAsync(flowEnvInfo.Project.StartNode); // 设置流程起点
            UIContextOperation?.Invoke(() =>
            {
                OnProjectLoaded?.Invoke(new ProjectLoadedEventArgs()); // 加载完成
            });
            IsLoadingProject = false;

            #region 暂时注释
            /* #region 加载节点数据，如果是区域控件，提前加载区域
                var projectData = flowEnvInfo.Project;
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
                            MethodDetailss.TryGetValue(nodeInfo.MethodName, out methodDetails);// 加载远程环境时尝试获取方法信息
                        }

                        var nodeModel = FlowFunc.CreateNode(this, controlType, methodDetails); // 加载远程项目时创建节点
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
                #endregion

                #region 加载区域中的节点
                // 加载区域子项
                //foreach ((NodeModelBase region, string[] childNodeGuids) item in regionChildNodes)
                //{
                //    foreach (var childNodeGuid in item.childNodeGuids)
                //    {
                //        NodeModels.TryGetValue(childNodeGuid, out NodeModelBase? childNode);
                //        if (childNode is null)
                //        {
                //            // 节点尚未加载
                //            continue;
                //        }
                //        // 存在节点
                //        UIContextOperation?.Invoke(() => OnNodeCreate?.Invoke(new NodeCreateEventArgs(childNode, true, item.region.Guid)));
                //    }
                //}
                #endregion

                #region 加载普通的节点
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
                    //OnNodeCreate?.Invoke(new NodeCreateEventArgs(item.nodeModel, item.position));
                    UIContextOperation?.Invoke(() => OnNodeCreate?.Invoke(new NodeCreateEventArgs(item.nodeModel, item.position)));
                }
                #endregion

                #region 确定节点之间的连接关系
                _ = Task.Run(async () =>
                    {
                        await Task.Delay(500);
                        #region 连接节点的调用关系
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
                                    UIContextOperation?.Invoke(() => OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(fromNode.Guid,
                                                                               toNode.Guid,
                                                                               JunctionOfConnectionType.Invoke,
                                                                               item.connectionType,
                                                                               NodeConnectChangeEventArgs.ConnectChangeType.Create))); // 通知UI连接节点
                                }
                            }
                        }
                        #endregion

                        #region 连接节点的传参关系
                        foreach (var toNode in NodeModels.Values)
                        {
                            if(toNode.MethodDetails.ParameterDetailss is null)
                            {
                                continue;
                            }
                            for (var i = 0; i < toNode.MethodDetails.ParameterDetailss.Length; i++)
                            {
                                var pd = toNode.MethodDetails.ParameterDetailss[i];
                                if (!string.IsNullOrEmpty(pd.ArgDataSourceNodeGuid)
                                    && NodeModels.TryGetValue(pd.ArgDataSourceNodeGuid, out var fromNode))
                                {
                                    UIContextOperation?.Invoke(() =>
                                        OnNodeConnectChange?.Invoke(
                                        new NodeConnectChangeEventArgs(
                                            fromNode.Guid, // 从哪个节点开始
                                            toNode.Guid, // 连接到那个节点
                                            JunctionOfConnectionType.Arg,
                                            (int)pd.Index, // 连接线的样式类型
                                            pd.ArgDataSourceType,
                                            NodeConnectChangeEventArgs.ConnectChangeType.Create // 是创建连接还是删除连接
                                        ))); // 通知UI 
                                }
                            }
                        }
                        #endregion
                    }); 
                #endregion*/
            #endregion

        }

        /// <summary>
        /// 从远程环境获取项目信息
        /// </summary>
        /// <returns></returns>
        public async Task<FlowEnvInfo> GetEnvInfoAsync()
        {
            var envInfo = await msgClient.SendAndWaitDataAsync<FlowEnvInfo>(EnvMsgTheme.GetEnvInfo);
            return envInfo;
        }

        /// <summary>
        /// 连接到远程环境
        /// </summary>
        /// <param name="addres"></param>
        /// <param name="port"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<(bool, RemoteMsgUtil)> ConnectRemoteEnv(string addres, int port, string token)
        {
            await Console.Out.WriteLineAsync("远程环境尚未实现的接口：ConnectRemoteEnv");
            return (false, null);
        }
        
        /// <summary>
        /// 退出远程环境
        /// </summary>
        public void ExitRemoteEnv()
        {
            this.WriteLine(InfoType.INFO, "远程环境尚未实现的接口：ExitRemoteEnv");
        }
        /// <summary>
        /// （待更新）加载类库
        /// </summary>
        /// <param name="dllPath"></param>
        public void LoadLibrary(string dllPath)
        {
            // 将dll文件发送到远程环境，由远程环境进行加载
            this.WriteLine(InfoType.INFO, "远程环境尚未实现的接口：LoadDll");
        }
        /// <summary>
        /// （待更新）卸载类库
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        public bool TryUnloadLibrary(string assemblyName)
        {
            // 尝试移除远程环境中的加载了的依赖
            this.WriteLine(InfoType.INFO, "远程环境尚未实现的接口：RemoteDll");
            return false;
        }
        /// <summary>
        /// 启动远程环境的流程
        /// </summary>
        /// <returns></returns>
        public async Task<bool> StartFlowAsync()
        {
            // 远程环境下不需要UI上下文
            var result = await msgClient.SendAndWaitDataAsync<bool>(EnvMsgTheme.StartFlow);
            return result;
        }
        /// <summary>
        /// 从选定的节点开始运行
        /// </summary>
        /// <param name="startNodeGuid"></param>
        /// <returns></returns>
        public async Task<bool> StartAsyncInSelectNode(string startNodeGuid)
        {
            var result = await msgClient.SendAndWaitDataAsync<bool>(EnvMsgTheme.StartFlowInSelectNode, new
            {
                nodeGuid = startNodeGuid
            });
            return result;
        }
        /// <summary>
        /// 结束远程环境的流程运行
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ExitFlowAsync()
        {
            var result =  await msgClient.SendAndWaitDataAsync<bool>(EnvMsgTheme.ExitFlow, null);
            return result;
        }

        /// <summary>
        /// 移动节点，通知远程环境也一起移动，保持相对位置一致
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public void MoveNode(string nodeGuid, double x, double y)
        {
            //UIContextOperation?.Invoke(() =>
            //{
            //    OnNodeMoved?.Invoke(new NodeMovedEventArgs(nodeGuid, x, y));
            //});
            _ = msgClient.SendAsync(EnvMsgTheme.MoveNode,
                    new
                    {
                        nodeGuid,
                        x,
                        y
                    });

            if(NodeModels.TryGetValue(nodeGuid, out var nodeModel))
            {
                nodeModel.Position.X = x;
                nodeModel.Position.Y = y;
            }
        }


        /// <summary>
        /// 设置远程环境的流程起点节点
        /// </summary>
        /// <param name="nodeGuid">尝试设置为起始节点的节点Guid</param>
        /// <returns>被设置为起始节点的Guid</returns>
        public async Task<string> SetStartNodeAsync(string nodeGuid)
        {
            var newNodeGuid = await msgClient.SendAndWaitDataAsync<string>(EnvMsgTheme.SetStartNode, new
            {
                nodeGuid
            });
            if (NodeModels.TryGetValue(newNodeGuid, out var nodeModel)) // 存在节点
            {
                UIContextOperation?.Invoke(() => OnStartNodeChange?.Invoke(new StartNodeChangeEventArgs(nodeGuid, newNodeGuid)));
            }
            return newNodeGuid;
        }

       
        /// <summary>
        /// 在两个节点之间创建方法调用关系
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
            if (fromNodeJunctionType == JunctionType.Execute)
            {
                if (toNodeJunctionType == JunctionType.NextStep)
                {
                    (fromNodeGuid, toNodeGuid) = (toNodeGuid, fromNodeGuid);// 需要反转
                }
                else
                {
                    return false;  // 非预期的控制点连接
                }
            }
            else if (fromNodeJunctionType == JunctionType.NextStep)
            {
                if (toNodeJunctionType == JunctionType.Execute)
                {
                    // 顺序正确无须反转
                }
                else
                {
                    return false;  // 非预期的控制点连接
                }
            }
            else // 其它类型的控制点，排除
            {
                return false;  // 非预期的控制点连接
            }


            var sendObj = new
            {
                fromNodeGuid = fromNodeGuid,
                toNodeGuid = toNodeGuid,
                fromJunctionType = fromNodeJunctionType.ToString(),
                toJunctionType = toNodeJunctionType.ToString(),
                invokeType = invokeType.ToString(),
            };
            var result = await msgClient.SendAndWaitDataAsync<bool>(EnvMsgTheme.ConnectInvokeNode, sendObj);
            if (result)
            {
                OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(fromNodeGuid,
                                                                            toNodeGuid,
                                                                            JunctionOfConnectionType.Invoke,
                                                                            invokeType,
                                                                            NodeConnectChangeEventArgs.ConnectChangeType.Create)); // 通知UI
            }
            return result;
        }

        /// <summary>
        /// 在两个节点之间创建参数传递关系
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
                                                 int argIndex = 0)
        {

            // 正确的顺序：起始节点[返回值控制点] 向 目标节点[入参控制点] 发起连接
            //Console.WriteLine();
            //Console.WriteLine($"起始节点：{fromNodeGuid}");
            //Console.WriteLine($"目标节点：{toNodeGuid}");
            //Console.WriteLine($"链接请求：{(fromNodeJunctionType, toNodeJunctionType)}");
            //Console.WriteLine((fromNodeJunctionType, toNodeJunctionType));

            if (fromNodeJunctionType == JunctionType.ArgData)
            {
                if (toNodeJunctionType == JunctionType.ReturnData)
                {
                    (fromNodeGuid, toNodeGuid) = (toNodeGuid, fromNodeGuid);// 需要反转
                }
                else
                {
                    return false;  // 非预期的控制点连接
                }
            }
            else if (fromNodeJunctionType == JunctionType.ReturnData)
            {
                if (toNodeJunctionType == JunctionType.ArgData)
                {
                    // 顺序正确无须反转
                }
                else
                {
                    return false;  // 非预期的控制点连接
                }
            }
            else // 其它类型的控制点，排除
            {
                return false;  // 非预期的控制点连接
            }

            var sendObj = new
            {
                fromNodeGuid = fromNodeGuid,
                toNodeGuid = toNodeGuid,
                fromJunctionType = fromNodeJunctionType.ToString(),
                toJunctionType = toNodeJunctionType.ToString(),
                argSourceType = argSourceType.ToString(),
                argIndex = argIndex,
            };
            var result = await msgClient.SendAndWaitDataAsync<bool>(EnvMsgTheme.ConnectArgSourceNode, sendObj);
            if (result)
            {
                OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(fromNodeGuid,
                                                                            toNodeGuid,
                                                                            JunctionOfConnectionType.Arg,
                                                                            argIndex,
                                                                            argSourceType,
                                                                            NodeConnectChangeEventArgs.ConnectChangeType.Create)); // 通知UI
            }
            return result;
        }

        /// <summary>
        /// （待更新）设置两个节点某个类型的方法调用关系为优先调用
        /// </summary>
        /// <param name="fromNodeGuid">起始节点</param>
        /// <param name="toNodeGuid">目标节点</param>
        /// <param name="connectionType">连接关系</param>
        /// <returns>是否成功调用</returns>
        public async Task<bool> SetConnectPriorityInvoke(string fromNodeGuid, string toNodeGuid, ConnectionInvokeType connectionType)
        {
            this.WriteLine(InfoType.WARN, "远程环境尚未实现的接口(重要，会尽快实现)：SetConnectPriorityInvoke");
            return false;
        }

        /// <summary>
        /// 移除两个节点之间的方法调用关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点</param>
        /// <param name="toNodeGuid">目标节点</param>
        /// <param name="invokeType">连接类型</param>
        public async Task<bool> RemoveConnectInvokeAsync(string fromNodeGuid, string toNodeGuid, ConnectionInvokeType invokeType)
        {
            var result = await msgClient.SendAndWaitDataAsync<bool>(EnvMsgTheme.RemoveInvokeConnect, new
            {
                fromNodeGuid = fromNodeGuid,
                toNodeGuid = toNodeGuid,
                invokeType = invokeType.ToString(),
            });
            if (result)
            {
                UIContextOperation.Invoke(() =>
                {
                    OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(fromNodeGuid,
                                                                            toNodeGuid,
                                                                            JunctionOfConnectionType.Invoke,
                                                                            invokeType,
                                                                            NodeConnectChangeEventArgs.ConnectChangeType.Remove));
                });
            }
            return result;
        }
        
        /// <summary>
        /// 移除连接节点之间参数传递的关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点Guid</param>
        /// <param name="toNodeGuid">目标节点Guid</param>
        /// <param name="argIndex">连接到第几个参数</param>
        public async Task<bool> RemoveConnectArgSourceAsync(string fromNodeGuid, string toNodeGuid, int argIndex)
        {
            var result = await msgClient.SendAndWaitDataAsync<bool>(EnvMsgTheme.RemoveArgSourceConnect, new
            {
                fromNodeGuid = fromNodeGuid,
                toNodeGuid = toNodeGuid,
                argIndex = argIndex,
            });
            if (result)
            {
                UIContextOperation.Invoke(() =>
                {
                    OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(fromNodeGuid,
                                                                               toNodeGuid,
                                                                               JunctionOfConnectionType.Arg,
                                                                               argIndex,
                                                                               ConnectionArgSourceType.GetPreviousNodeData,
                                                                               NodeConnectChangeEventArgs.ConnectChangeType.Remove)); // 通知UI
                });
            }
            return result;
        }


        /// <summary>
        /// 从节点信息集合批量加载节点控件
        /// </summary>
        /// <param name="nodeInfos">节点信息</param>
        /// <returns></returns>
        public async Task LoadNodeInfosAsync(List<NodeInfo> nodeInfos)
        {
            if (IsLoadingProject || IsLoadingNode)
            {
                return;
            }
            
            List<NodeInfo> loadSuuccessNodes = new List<NodeInfo>(); // 加载成功的节点信息
            List<NodeInfo> loadFailureNodes = new List<NodeInfo>(); // 加载失败的节点信息
            List<NodeInfo> needPlaceNodeInfos = new List<NodeInfo>(); // 需要重新放置的节点

            #region 尝试从节点信息加载节点
            foreach (NodeInfo? nodeInfo in nodeInfos)
            {
                if (!EnumHelper.TryConvertEnum<NodeControlType>(nodeInfo.Type, out var controlType))
                {
                    continue;
                }
                NodeInfo newNodeInfo;
                try
                {
                    if (!string.IsNullOrEmpty(nodeInfo.MethodName))
                    {
                        if (!MethodDetailss.TryGetValue(nodeInfo.MethodName, out var methodDetails))
                        {
                            loadFailureNodes.Add(nodeInfo);
                            continue; // 有方法名称，但本地没有缓存的相关方法信息，跳过
                        }
                        // 加载远程环境时尝试获取方法信息
                        newNodeInfo = await CreateNodeAsync(controlType, nodeInfo.Position, methodDetails.ToInfo());
                    }
                    else
                    {
                        newNodeInfo = await CreateNodeAsync(controlType, nodeInfo.Position);
                    }
                    loadSuuccessNodes.Add(nodeInfo);
                }
                catch (Exception ex)
                {
                    SereinEnv.WriteLine(ex);
                    loadFailureNodes.Add(nodeInfo);
                    continue; // 跳过加载失败的节点
                }
            } 
            #endregion

            // 远程环境无法加载的节点，输出信息
            foreach (var f_node in loadFailureNodes) 
            {
                SereinEnv.WriteLine(InfoType.INFO, "无法加载的节点Guid:" + f_node.Guid);
            }

            #region 尝试重新放置节点的位置
            // 判断加载的节点是否需要放置在容器中
            foreach (var nodeInfo in loadSuuccessNodes)
            {
                if (!string.IsNullOrEmpty(nodeInfo.ParentNodeGuid) &&
                    NodeModels.TryGetValue(nodeInfo.ParentNodeGuid, out var parentNode))
                {
                    needPlaceNodeInfos.Add(nodeInfo); // 需要重新放置的节点
                }
            }
            loadSuuccessNodes.Clear();
            loadFailureNodes.Clear();
            foreach (var nodeInfo in needPlaceNodeInfos)
            {
                // 通知远程调整节点放置位置
                var isSuuccess = await PlaceNodeToContainerAsync(nodeInfo.Guid, nodeInfo.ParentNodeGuid);
                if (isSuuccess)
                {
                    loadSuuccessNodes.Add(nodeInfo);
                }
                else
                {
                    loadFailureNodes.Add(nodeInfo);
                }
            } 
            #endregion

            foreach (var f_node in loadFailureNodes)
            {
                SereinEnv.WriteLine(InfoType.INFO, $"无法移动到指定容器的节点Guid ：{f_node.Guid}" +
                    $"{Environment.NewLine}容器节点Guid{f_node.ParentNodeGuid}{Environment.NewLine}" );
            }

        }

        /// <summary>
        /// 创建节点/区域/基础控件
        /// </summary>
        /// <param name="nodeType">节点/区域/基础控件类型</param>
        /// <param name="position">节点在画布上的位置（</param>
        /// <param name="methodDetailsInfo">节点绑定的方法说明</param>
        public async Task<NodeInfo> CreateNodeAsync(NodeControlType nodeControlType, 
            PositionOfUI position,
            MethodDetailsInfo methodDetailsInfo = null)
        {
            IsLoadingNode = true;
            var nodeInfo = await msgClient.SendAndWaitDataAsync<NodeInfo>(EnvMsgTheme.CreateNode, new
            {
                nodeType = nodeControlType.ToString(),
                position = position,
                mdInfo = methodDetailsInfo,
            });
            
            MethodDetails? methodDetails = null;
            if (!string.IsNullOrEmpty(nodeInfo.MethodName))
            {
                MethodDetailss.TryGetValue(nodeInfo.MethodName, out methodDetails);// 加载远程环境时尝试获取方法信息
            }

            //MethodDetailss.TryGetValue(methodDetailsInfo.MethodName, out var methodDetails);// 加载项目时尝试获取方法信息
            var nodeModel = FlowFunc.CreateNode(this, nodeControlType, methodDetails); // 远程环境下加载节点
            nodeModel.LoadInfo(nodeInfo);
            TryAddNode(nodeModel);
            IsLoadingNode = false;

            // 通知UI更改
            UIContextOperation.Invoke(() =>
            {
                OnNodeCreate?.Invoke(new NodeCreateEventArgs(nodeModel, position));
            });
            return nodeInfo;
        }



        /// <summary>
        /// 将节点放置在容器中
        /// </summary>
        /// <returns></returns>
        public async Task<bool> PlaceNodeToContainerAsync(string nodeGuid, string containerNodeGuid)
        {
            var isSuuccess = await msgClient.SendAndWaitDataAsync<bool>(EnvMsgTheme.PlaceNode, new
            {
                nodeGuid = nodeGuid,
                containerNodeGuid = containerNodeGuid,
            });
            if (isSuuccess)
            {
                var nodeModel = GuidToModel(nodeGuid); // 获取目标节点
                if (nodeModel is null) return false; 
                var containerNode = GuidToModel(containerNodeGuid); // 获取容器节点
                if (containerNode is not INodeContainer nodeContainer) return false;
                var result = nodeContainer.PlaceNode(nodeModel);
                if (result)
                {
                    // 通知UI更改
                    UIContextOperation.Invoke(() =>
                    {
                        OnNodePlace?.Invoke(new NodePlaceEventArgs(nodeGuid, containerNodeGuid)); // 通知UI更改节点放置位置
                    });
                }
                return result;
            }
            return isSuuccess;
        }

        /// <summary>
        /// 将节点从容器中脱离
        /// </summary>
        /// <returns></returns>
        public async Task<bool> TakeOutNodeToContainerAsync(string nodeGuid)
        {
            var isSuuccess = await msgClient.SendAndWaitDataAsync<bool>(EnvMsgTheme.TakeOutNode, new
            {
                nodeGuid = nodeGuid,
            });
            if (isSuuccess) 
            {
                var nodeModel = GuidToModel(nodeGuid); // 获取目标节点
                if (nodeModel is null) return false;
                if (nodeModel.ContainerNode is not INodeContainer nodeContainer)
                {
                    return false;
                }
                var result = nodeContainer.TakeOutNode(nodeModel); // 从容器节点取出
                if (result)
                {
                    // 通知UI更改
                    UIContextOperation.Invoke(() =>
                    {
                        OnNodeTakeOut?.Invoke(new NodeTakeOutEventArgs(nodeGuid)); // 重新放置在画布上
                    });
                }
                return result;
            }
            return isSuuccess;
        }



        /// <summary>
        /// 移除远程环境的某个节点
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <returns></returns>
        public async Task<bool> RemoveNodeAsync(string nodeGuid)
        {
            var result = await msgClient.SendAndWaitDataAsync<bool>(EnvMsgTheme.RemoveNode, new
            {
                nodeGuid
            });
            if (result)
            {
                UIContextOperation.Invoke(() =>
                {
                    OnNodeRemove?.Invoke(new NodeRemoveEventArgs(nodeGuid));
                });
            }
            else
            {
                this.WriteLine(InfoType.ERROR, "删除失败");
            }
            return result;
        }

        /// <summary>
        /// 激活远程某个全局触发器节点
        /// </summary>
        /// <param name="nodeGuid"></param>
        public void ActivateFlipflopNode(string nodeGuid)
        {
            // 需要重写
            _ = msgClient.SendAsync(EnvMsgTheme.ActivateFlipflopNode, new
            {
                nodeGuid
            });
        }

        /// <summary>
        /// 暂停远程某个全局触发器节点
        /// </summary>
        /// <param name="nodeGuid"></param>
        public void TerminateFlipflopNode(string nodeGuid)
        {
            // 需要重写
            _ = msgClient.SendAsync(EnvMsgTheme.TerminateFlipflopNode, new
            {
                nodeGuid
            });
        }

        /// <summary>
        /// 设置远程环境某个节点的中断
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="isInterrupt"></param>
        /// <returns></returns>
        public async Task<bool> SetNodeInterruptAsync(string nodeGuid, bool isInterrupt)
        {
            var state = await msgClient.SendAndWaitDataAsync<bool>(EnvMsgTheme.SetNodeInterrupt, // 设置节点中断
                                                       new
                                                       {
                                                           nodeGuid,
                                                           isInterrupt,
                                                       });
            return state;
        }

        /// <summary>
        /// 为远程某个节点添加中断的表达式
        /// </summary>
        /// <param name="key"></param>
        /// <param name="expression"></param>
        /// <returns></returns>
        public async Task<bool> AddInterruptExpressionAsync(string key, string expression)
        {
            var state = await msgClient.SendAndWaitDataAsync<bool>(EnvMsgTheme.AddInterruptExpression,  // 设置节点/对象的中断表达式
                                                       new
                                                       {
                                                           key,
                                                           expression,
                                                       });
            return state;
        }

        /// <summary>
        /// 检查并获取节点/对象是否正在监视、以及监视的表达式（需要重写）
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<(bool, string[])> CheckObjMonitorStateAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                var exps = Array.Empty<string>();
                return (false, exps);
            }
            else
            {
                var result = await msgClient.SendAndWaitDataAsync<(bool, string[])>(EnvMsgTheme.SetNodeInterrupt, // 检查并获取节点/对象是否正在监视、以及监视的表达式
                                                                        new
                                                                        {
                                                                            key,
                                                                        });
                return result;
            }

        }



        /// <summary>
        /// 需要定位某个节点
        /// </summary>
        /// <param name="nodeGuid"></param>
        public void NodeLocated(string nodeGuid)
        {
            UIContextOperation?.Invoke(() => OnNodeLocated?.Invoke(new NodeLocatedEventArgs(nodeGuid)));
        }

        /// <summary>
        /// 通知远程环境修改节点数据
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="path"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task NotificationNodeValueChangeAsync(string nodeGuid, string path, object value)
        {
            if(IsLoadingProject || IsLoadingNode)
            {
                return;
            }
            //this.WriteLine(InfoType.INFO, $"通知远程环境修改节点数据：{nodeGuid},name:{path},value:{value}");
            await msgClient.SendAsync(EnvMsgTheme.ValueNotification, new
            {
                nodeGuid = nodeGuid,
                path = path,
                value = value.ToString(),
            });

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
            if (IsLoadingProject || IsLoadingNode)
            {
                return false;
            }
            if (!NodeModels.TryGetValue(nodeGuid,out var nodeModel))
            {
                return false;
            }
            //this.WriteLine(InfoType.INFO, $"通知远程环境修改节点可选数据：{nodeGuid},isAdd:{isAdd},paramIndex:{paramIndex}");
            var result = await msgClient.SendAndWaitDataAsync<bool>(EnvMsgTheme.ChangeParameter, new
            {
                nodeGuid = nodeGuid,
                isAdd = isAdd,
                paramIndex = paramIndex,
            });
            if (result) {
                if (isAdd)
                {
                    nodeModel.MethodDetails.AddParamsArg(paramIndex);
                }
                else
                {
                    nodeModel.MethodDetails.RemoveParamsArg(paramIndex);
                }
            }
            return result;
        }



        #region 私有方法

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
        private bool TryAddNode(NodeModelBase nodeModel)
        {
            NodeModels[nodeModel.Guid] = nodeModel;
            return true;
        }

        /// <summary>
        /// 私有方法，通过节点信息集合加载节点
        /// </summary>
        /// <param name="nodeInfos"></param>
        private void LoadNodeInfos(List<NodeInfo> nodeInfos)
        {
            #region 从NodeInfo创建NodeModel
            foreach (NodeInfo? nodeInfo in nodeInfos)
            {
                if (!EnumHelper.TryConvertEnum<NodeControlType>(nodeInfo.Type, out var controlType))
                {
                    continue;
                }

                #region 获取方法描述
                MethodDetails? methodDetails = null;
                if (string.IsNullOrEmpty(nodeInfo.MethodName))
                {
                    methodDetails = new MethodDetails();
                }
                else
                {
                    if (string.IsNullOrEmpty(nodeInfo.MethodName))
                    {
                        continue;
                    }
                    MethodDetailss.TryGetValue(nodeInfo.MethodName, out methodDetails);// 加载远程环境时尝试获取方法信息
                }
                #endregion

                var nodeModel = FlowFunc.CreateNode(this, controlType, methodDetails); // 加载项目时创建节点
                if (nodeModel is null)
                {
                    nodeInfo.Guid = string.Empty;
                    continue;
                }
                nodeModel.LoadInfo(nodeInfo); // 创建节点model
                TryAddNode(nodeModel); // 加载项目时将节点加载到环境中
                
                UIContextOperation?.Invoke(() =>
                    OnNodeCreate?.Invoke(new NodeCreateEventArgs(nodeModel, nodeInfo.Position))); // 添加到UI上
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
                if (NodeModels.TryGetValue(nodeInfo.Guid, out var childNode) &&
                    NodeModels.TryGetValue(nodeInfo.ParentNodeGuid, out var parentNode))
                {
                    childNode.ContainerNode = parentNode;
                    parentNode.ChildrenNode.Add(childNode);
                    UIContextOperation?.Invoke(() => 
                        OnNodePlace?.Invoke(new NodePlaceEventArgs(nodeInfo.Guid, nodeInfo.ParentNodeGuid)) // 通知UI更改节点放置位置
                    );

                }
            }
            #endregion

            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                #region 确定节点之间的方法调用关系
                foreach (var nodeInfo in nodeInfos)
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
                            UIContextOperation?.Invoke(() => OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(fromNode.Guid,
                                                                                toNode.Guid,
                                                                                JunctionOfConnectionType.Invoke,
                                                                                item.connectionType,
                                                                                NodeConnectChangeEventArgs.ConnectChangeType.Create))); // 通知UI连接节点
                        }
                    }
                }
                #endregion

                #region 确定节点之间的参数调用关系
                foreach (var toNode in NodeModels.Values)
                {
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

                            UIContextOperation?.Invoke(() =>
                                         OnNodeConnectChange?.Invoke(
                                         new NodeConnectChangeEventArgs(
                                             fromNode.Guid, // 从哪个节点开始
                                             toNode.Guid, // 连接到那个节点
                                             JunctionOfConnectionType.Arg,
                                             (int)pd.Index, // 连接线的样式类型
                                             pd.ArgDataSourceType,
                                             NodeConnectChangeEventArgs.ConnectChangeType.Create // 是创建连接还是删除连接
                                         ))); // 通知UI 
                        }
                    }
                }
                #endregion 
            });
            UIContextOperation?.Invoke(() => OnProjectLoaded?.Invoke(new ProjectLoadedEventArgs()));
        }


        #endregion

        #region 远程环境下暂未实现的接口


        public void SetMonitorObjState(string key, bool isMonitor)
        {
            this.WriteLine(InfoType.INFO, "远程环境尚未实现的接口：SetMonitorObjState");
        }

        public async Task<object> InvokeNodeAsync(IDynamicContext context, string nodeGuid)
        {
            // 登录到远程环境后，启动器相关方法无效
            this.WriteLine(InfoType.INFO, "远程环境尚未实现接口 InvokeNodeAsync");
            return null;
        }

       

        public bool TryGetMethodDetailsInfo(string libraryName, string methodName, out MethodDetailsInfo mdInfo)
        {
            this.WriteLine(InfoType.INFO, "远程环境尚未实现的接口：TryGetMethodDetailsInfo");
            mdInfo = null;
            return false;
        }

        public bool TryGetDelegateDetails(string libraryName, string methodName, out DelegateDetails del)
        {
            this.WriteLine(InfoType.INFO, "远程环境尚未实现的接口：TryGetDelegateDetails");
            del = null;
            return false;
        }


        /// <summary>
        /// 对象监视表达式
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="monitorData"></param>
        /// <param name="sourceType"></param>
        public void MonitorObjectNotification(string nodeGuid, object monitorData, MonitorObjectEventArgs.ObjSourceType sourceType)
        {
            this.WriteLine(InfoType.INFO, "远程环境尚未实现的接口：MonitorObjectNotification");

        }
        /// <summary>
        /// 触发节点的中断
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="expression"></param>
        /// <param name="type"></param>
        public void TriggerInterrupt(string nodeGuid, string expression, InterruptTriggerEventArgs.InterruptTriggerType type)
        {
            this.WriteLine(InfoType.INFO, "远程环境尚未实现的接口：TriggerInterrupt");
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
            this.WriteLine(InfoType.INFO, "远程环境尚未实现的接口：LoadNativeLibraryOfRuning");
            return false;
        }

        /// <summary>
        /// 运行时加载指定目录下的类库
        /// </summary>
        /// <param name="path">目录</param>
        /// <param name="isRecurrence">是否递归加载</param>
        public void LoadAllNativeLibraryOfRuning(string path, bool isRecurrence = true)
        {
            this.WriteLine(InfoType.INFO, "远程环境尚未实现的接口：LoadAllNativeLibraryOfRuning");
        }



        /// <summary>
        /// 添加或更新全局数据
        /// </summary>
        /// <param name="keyName">数据名称</param>
        /// <param name="data">数据集</param>
        /// <returns></returns>
        public object AddOrUpdateGlobalData(string keyName, object data)
        {
            this.WriteLine(InfoType.INFO, "远程环境尚未实现的接口：AddOrUpdateGlobalData");
            return null;
        }
        /// <summary>
        /// 获取全局数据
        /// </summary>
        /// <param name="keyName">数据名称</param>
        /// <returns></returns>
        public object GetGlobalData(string keyName)
        {
            this.WriteLine(InfoType.INFO, "远程环境尚未实现的接口：GetGlobalData");
            return null;
        }


        #endregion




    }
}
