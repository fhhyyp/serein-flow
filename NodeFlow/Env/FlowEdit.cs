using Microsoft.CodeAnalysis;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Model.Operation;
using Serein.NodeFlow.Services;
using System.Diagnostics;
using System.Threading.Tasks;
using static Serein.Library.Api.IFlowEnvironment;
using IOperation = Serein.NodeFlow.Model.Operation.IOperation;

namespace Serein.NodeFlow.Env
{

    /// <summary>
    /// 流程编辑接口实现
    /// </summary>
    internal class FlowEdit : IFlowEdit
    {
        public FlowEdit(IFlowEnvironment flowEnvironment,
                        IFlowEnvironmentEvent flowEnvironmentEvent,
                        FlowLibraryService flowLibraryManagement,
                        FlowOperationService flowOperationService,
                        FlowModelService flowModelService,
                        UIContextOperation UIContextOperation,
                        ISereinIOC sereinIOC,
                        NodeMVVMService nodeMVVMService)
        {
            this.flowEnvironment = flowEnvironment;
            this.flowEnvironmentEvent = flowEnvironmentEvent;
            this.flowLibraryManagement = flowLibraryManagement;
            this.flowOperationService = flowOperationService;
            this.flowModelService = flowModelService;
            this.UIContextOperation = UIContextOperation;
            NodeMVVMManagement = nodeMVVMService;
            InitNodeMVVM(nodeMVVMService);
        }


        /// <inheritdoc/>
        public NodeMVVMService NodeMVVMManagement { get; }

        private readonly IFlowEnvironment flowEnvironment;
        private readonly IFlowEnvironmentEvent flowEnvironmentEvent;
        private readonly FlowLibraryService flowLibraryManagement;
        private readonly FlowOperationService flowOperationService;
        private readonly FlowModelService flowModelService;
        private readonly NodeMVVMService nodeMVVMService;


        /// <summary>
        /// 注册基本节点类型
        /// </summary>
        private void InitNodeMVVM(NodeMVVMService nodeMVVMService)
        {
            nodeMVVMService.RegisterModel(NodeControlType.UI, typeof(SingleUINode)); // 动作节点
            nodeMVVMService.RegisterModel(NodeControlType.Action, typeof(SingleActionNode)); // 动作节点
            nodeMVVMService.RegisterModel(NodeControlType.Flipflop, typeof(SingleFlipflopNode)); // 触发器节点
            nodeMVVMService.RegisterModel(NodeControlType.ExpOp, typeof(SingleExpOpNode)); // 表达式节点
            nodeMVVMService.RegisterModel(NodeControlType.ExpCondition, typeof(SingleConditionNode)); // 条件表达式节点
            nodeMVVMService.RegisterModel(NodeControlType.GlobalData, typeof(SingleGlobalDataNode));  // 全局数据节点
            nodeMVVMService.RegisterModel(NodeControlType.Script, typeof(SingleScriptNode)); // 脚本节点
            nodeMVVMService.RegisterModel(NodeControlType.NetScript, typeof(SingleNetScriptNode)); // 脚本节点
            nodeMVVMService.RegisterModel(NodeControlType.FlowCall, typeof(SingleFlowCallNode)); // 流程调用节点
        }


        private UIContextOperation UIContextOperation;



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
            return flowModelService.TryGetCanvasModel(nodeGuid, out canvasDetails);

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
            return flowModelService.TryGetNodeModel(nodeGuid, out nodeModel);

        }

        #region 私有方法

        /// <summary>
        /// 从节点信息创建节点，并返回状态指示是否创建成功
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <param name="nodeModel"></param>
        /// <returns></returns>
        private bool CreateNodeFromNodeInfo(NodeInfo nodeInfo, out IFlowNode? nodeModel)
        {
            if (!EnumHelper.TryConvertEnum<NodeControlType>(nodeInfo.Type, out var controlType))
            {
                nodeModel = null;
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
                    flowLibraryManagement.TryGetMethodDetails(nodeInfo.AssemblyName, nodeInfo.MethodName, out methodDetails); // 加载项目时尝试获取方法信息
                }
            }
            else if (controlType.IsBaseNode())
            {
                // 加载基础节点
                methodDetails = new MethodDetails();

            }
            else
            {
                if (string.IsNullOrEmpty(nodeInfo.MethodName))
                {
                    nodeModel = null;
                    return false;
                }
                // 加载方法节点
                flowLibraryManagement.TryGetMethodDetails(nodeInfo.AssemblyName, nodeInfo.MethodName, out methodDetails); // 加载项目时尝试获取方法信息
            }
            #endregion

