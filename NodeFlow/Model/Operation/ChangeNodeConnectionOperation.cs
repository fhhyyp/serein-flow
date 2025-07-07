using Serein.Library;
using Serein.Library.Api;
using Serein.NodeFlow.Env;
using Serein.NodeFlow.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Serein.Library.Api.NodeConnectChangeEventArgs;

namespace Serein.NodeFlow.Model.Operation
{
    /// <summary>
    /// 节点连接状态发生改变
    /// </summary>
    internal class ChangeNodeConnectionOperation : OperationBase
    {
        public override string Theme => nameof(ChangeNodeConnectionOperation);

        /// <summary>
        /// 所在画布
        /// </summary>
        public required string CanvasGuid { get; set; }

        /// <summary>
        /// 连接关系中始节点的Guid
        /// </summary>
        public required string FromNodeGuid { get; set; }

        /// <summary>
        /// 连接关系中目标节点的Guid
        /// </summary>
        public required string ToNodeGuid { get; set; }

        /// <summary>
        /// 起始节点连接控制点类型
        /// </summary>
        public JunctionType FromNodeJunctionType { get; set; }

        /// <summary>
        /// 目标节点连接控制点类型
        /// </summary>
        public JunctionType ToNodeJunctionType { get; set; }

        /// 连接类型
        /// </summary>
        public ConnectionInvokeType ConnectionInvokeType { get; set; }
        /// <summary>
        /// 表示此次需要在两个节点之间创建连接关系，或是移除连接关系
        /// </summary>
        public ConnectChangeType ChangeType { get; set; }
        /// <summary>
        /// 指示需要创建什么类型的连接线
        /// </summary>
        public JunctionOfConnectionType JunctionOfConnectionType { get; set; } = JunctionOfConnectionType.None;
        /// <summary>
        /// 节点对应的方法入参所需参数来源
        /// </summary>
        public ConnectionArgSourceType ConnectionArgSourceType { get; set; }

        /// <summary>
        /// 第几个参数
        /// </summary>
        public int ArgIndex { get; set; } = -1;

        public override bool IsCanUndo => false;

        #region 私有参数
        private FlowCanvasDetails FlowCanvas;
        private IFlowNode FromNode;
        private IFlowNode ToNode;
        #endregion

        public override bool ValidationParameter()
        {
            if (JunctionOfConnectionType == JunctionOfConnectionType.None)
                return false;
            if (JunctionOfConnectionType == JunctionOfConnectionType.Arg && ArgIndex == -1)
                return false;

            if (!flowModelService.ContainsCanvasModel(CanvasGuid) // 不存在画布
                || !flowModelService.ContainsNodeModel(FromNodeGuid) // 不存在节点
                || !flowModelService.ContainsNodeModel(ToNodeGuid)) // 不存在节点
            {
                return false;
            }

            return true;
        }

