using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Tool;
using System.Collections.Concurrent;

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
        /// <param name="RemoteEnvControl">连接到远程环境后，本地环境自动切换到对应的环境实体</param>
        /// <param name="uIContextOperation">远程环境下需要操作UI线程时，所提供的线程上下文封装工具</param>
        public RemoteFlowEnvironment(RemoteEnvControl RemoteEnvControl, UIContextOperation uIContextOperation)
        {
            this.UIContextOperation = uIContextOperation;
            remoteEnvControl = RemoteEnvControl;
            msgClient = new MsgControllerOfClient(this, RemoteEnvControl.SendAsync);
            RemoteEnvControl.EnvClient.MsgHandleHelper.AddModule(msgClient, (ex, send) =>
            {
                Console.WriteLine(ex);
            });
        }

        //private readonly Func<string, object?, Task> SendCommandAsync; 
        private readonly RemoteEnvControl remoteEnvControl;
        private readonly MsgControllerOfClient msgClient;
        private readonly ConcurrentDictionary<string, MethodDetails> MethodDetailss = [];

        /// <summary>
        /// 环境加载的节点集合
        /// Node Guid - Node Model
        /// </summary>
        private Dictionary<string, NodeModelBase> NodeModels { get; } = [];

        public event LoadDllHandler OnDllLoad;
        public event ProjectLoadedHandler OnProjectLoaded;
        public event NodeConnectChangeHandler OnNodeConnectChange;
        public event NodeCreateHandler OnNodeCreate;
        public event NodeRemoveHandler OnNodeRemove;
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

        public bool IsLcR => true;

        public bool IsRcL => false;

        public RunState FlowState { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public RunState FlipFlopState { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public IFlowEnvironment CurrentEnv => this;
        public UIContextOperation UIContextOperation { get; }
        public void SetConsoleOut()
        {
            var logTextWriter = new LogTextWriter(msg =>
            {
                OnEnvOut?.Invoke(msg);
            });
            Console.SetOut(logTextWriter);
        }

        public void WriteLineObjToJson(object obj)
        {
            Console.WriteLine("远程环境尚未实现的接口：WriteLineObjToJson");
        }

        public async Task StartRemoteServerAsync(int port = 7525)
        {
            await Console.Out.WriteLineAsync("远程环境尚未实现的接口：StartRemoteServerAsync");
        }

        public void StopRemoteServer()
        {
            Console.WriteLine("远程环境尚未实现的接口：StopRemoteServer");
        }

        public async Task<SereinProjectData> GetProjectInfoAsync()
        {
            var prjectInfo = await msgClient.SendAndWaitDataAsync<SereinProjectData>(EnvMsgTheme.GetProjectInfo); // 等待服务器返回项目信息
            return prjectInfo;
        }

        public void LoadProject(FlowEnvInfo flowEnvInfo, string filePath)
        {
            //Console.WriteLine("远程环境尚未实现的接口：LoadProject");

            // dll面板
            var libmds = flowEnvInfo.LibraryMds;
            foreach (var lib in libmds)
            {
                NodeLibrary nodeLibrary = new NodeLibrary
                {
                    FullName = lib.LibraryName,
                    FilePath = "Remote",
                };
                var mdInfos = lib.Mds.ToList();
                //OnDllLoad?.Invoke(new LoadDllEventArgs(nodeLibrary, mdInfos)); // 通知UI创建dll面板显示
                UIContextOperation?.Invoke(() => OnDllLoad?.Invoke(new LoadDllEventArgs(nodeLibrary, mdInfos))); // 通知UI创建dll面板显示
                foreach (var mdInfo in mdInfos)
                {
                    MethodDetailss.TryAdd(mdInfo.MethodName, new MethodDetails(mdInfo)); // 从DLL读取时生成元数据
                }
            }
            //flowSemaphore.

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
                    // 存在节点
                    UIContextOperation?.Invoke(() => OnNodeCreate?.Invoke(new NodeCreateEventArgs(childNode, true, item.region.Guid)));
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
                //OnNodeCreate?.Invoke(new NodeCreateEventArgs(item.nodeModel, item.position));
                UIContextOperation?.Invoke(() => OnNodeCreate?.Invoke(new NodeCreateEventArgs(item.nodeModel, item.position)));
            }



            // 确定节点之间的连接关系
            _ = Task.Run(async () =>
            {
                await Task.Delay(250);
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
                            //OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(fromNode.Guid,
                            //                                                toNode.Guid,
                            //                                                item.connectionType,
                            //                                                NodeConnectChangeEventArgs.ConnectChangeType.Create)); // 

                        }
                    }
                }
            });

            SetStartNode(projectData.StartNode);
            UIContextOperation?.Invoke(() =>
            {
                OnProjectLoaded?.Invoke(new ProjectLoadedEventArgs());
            });

        }
        private bool TryAddNode(NodeModelBase nodeModel)
        {
            //nodeModel.Guid ??= Guid.NewGuid().ToString();
            NodeModels[nodeModel.Guid] = nodeModel;

            // 如果是触发器，则需要添加到专属集合中
            //if (nodeModel is SingleFlipflopNode flipflopNode)
            //{
            //    var guid = flipflopNode.Guid;
            //    if (!FlipflopNodes.Exists(it => it.Guid.Equals(guid)))
            //    {
            //        FlipflopNodes.Add(flipflopNode);
            //    }
            //}
            return true;
        }

        private void ConnectNode(NodeModelBase fromNode, NodeModelBase toNode, ConnectionInvokeType connectionType)
        {
            if (fromNode is null || toNode is null || fromNode == toNode)
            {
                return;
            }

            var ToExistOnFrom = true;
            var FromExistInTo = true;
            ConnectionInvokeType[] ct = [ConnectionInvokeType.IsSucceed,
                                   ConnectionInvokeType.IsFail,
                                   ConnectionInvokeType.IsError,
                                   ConnectionInvokeType.Upstream];


            foreach (ConnectionInvokeType ctType in ct)
            {
                var FToTo = fromNode.SuccessorNodes[ctType].Where(it => it.Guid.Equals(toNode.Guid)).ToArray();
                var ToOnF = toNode.PreviousNodes[ctType].Where(it => it.Guid.Equals(fromNode.Guid)).ToArray();
                ToExistOnFrom = FToTo.Length > 0;
                FromExistInTo = ToOnF.Length > 0;
                if (ToExistOnFrom && FromExistInTo)
                {
                    Console.WriteLine("起始节点已与目标节点存在连接");
                    
                    //return;
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


                fromNode.SuccessorNodes[connectionType].Add(toNode); // 添加到起始节点的子分支
                toNode.PreviousNodes[connectionType].Add(fromNode); // 添加到目标节点的父分支
                OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(fromNode.Guid,
                                                                        toNode.Guid,
                                                                        JunctionOfConnectionType.Invoke,
                                                                        connectionType,
                                                                        NodeConnectChangeEventArgs.ConnectChangeType.Create)); // 通知UI
            }


            
        }



        public async Task<FlowEnvInfo> GetEnvInfoAsync()
        {

            var envInfo = await msgClient.SendAndWaitDataAsync<FlowEnvInfo>(EnvMsgTheme.GetEnvInfo);

            return envInfo;
        }



        public async Task<(bool, RemoteEnvControl)> ConnectRemoteEnv(string addres, int port, string token)
        {
            await Console.Out.WriteLineAsync("远程环境尚未实现的接口：ConnectRemoteEnv");
            return (false, null);
        }

        public void ExitRemoteEnv()
        {
            Console.WriteLine("远程环境尚未实现的接口：ExitRemoteEnv");
        }

        public void LoadDll(string dllPath)
        {
            // 将dll文件发送到远程环境，由远程环境进行加载
            Console.WriteLine("远程环境尚未实现的接口：LoadDll");
        }

        public bool RemoteDll(string assemblyFullName)
        {
            // 尝试移除远程环境中的加载了的依赖
            Console.WriteLine("远程环境尚未实现的接口：RemoteDll");
            return false;
        }

        public void ClearAll()
        {
            Console.WriteLine("远程环境尚未实现的接口：ClearAll");
        }

        public async Task StartAsync()
        {
            // 远程环境下不需要UI上下文
            await msgClient.SendAsync(EnvMsgTheme.StartFlow);
        }

        public async Task StartAsyncInSelectNode(string startNodeGuid)
        {
            _  = msgClient.SendAsync(EnvMsgTheme.StartFlowInSelectNode, new
            {
                nodeGuid = startNodeGuid
            });
        }

        public async void ExitFlow()
        {
            await msgClient.SendAsync(EnvMsgTheme.ExitFlow, null);
        }

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

        
        public void SetStartNode(string nodeGuid)
        {
            _ = msgClient.SendAsync(EnvMsgTheme.SetStartNode, new
            {
                nodeGuid
            });
            //UIContextOperation?.Invoke(() => OnStartNodeChange?.Invoke(new StartNodeChangeEventArgs(nodeGuid,nodeGuid)));
        }

        public async Task<object> InvokeNodeAsync(string nodeGuid)
        {
            Console.WriteLine("远程环境尚未实现接口 InvokeNodeAsync");
            _ = msgClient.SendAsync(EnvMsgTheme.SetStartNode, new
            {
                nodeGuid
            });
            return null;
        }

        public async Task<bool> ConnectNodeAsync(string fromNodeGuid,
                                                 string toNodeGuid,
                                                 JunctionType fromNodeJunctionType,
                                                 JunctionType toNodeJunctionType,
                                                 ConnectionInvokeType connectionType,
                                                 int argIndex = 0)
        {
            var result = await msgClient.SendAndWaitDataAsync<bool>(EnvMsgTheme.ConnectNode, new
            {
                fromNodeGuid,
                toNodeGuid,
                fromNodeJunctionType = fromNodeJunctionType.ToString(),
                toNodeJunctionType = toNodeJunctionType.ToString(),
                connectionType = connectionType.ToString(),
            });
            if (result)
            {
                OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(fromNodeGuid,
                                                                            toNodeGuid,
                                                                            JunctionOfConnectionType.Invoke,
                                                                            connectionType,
                                                                            NodeConnectChangeEventArgs.ConnectChangeType.Create)); // 通知UI
            }
            return result;
        }

        public async Task<NodeInfo> CreateNodeAsync(NodeControlType nodeControlType, PositionOfUI position, MethodDetailsInfo methodDetailsInfo = null)
        {
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

            // 通知UI更改
            UIContextOperation.Invoke(() =>
            {
                OnNodeCreate?.Invoke(new NodeCreateEventArgs(nodeModel, position));
            });
            return nodeInfo;
        }

        public async Task<bool> RemoveConnectAsync(string fromNodeGuid, string toNodeGuid, ConnectionInvokeType connectionType)
        {
            var result = await msgClient.SendAndWaitDataAsync<bool>(EnvMsgTheme.RemoveConnect, new
            {
                fromNodeGuid,
                toNodeGuid,
                connectionType = connectionType.ToString(),
            });
            if (result)
            {
                UIContextOperation.Invoke(() =>
                {
                    OnNodeConnectChange?.Invoke(new NodeConnectChangeEventArgs(fromNodeGuid,
                                                                            toNodeGuid,
                                                                            JunctionOfConnectionType.Invoke,
                                                                            connectionType,
                                                                            NodeConnectChangeEventArgs.ConnectChangeType.Remote));
                });
            }
            return result;
        }

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
                Console.WriteLine("删除失败");
            }
            return result;
        }

        public void ActivateFlipflopNode(string nodeGuid)
        {
            _ = msgClient.SendAsync(EnvMsgTheme.ActivateFlipflopNode, new
            {
                nodeGuid
            });
        }

        public void TerminateFlipflopNode(string nodeGuid)
        {
            _ = msgClient.SendAsync(EnvMsgTheme.TerminateFlipflopNode, new
            {
                nodeGuid
            });
        }

        public async Task<bool> SetNodeInterruptAsync(string nodeGuid, InterruptClass interruptClass)
        {
            var state = await msgClient.SendAndWaitDataAsync<bool>(EnvMsgTheme.SetNodeInterrupt, // 设置节点中断
                                                       new
                                                       {
                                                           nodeGuid,
                                                           interruptClass = interruptClass.ToString(),
                                                       });
            return state;
        }


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

        public void SetMonitorObjState(string key, bool isMonitor)
        {
            Console.WriteLine("远程环境尚未实现的接口：SetMonitorObjState");
        }

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


        public async Task<ChannelFlowInterrupt.CancelType> GetOrCreateGlobalInterruptAsync()
        {
            await Console.Out.WriteLineAsync("远程环境尚未实现的接口：GetOrCreateGlobalInterruptAsync");
            return ChannelFlowInterrupt.CancelType.Error;
        }

        public bool TryGetMethodDetailsInfo(string methodName, out MethodDetailsInfo mdInfo)
        {
            Console.WriteLine("远程环境尚未实现的接口：TryGetMethodDetailsInfo");
            mdInfo = null;
            return false;
        }

        public bool TryGetDelegateDetails(string methodName, out DelegateDetails del)
        {
            Console.WriteLine("远程环境尚未实现的接口：TryGetDelegateDetails");
            del = null;
            return false;
        }



        public void MonitorObjectNotification(string nodeGuid, object monitorData, MonitorObjectEventArgs.ObjSourceType sourceType)
        {
            Console.WriteLine("远程环境尚未实现的接口：MonitorObjectNotification");

        }

        public void TriggerInterrupt(string nodeGuid, string expression, InterruptTriggerEventArgs.InterruptTriggerType type)
        {
            Console.WriteLine("远程环境尚未实现的接口：TriggerInterrupt");
        }

        public void NodeLocated(string nodeGuid)
        {
            //Console.WriteLine("远程环境尚未实现的接口：NodeLocated");
            UIContextOperation?.Invoke(() => OnNodeLocated?.Invoke(new NodeLocatedEventArgs(nodeGuid)));
        }

        public async Task NotificationNodeValueChangeAsync(string nodeGuid, string path, object value)
        {
            //Console.WriteLine($"通知远程环境修改节点数据：{nodeGuid},name:{path},value:{value}");
            _  = msgClient.SendAsync(EnvMsgTheme.ValueNotification, new
            {
                nodeGuid = nodeGuid,
                path = path,
                value = value.ToString(),
            });

        }
    }
}