            nodeModel = FlowNodeExtension.CreateNode(flowEnvironment, controlType, methodDetails); // 加载项目时创建节点
            if (nodeModel is null)
            {
                nodeInfo.Guid = string.Empty;
                return false;
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
            flowModelService.AddNodeModel(nodeModel);


            // 如果是触发器，则需要添加到专属集合中
            /*if (nodeModel is SingleFlipflopNode flipflopNode)
            {
                var guid = flipflopNode.Guid;
                if (!FlipflopNodes.Exists(it => it.Guid.Equals(guid)))
                {
                    FlipflopNodes.Add(flipflopNode);
                }
            }*/
            return true;
        }
        #endregion


        #region 流程接口

        private int _add_canvas_count = 1;



        public void CreateCanvas(string canvasName, int width, int height)
        {
            IOperation operation = new CreateCanvasOperation
            {
                CanvasInfo = new FlowCanvasDetailsInfo
                {
                    Name = $"Canvas {_add_canvas_count++}",
                    Width = width,
                    Height = height,
                    Guid = Guid.NewGuid().ToString(),
                    ScaleX = 1.0f,
                    ScaleY = 1.0f,
                }
            };

            flowOperationService.Execute(operation);
        }

        public void RemoveCanvas(string canvasGuid)
        {
            IOperation operation = new RemoveCanvasOperation
            {
                CanvasGuid = canvasGuid
            };
            flowOperationService.Execute(operation);
        }

        public void ConnectInvokeNode(string canvasGuid, string fromNodeGuid, string toNodeGuid, JunctionType fromNodeJunctionType, JunctionType toNodeJunctionType, ConnectionInvokeType invokeType)
        {
            IOperation operation = new ChangeNodeConnectionOperation
            {
                CanvasGuid = canvasGuid,
                FromNodeGuid = fromNodeGuid,
                ToNodeGuid = toNodeGuid,
                FromNodeJunctionType = fromNodeJunctionType,
                ToNodeJunctionType = toNodeJunctionType,
                ConnectionInvokeType = invokeType,
                ChangeType = NodeConnectChangeEventArgs.ConnectChangeType.Create,
                JunctionOfConnectionType = JunctionOfConnectionType.Invoke,
            };

            flowOperationService.Execute(operation);
        }

        public void ConnectArgSourceNode(string canvasGuid, string fromNodeGuid, string toNodeGuid, JunctionType fromNodeJunctionType, JunctionType toNodeJunctionType, ConnectionArgSourceType argSourceType, int argIndex)
        {
            IOperation operation = new ChangeNodeConnectionOperation
            {
                CanvasGuid = canvasGuid,
                FromNodeGuid = fromNodeGuid,
                ToNodeGuid = toNodeGuid,
                FromNodeJunctionType = fromNodeJunctionType,
                ToNodeJunctionType = toNodeJunctionType,
                ConnectionArgSourceType = argSourceType,
                ArgIndex = argIndex,
                ChangeType = NodeConnectChangeEventArgs.ConnectChangeType.Create,
                JunctionOfConnectionType = JunctionOfConnectionType.Arg,
            };
             flowOperationService.Execute(operation);
        }

        public void RemoveInvokeConnect(string canvasGuid, string fromNodeGuid, string toNodeGuid, ConnectionInvokeType connectionType)
        {
            IOperation operation = new ChangeNodeConnectionOperation
            {
                CanvasGuid = canvasGuid,
                FromNodeGuid = fromNodeGuid,
                ToNodeGuid = toNodeGuid,
                ConnectionInvokeType = connectionType,
                ChangeType = NodeConnectChangeEventArgs.ConnectChangeType.Remove,
            };
            flowOperationService.Execute(operation);
        }