        public override async Task<bool> ExecuteAsync()
        {
            if (!ValidationParameter()) return false;
            if (!flowModelService.TryGetCanvasModel(CanvasGuid, out  FlowCanvas) // 不存在画布
                || !flowModelService.TryGetNodeModel(FromNodeGuid, out FromNode) // 不存在节点
                || !flowModelService.TryGetNodeModel(ToNodeGuid, out  ToNode)) // 不存在节点
            {
                return false;
            }

            if(ChangeType == ConnectChangeType.Create) // 创建连线时需要检查
            {
                (var jcType, var isCanConnection) = CheckConnect(FromNode, ToNode, FromNodeJunctionType, ToNodeJunctionType);
                if (!isCanConnection)
                {
                    SereinEnv.WriteLine(InfoType.WARN, "出现非预期的连接行为");
                    return false; // 出现不符预期的连接行为，忽略此次连接行为
                }

                // 如果起始控制点是“方法执行”,目标控制点是“方法调用”，需要反转 from to 节点
                if (jcType == JunctionOfConnectionType.Invoke
                    && FromNodeJunctionType == JunctionType.Execute
                    && ToNodeJunctionType == JunctionType.NextStep)
                {
                    // 如果 起始控制点 是“方法调用”，需要反转 from to 节点
                    (FromNode, ToNode) = (ToNode, FromNode);
                }

                // 如果起始控制点是“方法入参”,目标控制点是“返回值”，需要反转 from to 节点
                if (jcType == JunctionOfConnectionType.Arg
                    && FromNodeJunctionType == JunctionType.ArgData
                    && ToNodeJunctionType == JunctionType.ReturnData)
                {
                    (FromNode, ToNode) = (ToNode, FromNode);
                }
            }


            //if (toNode is SingleFlipflopNode flipflopNode)
            //{
            //    flowTaskManagement?.TerminateGlobalFlipflopRuning(flipflopNode); // 假设被连接的是全局触发器，尝试移除
            //}

            var state =  (JunctionOfConnectionType, ChangeType) switch
            {
                (JunctionOfConnectionType.Invoke, NodeConnectChangeEventArgs.ConnectChangeType.Create) => await CreateInvokeConnection(), // 创建节点之间的调用关系
                (JunctionOfConnectionType.Invoke, NodeConnectChangeEventArgs.ConnectChangeType.Remove) => await RemoveInvokeConnection(), // 移除节点之间的调用关系
                (JunctionOfConnectionType.Arg, NodeConnectChangeEventArgs.ConnectChangeType.Create) => await CreateArgConnection(), // 创建节点之间的参数传递关系
                (JunctionOfConnectionType.Arg, NodeConnectChangeEventArgs.ConnectChangeType.Remove) => await RemoveArgConnection(), // 移除节点之间的参数传递关系
                _ => false
            };
            return state;
        }

