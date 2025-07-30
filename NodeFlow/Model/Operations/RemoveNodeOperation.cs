using Serein.Library;
using Serein.Library.Api;
using System.Reflection.Metadata;

namespace Serein.NodeFlow.Model.Operations
{
    internal class RemoveNodeOperation : OperationBase
    {
        public override string Theme => throw new NotImplementedException();

        public required string CanvasGuid { get; internal set; }
        public required string NodeGuid { get; internal set; }

        /// <summary>
        /// 节点所在画布
        /// </summary>
        private FlowCanvasDetails flowCanvasDetails;

        /// <summary>
        /// 被删除的节点
        /// </summary>
        private IFlowNode flowNode;

        /// <summary>
        /// 移除节点时删除连线所触发的事件参数的缓存
        /// </summary>
        private List<NodeConnectChangeEventArgs> EventArgs {  get; } = new List<NodeConnectChangeEventArgs>();

        public override bool ValidationParameter()
        {
            var canvasModel = flowModelService.GetCanvasModel(CanvasGuid);
            var nodeModel = flowModelService.GetNodeModel(NodeGuid);
            if(canvasModel is null)
            {
                return false;
            }
            if(nodeModel is null)
            {
                return false;
            }
            flowCanvasDetails = canvasModel;
            flowNode = nodeModel;
            return true;
        }

        public override async Task<bool> ExecuteAsync()
        {
            if (!ValidationParameter()) return false;

            // 需要移除对应的方法调用、参数获取调用以及子节点信息
            // 还需要记录移除的事件参数，用以撤销恢复

            if (flowNode.ChildrenNode.Count > 0)
            {
                // 如果该节点存在子节点，则删除所有子节点
                foreach(var child in flowNode.ChildrenNode)
                {
                    flowEnvironment.FlowEdit.RemoveNode(CanvasGuid, child.Guid);
                }
            }


            #region 移除方法调用关系

            // 检查该节点的前继节点，然后从这些前继节点中移除与该节点的连接关系
            var previousNodes = flowNode.PreviousNodes.Where(kvp => kvp.Value.Count > 0).ToDictionary();
            foreach (var item in previousNodes)
            {

                var connectionType = item.Key; // 连接类型
                var nodes = item.Value.ToArray();  // 对应类型的父节点集合
                foreach (IFlowNode previousNode in nodes)
                {
                    flowNode.PreviousNodes[connectionType].Remove(previousNode);
                    previousNode.SuccessorNodes[connectionType].Remove(flowNode);
                    var e = new NodeConnectChangeEventArgs(
                            CanvasGuid, // 画布
                            previousNode.Guid, // 父节点Guid
                            flowNode.Guid, // 被移除的节点Guid
                            JunctionOfConnectionType.Invoke, // 方法调用关系
                            connectionType, // 对应的连接关系
                            NodeConnectChangeEventArgs.ConnectChangeType.Remove); // 移除连线
                    EventArgs.Add(e); // 缓存事件参数
                }
            }

            // 检查该节点的后续节点，然后从这些后续节点中移除与该节点的连接关系
            var successorNodes = flowNode.SuccessorNodes.Where(kvp => kvp.Value.Count > 0).ToDictionary();
            if (flowNode.ControlType == NodeControlType.FlowCall)
            {
                // 根据流程接口节点目前的设计，暂未支持能连接下一个节点
            }
            else
            {
                // 遍历所有后继节点，从那些后继节点中的前置节点集合中移除该节点
                foreach (var item in successorNodes)
                {

                    var connectionType = item.Key; // 方法调用连接类型
                    var nodes = item.Value.ToArray();  // 对应类型的父节点集合
                    foreach (IFlowNode successorNode in nodes)
                    {
                        successorNode.PreviousNodes[connectionType].Remove(flowNode);
                        flowNode.SuccessorNodes[connectionType].Remove(successorNode);
                        var e = new NodeConnectChangeEventArgs(
                                CanvasGuid, // 画布
                                flowNode.Guid, // 被移除的节点Guid
                                successorNode.Guid, // 子节点Guid
                                JunctionOfConnectionType.Invoke, // 方法调用关系
                                connectionType, // 对应的连接关系
                                NodeConnectChangeEventArgs.ConnectChangeType.Remove); // 移除连线
                        EventArgs.Add(e); // 缓存事件参数
                        
                    }
                }
            }

            #endregion

            #region 移除参数获取关系

            // 遍历需要该节点返回值的节点，移除与其的连接
            foreach (var item in flowNode.NeedResultNodes)
            {
                var connectionType = item.Key; // 参数来源连接类型
                var argNodes = item.Value.ToArray();  // 对应类型的入参需求节点集合
                foreach (var argNode in argNodes)
                {
                    var md = argNode.MethodDetails;
                    if (md is null) continue;
                    var pds = md.ParameterDetailss;
                    if (pds is null || pds.Length == 0) continue;
                    foreach (var parameter in pds)
                    {
                        if (!parameter.ArgDataSourceNodeGuid.Equals(flowNode.Guid)) continue;
                        parameter.ArgDataSourceNodeGuid = string.Empty;
                        parameter.ArgDataSourceType = ConnectionArgSourceType.GetPreviousNodeData;
                        // 找到了对应的入参控制点了
                        var e = new NodeConnectChangeEventArgs(
                                    CanvasGuid, // 画布
                                    flowNode.Guid, // 数据来源节点（被移除的节点Guid）
                                    argNode.Guid, // 需要数据的节点
                                    parameter.Index, // 作用在第几个参数上，用于指示移除第几个参数的连线
                                    JunctionOfConnectionType.Arg, // 指示移除的是参数连接线
                                    connectionType, // 对应的连接关系
                                    NodeConnectChangeEventArgs.ConnectChangeType.Remove); // 移除连线
                        EventArgs.Add(e); // 缓存事件参数
                    }
                }
            }

            // 遍历该节点参数详情，获取来源节点，移除与其的连接
          
            if (flowNode.MethodDetails?.ParameterDetailss != null)
            {
                var pds = flowNode.MethodDetails.ParameterDetailss.ToArray();
                foreach (var pd in pds)
                {
                    if (string.IsNullOrWhiteSpace(pd.ArgDataSourceNodeGuid)) continue;
                    if(flowModelService.TryGetNodeModel(pd.ArgDataSourceNodeGuid, out var argSourceNode))
                    {
                        pd.ArgDataSourceNodeGuid = string.Empty;
                        pd.ArgDataSourceType = ConnectionArgSourceType.GetPreviousNodeData;
                        // 找到了对应的入参控制点了
                        var e = new NodeConnectChangeEventArgs(
                                    CanvasGuid, // 画布
                                    argSourceNode.Guid, // 数据来源节点
                                    flowNode.Guid, // 需要数据的节点（被移除的节点Guid）
                                    pd.Index, // 作用在第几个参数上，用于指示移除第几个参数的连线
                                    JunctionOfConnectionType.Arg, // 指示移除的是参数连接线
                                    pd.ArgDataSourceType, // 对应的连接关系
                                    NodeConnectChangeEventArgs.ConnectChangeType.Remove); // 移除连线
                        EventArgs.Add(e); // 缓存事件参数
                    }
                }
            }
            
            #endregion

            flowModelService.RemoveNodeModel(flowNode); // 从记录中移除
                                                        //flowNode.Remove(); // 调用节点的移除方法


            // 存在UI上下文操作，当前运行环境极有可能运行在有UI线程的平台上
            // 为了避免直接修改 ObservableCollection 集合导致异常产生，故而使用UI线程上下文操作运行
            NodeConnectChangeEventArgs[] es = EventArgs.ToArray();
            await TriggerEvent(() =>
            {

                /*flowCanvasDetails.Nodes.Remove(flowNode);
                flowCanvasDetails.OnPropertyChanged(nameof(FlowCanvasDetails.Nodes));
                if (flowNode.IsPublic)
                {
                    flowCanvasDetails.PublicNodes.Remove(flowNode); 
                    flowCanvasDetails.OnPropertyChanged(nameof(FlowCanvasDetails.PublicNodes));
                }*/

                // 手动赋值刷新UI显示
                var lsit = flowCanvasDetails.Nodes.ToList();
                lsit.Remove(flowNode);
                flowCanvasDetails.Nodes = lsit;
                if (flowNode.IsPublic)
                {
                    var publicNodes = flowCanvasDetails.PublicNodes.ToList();
                    publicNodes.Remove(flowNode);
                    flowCanvasDetails.PublicNodes = publicNodes;
                }

                foreach (var e in es)
                {
                    flowEnvironmentEvent.OnNodeConnectChanged(e); // 触发事件
                }
                flowEnvironmentEvent.OnNodeRemoved(new NodeRemoveEventArgs(CanvasGuid, NodeGuid));
            });

            /*if (flowEnvironment.UIContextOperation is null) 
            {
                flowCanvasDetails?.Nodes.Remove(flowNode);
            }
            else
            {
               
               
            }*/

           /* await TriggerEvent(() =>
            {
                
            });*/
            return true;
        }