        public void RemoveArgSourceConnect(string canvasGuid, string fromNodeGuid, string toNodeGuid, int argIndex)
        {
            IOperation operation = new ChangeNodeConnectionOperation
            {
                CanvasGuid = canvasGuid,
                FromNodeGuid = fromNodeGuid,
                ToNodeGuid = toNodeGuid,
                ArgIndex = argIndex,
                ChangeType = NodeConnectChangeEventArgs.ConnectChangeType.Remove
            };
            flowOperationService.Execute(operation);
        }

        public void CreateNode(string canvasGuid, NodeControlType nodeType, PositionOfUI position, MethodDetailsInfo methodDetailsInfo = null)
        {
            IOperation operation = new CreateNodeOperation
            {
                CanvasGuid = canvasGuid,
                NodeControlType = nodeType,
                Position = position,
                MethodDetailsInfo = methodDetailsInfo
            };
            flowOperationService.Execute(operation);
        }

        public void RemoveNode(string canvasGuid, string nodeGuid)
        {
            IOperation operation = new RemoveNodeOperation
            {
                CanvasGuid = canvasGuid,
                NodeGuid = nodeGuid
            };
            flowOperationService.Execute(operation);
        }

        public void PlaceNodeToContainer(string canvasGuid, string nodeGuid, string containerNodeGuid)
        {
            IOperation operation = new ContainerPlaceNodeOperation
            {
                CanvasGuid = canvasGuid,
                NodeGuid = nodeGuid,
                ContainerNodeGuid = containerNodeGuid
            };
            flowOperationService.Execute(operation);
        }

        public void TakeOutNodeToContainer(string canvasGuid, string nodeGuid)
        {
            IOperation operation = new ContainerTakeOutNodeOperation
            {
                CanvasGuid = canvasGuid,
                NodeGuid = nodeGuid,
            };
            flowOperationService.Execute(operation);
        }

        public void SetStartNode(string canvasGuid, string nodeGuid)
        {

            IOperation operation = new SetStartNodeOperation
            {
                CanvasGuid = canvasGuid,
                NewNodeGuid = nodeGuid,
            };
            _ = flowOperationService.Execute(operation);

            return;
            if (!TryGetCanvasModel(canvasGuid, out var canvasModel) || !TryGetNodeModel(nodeGuid, out var newStartNodeModel))
            {
                return;
            }
            var oldNodeGuid = canvasModel.StartNode?.Guid;
            /*if(TryGetNodeModel(oldNodeGuid, out var newStartNodeModel))
            {
                newStartNode.IsStart = false;
            }*/
            canvasModel.StartNode = newStartNodeModel;
            //newStartNode.IsStart = true;
            _ = TriggerEvent(() =>
                flowEnvironmentEvent.OnStartNodeChanged(
                    new StartNodeChangeEventArgs(canvasGuid, oldNodeGuid, newStartNodeModel.Guid)
                ));
            return;
        }

        public void SetConnectPriorityInvoke(string fromNodeGuid, string toNodeGuid, ConnectionInvokeType connectionType)
        {

            IOperation operation = new ChangeNodeConnectionOperation
            {
                CanvasGuid = string.Empty, // 连接优先级不需要画布
                FromNodeGuid = fromNodeGuid,
                ToNodeGuid = toNodeGuid,
                ConnectionInvokeType = connectionType,
                ChangeType = NodeConnectChangeEventArgs.ConnectChangeType.Create
            };
            flowOperationService.Execute(operation);
        }