        public override void ToInfo()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 创建方法调用关系
        /// </summary>
        private async Task<bool> CreateInvokeConnection()
        {
            IFlowNode fromNode = FromNode ;
            IFlowNode toNode = ToNode;
            ConnectionInvokeType invokeType = ConnectionInvokeType;
            if (fromNode.ControlType == NodeControlType.FlowCall)
            {
                SereinEnv.WriteLine(InfoType.ERROR, $"流程接口节点不可调用下一个节点。" +
                          $"{Environment.NewLine}流程节点:{fromNode.Guid}");
                return false;
            }


            var isOverwriting = false;
            ConnectionInvokeType overwritingCt = ConnectionInvokeType.None;
            var isPass = false;

            #region 检查是否存在对应的连接
            foreach (ConnectionInvokeType ctType in NodeStaticConfig.ConnectionTypes)
            {
                var count1 = fromNode.SuccessorNodes[ctType].Count(it => it.Guid.Equals(toNode.Guid));
                var count2 = toNode.PreviousNodes[ctType].Count(it => it.Guid.Equals(fromNode.Guid));
                var hasError1 = count1 > 0;
                var hasError2 = count2 > 0;
                if (hasError1 && hasError2)
                {
                    if (ctType == invokeType)
                    {
                        SereinEnv.WriteLine(InfoType.WARN, $"起始节点已与目标节点存在连接。" +
                           $"{Environment.NewLine}起始节点:{fromNode.Guid}" +
                           $"{Environment.NewLine}目标节点:{toNode.Guid}");
                        return false;
                    }
                    isOverwriting = true; // 需要移除连接再创建连接
                    overwritingCt = ctType;
                }
                else
                {
                    // 检查是否可能存在异常
                    if (!hasError1 && hasError2)
                    {
                        SereinEnv.WriteLine(InfoType.ERROR, $"起始节点不是目标节点的父节点，目标节点却是起始节点的子节点。" +
                            $"{Environment.NewLine}起始节点:{fromNode.Guid}" +
                            $"{Environment.NewLine}目标节点:{toNode.Guid}");
                        isPass = false;
                    }
                    else if (hasError1 && !hasError2)
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
            #endregion


            if (isPass)
            {
                if (isOverwriting) // 需要替换
                {
                    fromNode.SuccessorNodes[overwritingCt].Remove(toNode); // 从起始节点原有类别的子分支中移除
                    toNode.PreviousNodes[overwritingCt].Remove(fromNode); // 从目标节点原有类别的父分支中移除
                }
                fromNode.SuccessorNodes[invokeType].Add(toNode); // 添加到起始节点新类别的子分支
                toNode.PreviousNodes[invokeType].Add(fromNode); // 添加到目标节点新类别的父分支

                await TriggerEvent(() =>
                {
                    flowEnvironmentEvent.OnNodeConnectChanged(
                               new NodeConnectChangeEventArgs(
                                    FlowCanvas.Guid,
                                   fromNode.Guid, // 从哪个节点开始
                                   toNode.Guid, // 连接到那个节点
                                   JunctionOfConnectionType.Invoke,
                                   invokeType, // 连接线的样式类型
                                   NodeConnectChangeEventArgs.ConnectChangeType.Create // 是创建连接还是删除连接
                               ));
                });
                
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
        /// 移除方法调用关系
        /// </summary>
        private async Task<bool> RemoveInvokeConnection()
        {
            FromNode.SuccessorNodes[ConnectionInvokeType].Remove(ToNode);
            ToNode.PreviousNodes[ConnectionInvokeType].Remove(FromNode);

            await TriggerEvent(() =>
            {
                flowEnvironmentEvent.OnNodeConnectChanged(
                       new NodeConnectChangeEventArgs(
                           FlowCanvas.Guid,
                           FromNode.Guid,
                           ToNode.Guid,
                           JunctionOfConnectionType.Invoke,
                           ConnectionInvokeType,
                           NodeConnectChangeEventArgs.ConnectChangeType.Remove));
            });


            /* if (string.IsNullOrEmpty(ToNode.MethodDetails.ParameterDetailss[ArgIndex].ArgDataSourceNodeGuid))
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
             }*/
            return true;
        }

        /// <summary>
        /// 创建参数连接关系
        /// </summary>
        /// <exception cref="Exception"></exception>
        private async Task<bool> CreateArgConnection()
        {/*
            IFlowNode fromNodeControl = ToNode;
            IFlowNode toNodeControl = ToNode;*/
            ConnectionArgSourceType type = ConnectionArgSourceType;
            int index = ArgIndex;


            /*FromNode.NeedResultNodes[type].Remove(ToNode); // 从起始节点的参数来源中移除目标节点
            if (FromNode.Guid == ToNode.Guid) // 不能连接到自己
            {
                SereinEnv.WriteLine(InfoType.ERROR, $"起始节点与目标节点不能是同一个节点" +
                    $"{Environment.NewLine}起始节点：{FromNode.Guid}" +
                    $"{Environment.NewLine}目标节点：{ToNode.Guid}");
                return false;
            }*/
            var toNodeArgSourceGuid = ToNode.MethodDetails.ParameterDetailss[ArgIndex].ArgDataSourceNodeGuid; // 目标节点对应参数可能已经有其它连接
            var toNodeArgSourceType = ToNode.MethodDetails.ParameterDetailss[ArgIndex].ArgDataSourceType;
            
            if (FromNode.Guid == toNodeArgSourceGuid 
                && toNodeArgSourceType == ConnectionArgSourceType)
            {
                if (FromNode.NeedResultNodes[type].Contains(ToNode))
                {
                    SereinEnv.WriteLine(InfoType.INFO, $"节点之间已建立过连接关系" +
                    $"起始节点：{FromNode.Guid}" +
                    $"目标节点：{ToNode.Guid}" +
                    $"参数索引：{ArgIndex}" +
                    $"参数类型：{ConnectionArgSourceType}");
                    return false;
                }
                FromNode.NeedResultNodes[type].Add(ToNode);
                await TriggerEvent(() =>
                {
                    flowEnvironmentEvent.OnNodeConnectChanged(
                                new NodeConnectChangeEventArgs(
                                    FlowCanvas.Guid,
                                    FromNode.Guid, // 从哪个节点开始
                                    ToNode.Guid, // 连接到那个节点
                                    ArgIndex, // 连接线的样式类型
                                    JunctionOfConnectionType.Arg,
                                    ConnectionArgSourceType,
                                    NodeConnectChangeEventArgs.ConnectChangeType.Remove // 是创建连接还是删除连接
                                )); // 通知UI 
                });
                await TriggerEvent(() =>
                {
                    flowEnvironmentEvent.OnNodeConnectChanged(
                                new NodeConnectChangeEventArgs(
                                    FlowCanvas.Guid,
                                    FromNode.Guid, // 从哪个节点开始
                                    ToNode.Guid, // 连接到那个节点
                                    ArgIndex, // 连接线的样式类型
                                    JunctionOfConnectionType.Arg,
                                    ConnectionArgSourceType,
                                    NodeConnectChangeEventArgs.ConnectChangeType.Create // 是创建连接还是删除连接
                                )); // 通知UI 
                });
              
                return true;
            }

            if (!string.IsNullOrEmpty(toNodeArgSourceGuid)) // 更改关系获取
            {
                ToNode.MethodDetails.ParameterDetailss[ArgIndex].ArgDataSourceNodeGuid = null;
                ToNode.MethodDetails.ParameterDetailss[ArgIndex].ArgDataSourceType = ConnectionArgSourceType.GetPreviousNodeData; // 恢复默认值

                await TriggerEvent(() =>
                {
                    flowEnvironmentEvent.OnNodeConnectChanged(
                      new NodeConnectChangeEventArgs(
                          FlowCanvas.Guid,
                          FromNode.Guid,
                          ToNode.Guid,
                          ArgIndex,
                          JunctionOfConnectionType.Arg,
                          ConnectionArgSourceType.GetPreviousNodeData,
                          NodeConnectChangeEventArgs.ConnectChangeType.Remove));
                });
            }

            ToNode.MethodDetails.ParameterDetailss[ArgIndex].ArgDataSourceNodeGuid = FromNode.Guid; // 设置
            ToNode.MethodDetails.ParameterDetailss[ArgIndex].ArgDataSourceType = ConnectionArgSourceType;

            await TriggerEvent(() =>
            {
                flowEnvironmentEvent.OnNodeConnectChanged(
                                new NodeConnectChangeEventArgs(
                                    FlowCanvas.Guid,
                                    FromNode.Guid, // 从哪个节点开始
                                    ToNode.Guid, // 连接到那个节点
                                    ArgIndex, // 连接线的样式类型
                                    JunctionOfConnectionType.Arg,
                                    ConnectionArgSourceType,
                                    NodeConnectChangeEventArgs.ConnectChangeType.Create // 是创建连接还是删除连接
                                )); // 通知UI 
            });
          
            return true;

        }

        /// <summary>
        /// 移除参数连接关系
        /// </summary>
        /// <param name="fromNodeControl"></param>
        /// <param name="toNodeControl"></param>
        /// <param name="index"></param>
        private async Task<bool> RemoveArgConnection()
        {
            ToNode.MethodDetails.ParameterDetailss[ArgIndex].ArgDataSourceNodeGuid = null;
            ToNode.MethodDetails.ParameterDetailss[ArgIndex].ArgDataSourceType = ConnectionArgSourceType.GetPreviousNodeData; // 恢复默认值


            await TriggerEvent(() =>
            {
                flowEnvironmentEvent.OnNodeConnectChanged(
                    new NodeConnectChangeEventArgs(
                        FlowCanvas.Guid,
                        FromNode.Guid,
                        ToNode.Guid,
                        ArgIndex,
                        JunctionOfConnectionType.Arg,
                        ConnectionArgSourceType.GetPreviousNodeData,
                        NodeConnectChangeEventArgs.ConnectChangeType.Remove));
            });

            return true;
        }

        /// <summary>
        /// 检查连接是否合法
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
                //else if (toNodeJunctionType == JunctionType.ArgData && fromNode.Guid.Equals(toNode.Guid)) 
                //{
                //    // “方法执行”控制点拖拽到“方法入参”控制点，且是同一个节点，则添加获取参数关系，表示生成入参参数时自动从该节点的上一节点获取flowdata
                //    type = JunctionOfConnectionType.Arg;
                //    state = true;
                //}
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
                //if (toNodeJunctionType == JunctionType.Execute && fromNode.Guid.Equals(toNode.Guid)) // 添加获取参数关系
                //{
                //    // “方法入参”控制点拖拽到“方法执行”控制点，且是同一个节点，则添加获取参数关系，生成入参参数时自动从该节点的上一节点获取flowdata
                //    type = JunctionOfConnectionType.Arg;
                //    state = true;
                //}
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

    }
}