        public override bool Undo()
        {
            // 先恢复被删除的节点


            
            // 撤销删除节点时，还需要恢复连线状态
            foreach (NodeConnectChangeEventArgs e in EventArgs)
            {
                NodeConnectChangeEventArgs? newEventArgs = null;
                if (e.JunctionOfConnectionType == JunctionOfConnectionType.Invoke)
                {
                    newEventArgs = new NodeConnectChangeEventArgs(
                                    e.CanvasGuid, // 画布
                                    e.FromNodeGuid, // 被移除的节点Guid
                                    e.ToNodeGuid, // 子节点Guid
                                    e.JunctionOfConnectionType, // 指示需要恢复的是方法调用线
                                    e.ConnectionInvokeType, // 对应的连接关系
                                    NodeConnectChangeEventArgs.ConnectChangeType.Create); // 创建连线
                }
                else if (e.JunctionOfConnectionType == JunctionOfConnectionType.Arg)
                {
                    newEventArgs = new NodeConnectChangeEventArgs(
                                    e.CanvasGuid, // 画布
                                    e.FromNodeGuid, // 被移除的节点Guid
                                    e.ToNodeGuid, // 子节点Guid
                                    e.ArgIndex, // 作用在第几个参数上，用于指示移除第几个参数的连线
                                    e.JunctionOfConnectionType, // 指示需要恢复的是参数连接线
                                    e.ConnectionArgSourceType, // 对应的连接关系
                                    NodeConnectChangeEventArgs.ConnectChangeType.Create); // 创建连线
                
                }
                else
                {
                    newEventArgs = null;
                }
                if (newEventArgs != null) 
                {
                    // 使用反转了的事件参数进行触发
                    flowEnvironmentEvent.OnNodeConnectChanged(newEventArgs);
                }
            }
            return true;
        }


        public override void ToInfo()
        {
            throw new NotImplementedException();
        }
    }
}