        public void ChangeParameter(string nodeGuid, bool isAdd, int paramIndex)
        {
            IOperation operation = new ChangeParameterOperation
            {
                NodeGuid = nodeGuid,
                IsAdd = isAdd,
                ParamIndex = paramIndex
            };
            flowOperationService.Execute(operation);
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

            async Task AddNodeAsync(NodeInfo nodeInfo, IFlowNode nodeModel)
            {
                if (!TryGetCanvasModel(nodeInfo.CanvasGuid, out var canvasModel))
                {
                    SereinEnv.WriteLine(InfoType.ERROR, $"加载节点[{nodeInfo.Guid}]时发生异常，画布[{nodeInfo.CanvasGuid}]不存在");
                }
                else
                {
                    // 节点与画布互相绑定
                    // 需要在UI线程上进行添加，否则会报 “不支持从调度程序线程以外的线程对其 SourceCollection 进行的更改”异常
                    nodeModel.CanvasDetails = canvasModel;
                    await TriggerEvent(() =>
                    {
                        try
                        {
                            var nodes = canvasModel.Nodes.ToList();
                            nodes.Add(nodeModel);
                            canvasModel.Nodes = nodes;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                        }
                    }); // 添加到画布节点集合中
                    nodeModel.LoadInfo(nodeInfo); // 创建节点model
                    nodeModel.Guid ??= Guid.NewGuid().ToString();
                    flowModelService.AddNodeModel(nodeModel);

                    await TriggerEvent(() =>
                        flowEnvironmentEvent.OnNodeCreated(
                            new NodeCreateEventArgs(nodeInfo.CanvasGuid, nodeModel, nodeInfo.Position)
                        )
                        ); // 创建节点事件

                }

               
            }


            List<NodeInfo> flowCallNodeInfos = [];
            foreach (NodeInfo? nodeInfo in nodeInfos)
            {
                if (nodeInfo.Type == nameof(NodeControlType.FlowCall))
                {
                    flowCallNodeInfos.Add(nodeInfo);
                }
                else
                {
                    if (CreateNodeFromNodeInfo(nodeInfo, out var nodeModel) && nodeModel is not null)
                    {
                        await AddNodeAsync(nodeInfo, nodeModel);
                    }
                    else
                    {
                        SereinEnv.WriteLine(InfoType.WARN, $"节点创建失败。{Environment.NewLine}{nodeInfo}");
                    }
                }
            }

            // 创建流程接口节点
            foreach (NodeInfo? nodeInfo in flowCallNodeInfos)
            {
                if (CreateNodeFromNodeInfo(nodeInfo, out var nodeModel) && nodeModel is not null)
                {
                    await AddNodeAsync(nodeInfo, nodeModel);
                }
                else
                {
                    SereinEnv.WriteLine(InfoType.WARN, $"节点创建失败。{Environment.NewLine}{nodeInfo}");
                }
            }
            #endregion

            #region 重新放置节点

            List<NodeInfo> needPlaceNodeInfos = [];
            foreach (NodeInfo? nodeInfo in nodeInfos)
            {
                if (!string.IsNullOrEmpty(nodeInfo.ParentNodeGuid) &&
                    TryGetNodeModel(nodeInfo.ParentNodeGuid, out var parentNode))
                {
                    needPlaceNodeInfos.Add(nodeInfo); // 需要重新放置的节点
                }
            }

            foreach (NodeInfo nodeInfo in needPlaceNodeInfos)
            {
                if (TryGetNodeModel(nodeInfo.Guid, out var nodeModel) &&
                    TryGetNodeModel(nodeInfo.ParentNodeGuid, out var containerNode)
                    && containerNode is INodeContainer nodeContainer)
                {
                    var result = nodeContainer.PlaceNode(nodeModel);
                    if (result)
                    {
                        await TriggerEvent(() =>
                            flowEnvironmentEvent.OnNodePlace(
                                new NodePlaceEventArgs(nodeInfo.CanvasGuid, nodeModel.Guid, containerNode.Guid)
                            ));
                    }


                }
            }
            #endregion

            await Task.Delay(100);

            #region 确定节点之间的方法调用关系
            foreach (var nodeInfo in nodeInfos)
            {
                var canvasGuid = nodeInfo.CanvasGuid;
                if (!TryGetNodeModel(nodeInfo.Guid, out var fromNodeModel))
                {
                    return;
                }
                if (fromNodeModel is null) continue;
                foreach (var kvp in nodeInfo.SuccessorNodes)
                {
                    var type = kvp.Key;
                    var nodes = kvp.Value;
                    if (nodes.Length == 0) continue;
                    // 遍历当前类型分支的节点（确认连接关系）
                    foreach (var toNodeGuid in nodes)
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
                        if (fromNodeModel.SuccessorNodes[type].Contains(toNodeModel) || toNodeModel.PreviousNodes[type].Contains(fromNodeModel))
                        {
                            continue;
                        }
                        ConnectInvokeNode(canvasGuid, fromNodeModel.Guid, toNodeModel.Guid, JunctionType.NextStep, JunctionType.Execute, type);

                    }
                }
            }

            foreach (var nodeInfo in nodeInfos)
            {
                var canvasGuid = nodeInfo.CanvasGuid;
                if (!TryGetNodeModel(nodeInfo.Guid, out var toNodeModel))
                {
                    return;
                }
                if (toNodeModel is null) continue;
                foreach (var kvp in nodeInfo.PreviousNodes)
                {
                    var type = kvp.Key;
                    var nodes = kvp.Value;
                    if (nodes.Length == 0) continue;
                    // 遍历当前类型分支的节点（确认连接关系）
                    foreach (var toNodeGuid in nodes)
                    {
                        if (!TryGetNodeModel(toNodeGuid, out var fromNodeModel))
                        {
                            return;
                        }
                        if (toNodeModel is null)
                        {
                            // 防御性代码，加载正常保存的项目文件不会进入这里
                            continue;
                        }
                        if (fromNodeModel.SuccessorNodes[type].Contains(toNodeModel) || toNodeModel.PreviousNodes[type].Contains(fromNodeModel))
                        {
                            continue;
                        }
                        ConnectInvokeNode(canvasGuid, fromNodeModel.Guid, toNodeModel.Guid, JunctionType.NextStep, JunctionType.Execute, type);
                    }
                }


            }

            #endregion

            #region 确定节点之间的参数调用关系
            foreach (var nodeInfo in nodeInfos)
            {
                var pdInfos = nodeInfo.ParameterData;
                var toNodeGuid = nodeInfo.Guid;
                for (global::System.Int32 index = 0; index < pdInfos.Length; index++)
                {
                    var pdInfo = pdInfos[index];
                    var fromNodeGuid = pdInfo.SourceNodeGuid;
                    if (!string.IsNullOrWhiteSpace(fromNodeGuid) && flowModelService.TryGetCanvasModel(fromNodeGuid,out var fromNode))
                    {
                        continue;
                    }
                    var type = EnumHelper.ConvertEnum<ConnectionArgSourceType>(pdInfo.SourceType);
                    var canvasGuid = nodeInfo.CanvasGuid;
                    ConnectArgSourceNode(canvasGuid, fromNodeGuid, toNodeGuid, JunctionType.ReturnData, JunctionType.ArgData, type,index);

                }
            }


           /* var nodeModels = flowModelService.GetAllNodeModel();
            foreach (var toNode in nodeModels)
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
                        && TryGetNodeModel(pd.ArgDataSourceNodeGuid, out var fromNode))
                    {
                        *//*if (fromNode.NeedResultNodes[pd.ArgDataSourceType].Contains(toNode) 
                            && pd.ArgDataSourceNodeGuid == fromNode.Guid
                            && )
                        {
                            continue;
                        }*//*
                        ConnectArgSourceNode(canvasGuid, fromNode.Guid, toNode.Guid, JunctionType.ReturnData, JunctionType.ArgData, pd.ArgDataSourceType, pd.Index);
                    }
                }
            }*/
            #endregion

           
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
                UIContextOperation?.Invoke(() => flowEnvironmentEvent.OnNodeLocated(new NodeLocatedEventArgs(nodeGuid)));
            }

        }


        #endregion



        private async Task TriggerEvent(Action action)
        {
            if(UIContextOperation is null)
            {
                action?.Invoke();
            }
            else
            {
                await UIContextOperation.InvokeAsync(() =>
                {
                    action?.Invoke();
                });
            }
        }
    }



    

}
