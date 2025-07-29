using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Serein.Library
{
    /// <summary>
    /// 节点方法拓展
    /// </summary>
    public static class FlowModelExtension
    {
        /// <summary>
        /// 导出为画布信息
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public static FlowCanvasDetailsInfo ToInfo(this FlowCanvasDetails model)
        {
            return new FlowCanvasDetailsInfo
            {
                Guid = model.Guid,
                Height = model.Height,
                Width = model.Width,
                Name = model.Name,
                ScaleX = model.ScaleX,
                ScaleY = model.ScaleY,
                ViewX = model.ViewX,
                ViewY = model.ViewY,
                StartNode = model.StartNode?.Guid,
            };
        }

        /// <summary>
        /// 从画布信息加载
        /// </summary>
        /// <param name="canvasModel"></param>
        /// <param name="canvasInfo"></param>
        public static void LoadInfo(this FlowCanvasDetails canvasModel, FlowCanvasDetailsInfo canvasInfo)
        {
            canvasModel.Guid = canvasInfo.Guid;
            canvasModel.Height = canvasInfo.Height;
            canvasModel.Width = canvasInfo.Width;
            canvasModel.Name = canvasInfo.Name;
            canvasModel.ScaleX = canvasInfo.ScaleX;
            canvasModel.ScaleY = canvasInfo.ScaleY;
            canvasModel.ViewX = canvasInfo.ViewX;
            canvasModel.ViewY = canvasInfo.ViewY;
            if(canvasModel.Env.TryGetNodeModel(canvasInfo.StartNode,out var nodeModel))
            {
                canvasModel.StartNode = nodeModel;
            }
        }

        /// <summary>
        /// 输出方法参数信息
        /// </summary>
        /// <returns></returns>
        public static ParameterData[] SaveParameterInfo(this IFlowNode nodeModel)
        {
            if (nodeModel.MethodDetails is null || nodeModel.MethodDetails.ParameterDetailss == null)
            {
                return new ParameterData[0];
            }

            if (nodeModel.MethodDetails.ParameterDetailss.Length > 0)
            {
                return nodeModel.MethodDetails.ParameterDetailss
                                    .Select(it => new ParameterData
                                    {
                                        SourceNodeGuid = it.ArgDataSourceNodeGuid,
                                        SourceType = it.ArgDataSourceType.ToString(),
                                        State = it.IsExplicitData,
                                        ArgName = it.Name,
                                        Value = it.DataValue,

                                    })
                                    .ToArray();
            }
            else
            {
                return Array.Empty<ParameterData>();
            }
        }

        /// <summary>
        /// 导出为节点信息
        /// </summary>
        /// <returns></returns>
        public static NodeInfo ToInfo(this IFlowNode nodeModel)
        {
            // if (MethodDetails == null) return null;
            /*var trueNodes = nodeModel.SuccessorNodes[ConnectionInvokeType.IsSucceed].Select(item => item.Guid); // 真分支
            var falseNodes = nodeModel.SuccessorNodes[ConnectionInvokeType.IsFail].Select(item => item.Guid);// 假分支
            var errorNodes = nodeModel.SuccessorNodes[ConnectionInvokeType.IsError].Select(item => item.Guid);// 异常分支
            var upstreamNodes = nodeModel.SuccessorNodes[ConnectionInvokeType.Upstream].Select(item => item.Guid);// 上游分支*/

            var successorNodes = nodeModel.SuccessorNodes.ToDictionary(kv => kv.Key, kv => kv.Value.Select(item => item.Guid).ToArray()); // 后继分支
            var previousNodes = nodeModel.PreviousNodes.ToDictionary(kv => kv.Key, kv => kv.Value.Select(item => item.Guid).ToArray()); // 后继分支


            // 生成参数列表
            ParameterData[] parameterDatas = nodeModel.SaveParameterInfo();

            var nodeInfo = new NodeInfo
            {
                CanvasGuid = nodeModel.CanvasDetails.Guid,
                Guid = nodeModel.Guid,
                IsPublic = nodeModel.IsPublic,
                AssemblyName = nodeModel.MethodDetails.AssemblyName,
                MethodName = nodeModel.MethodDetails?.MethodName,
                Label = nodeModel.MethodDetails?.MethodAnotherName,
                Type = nodeModel.ControlType.ToString(), //this.GetType().ToString(),
                /*TrueNodes = trueNodes.ToArray(),
                FalseNodes = falseNodes.ToArray(),
                UpstreamNodes = upstreamNodes.ToArray(),
                ErrorNodes = errorNodes.ToArray(),*/
                ParameterData = parameterDatas,
                Position = nodeModel.Position,
                IsProtectionParameter = nodeModel.DebugSetting.IsProtectionParameter,
                IsInterrupt = nodeModel.DebugSetting.IsInterrupt,
                IsEnable = nodeModel.DebugSetting.IsEnable,
                ParentNodeGuid = nodeModel.ContainerNode?.Guid,
                ChildNodeGuids = nodeModel.ChildrenNode.Select(item => item.Guid).ToArray(),
                SuccessorNodes = successorNodes,
                PreviousNodes = previousNodes,
            };
            nodeInfo.Position.X = Math.Round(nodeInfo.Position.X, 1);
            nodeInfo.Position.Y = Math.Round(nodeInfo.Position.Y, 1);
            nodeInfo = nodeModel.SaveCustomData(nodeInfo);
            return nodeInfo;
        }

        /// <summary>
        /// 从节点信息加载节点
        /// </summary>
        /// <param name="nodeModel"></param>
        /// <param name="canvas"></param>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        public static void LoadInfo(this IFlowNode nodeModel, NodeInfo nodeInfo)
        {
            nodeModel.Guid = nodeInfo.Guid;
            nodeModel.Position = nodeInfo.Position ?? new PositionOfUI(0, 0);// 加载位置信息
            var md = nodeModel.MethodDetails; // 当前节点的方法说明
            nodeModel.DebugSetting.IsProtectionParameter = nodeInfo.IsProtectionParameter; // 保护参数
            nodeModel.DebugSetting.IsInterrupt = nodeInfo.IsInterrupt; // 是否中断
            nodeModel.DebugSetting.IsEnable = nodeInfo.IsEnable; // 是否使能
            nodeModel.IsPublic = nodeInfo.IsPublic; // 是否全局公开
            if (md != null)
            {
                if (md.ParameterDetailss == null)
                {
                    md.ParameterDetailss = new ParameterDetails[0];
                }

                var pds = md.ParameterDetailss; // 当前节点的入参描述数组
                #region 类库方法型节点加载参数
                if (nodeInfo.ParameterData.Length > pds.Length && md.HasParamsArg)
                {
                    // 保存的参数信息项数量大于方法本身的方法入参数量（可能存在可变入参）
                    var length = nodeInfo.ParameterData.Length - pds.Length; // 需要扩容的长度
                    nodeModel.MethodDetails.ParameterDetailss = ArrayHelper.Expansion(pds, length); // 扩容入参描述数组
                    pds = md.ParameterDetailss; // 当前节点的入参描述数组
                    var startParmsPd = pds[md.ParamsArgIndex]; // 获取可变入参参数描述
                    for (int i = md.ParamsArgIndex + 1; i <= md.ParamsArgIndex + length; i++)
                    {
                        pds[i] = startParmsPd.CloneOfModel(nodeModel);
                        pds[i].Index = pds[i - 1].Index + 1;
                        pds[i].IsParams = true;
                    }
                }

                for (int i = 0; i < nodeInfo.ParameterData.Length; i++)
                {
                    if (i >= pds.Length && nodeModel.ControlType != NodeControlType.FlowCall)
                    {
                        nodeModel.Env.WriteLine(InfoType.ERROR, $"保存的参数数量大于方法此时的入参参数数量：[{nodeInfo.Guid}][{nodeInfo.MethodName}]");
                        break;
                    }
                    var pd = pds[i];
                    ParameterData pdInfo = nodeInfo.ParameterData[i];
                    pd.IsExplicitData = pdInfo.State;
                    pd.DataValue = pdInfo.Value;
                    pd.ArgDataSourceType = EnumHelper.ConvertEnum<ConnectionArgSourceType>(pdInfo.SourceType);
                    pd.ArgDataSourceNodeGuid =  pdInfo.SourceNodeGuid;

                }

                nodeModel.LoadCustomData(nodeInfo); // 加载自定义数据

                #endregion
            }
        }

        private readonly static ObjectPool<Stack<IFlowNode>> flowStackPool = new ObjectPool<Stack<IFlowNode>>(()=> new Stack<IFlowNode>());
        private readonly static ObjectPool<HashSet<IFlowNode>> checkpoints = new ObjectPool<HashSet<IFlowNode>>(()=> new HashSet<IFlowNode>());

        /// <summary>
        /// 开始执行
        /// </summary>
        /// <param name="nodeModel"></param>
        /// <param name="context"></param>
        /// <param name="token">流程运行</param>
        /// <returns></returns>
#nullable enable
        public static async Task<FlowResult> StartFlowAsync(this IFlowNode nodeModel, IFlowContext context, CancellationToken token)
        {
            Stack<IFlowNode> flowStack = flowStackPool.Allocate() ;
            //HashSet<IFlowNode> processedNodes = processedNodesPool.Allocate() ; // 用于记录已处理上游节点的节点

            flowStack.Push(nodeModel);
            IFlowNode? previousNode = null;
            IFlowNode? currentNode = null;
            try
            {
#if DEBUG

                /*
                    var sw = Stopwatch.StartNew();
                    var checkpoints = new Dictionary<string, TimeSpan>();
                    checkpoints["创建调用信息"] = sw.Elapsed;
                    var last = TimeSpan.Zero;
                    foreach (var kv in checkpoints)
                    {
                        SereinEnv.WriteLine(InfoType.INFO, $"{kv.Key} 耗时: {(kv.Value - last).TotalMilliseconds} ms");
                        last = kv.Value;
                    }
                */
                var sw = Stopwatch.StartNew();
                var checkpoints = new Dictionary<string, TimeSpan>();
#endif
                while (true)
                {
#if DEBUG
                    sw.Restart();
                    var last = TimeSpan.Zero;
                    checkpoints.Clear();
#endif

                    #region 执行相关
                    // 从栈中弹出一个节点作为当前节点进行处理
                    previousNode = currentNode;
                    currentNode = flowStack.Pop();

                    #region 新增调用信息
                    FlowInvokeInfo? invokeInfo = null;
                    var isRecordInvokeInfo = context.IsRecordInvokeInfo;
                    if (!isRecordInvokeInfo) goto Label_NotRecordInvoke;

                    FlowInvokeInfo.InvokeType invokeType = context.NextOrientation switch
                    {
                        ConnectionInvokeType.IsSucceed => FlowInvokeInfo.InvokeType.IsSucceed,
                        ConnectionInvokeType.IsFail => FlowInvokeInfo.InvokeType.IsFail,
                        ConnectionInvokeType.IsError => FlowInvokeInfo.InvokeType.IsError,
                        ConnectionInvokeType.Upstream => FlowInvokeInfo.InvokeType.Upstream,
                        _ => FlowInvokeInfo.InvokeType.None
                    };
                    invokeInfo = context.NewInvokeInfo(previousNode, currentNode, invokeType);
                    #endregion
#if DEBUG
                    checkpoints[$"[{currentNode.Guid}]\t创建调用信息"] = sw.Elapsed;
#endif

                Label_NotRecordInvoke:
                    context.NextOrientation = ConnectionInvokeType.IsSucceed; // 默认执行成功
                    FlowResult? flowResult = null;
                    try
                    {
                        flowResult = await currentNode.ExecutingAsync(context, token);
                    }
                    catch (Exception ex)
                    {
                        flowResult = new FlowResult(currentNode.Guid, context);
                        context.Env.WriteLine(InfoType.ERROR, $"节点[{currentNode.Guid}]异常：" + ex);
                        context.NextOrientation = ConnectionInvokeType.IsError;
                        context.ExceptionOfRuning = ex;
                    }
#if DEBUG
                    finally
                    {
                        checkpoints[$"[{currentNode.Guid}]\t方法调用"] = sw.Elapsed;
                    }
#endif
                    #endregion

                    #region 更新调用信息
                    var state = context.NextOrientation switch
                    {
                        ConnectionInvokeType.IsFail => FlowInvokeInfo.RunState.Failed,
                        ConnectionInvokeType.IsError => FlowInvokeInfo.RunState.Error,
                        _ => FlowInvokeInfo.RunState.Succeed
                    };
                    if (isRecordInvokeInfo)
                    {
                        invokeInfo.UploadState(state);
                        invokeInfo.UploadResultValue(flowResult.Value);
                    }

                    #endregion

#if DEBUG
                    checkpoints[$"[{currentNode.Guid}]\t更新调用信息"] = sw.Elapsed;
#endif

                    #region 执行完成时更新栈
                    context.AddOrUpdateFlowData(currentNode.Guid, flowResult); // 上下文中更新数据
#if DEBUG
                    checkpoints["[{currentNode.Guid}]\t执行完成时更新栈"] = sw.Elapsed;
#endif

                    // 首先将指定类别后继分支的所有节点逆序推入栈中
                    var nextNodes = currentNode.SuccessorNodes[context.NextOrientation];
                    for (int index = nextNodes.Count - 1; index >= 0; index--)
                    {
                        // 筛选出启用的节点的节点
                        if (nextNodes[index].DebugSetting.IsEnable)
                        {
                            var node = nextNodes[index];
                            context.SetPreviousNode(node.Guid, currentNode.Guid);
                            flowStack.Push(node);
                        }
                    }

#if DEBUG
                    checkpoints[$"[{currentNode.Guid}]\t后继分支推入栈中"] = sw.Elapsed;
#endif

                    // 然后将指上游分支的所有节点逆序推入栈中
                    var upstreamNodes = currentNode.SuccessorNodes[ConnectionInvokeType.Upstream];
                    for (int index = upstreamNodes.Count - 1; index >= 0; index--)
                    {
                        // 筛选出启用的节点的节点
                        if (upstreamNodes[index].DebugSetting.IsEnable)
                        {
                            var node = upstreamNodes[index];
                            context.SetPreviousNode(node.Guid, currentNode.Guid);
                            flowStack.Push(node);
                        }
                    }

#if DEBUG
                    checkpoints[$"[{currentNode.Guid}]\t上游分支推入栈中"] = sw.Elapsed;
#endif
                    #endregion


#if DEBUG
                    foreach (var kv in checkpoints)
                    {
                        SereinEnv.WriteLine(InfoType.INFO, $"{kv.Key} 耗时: {(kv.Value - last).TotalMilliseconds} ms");
                        last = kv.Value;
                    }
                    SereinEnv.WriteLine(InfoType.INFO, $"------");

#endif

                    #region 执行完成后检查
                    if (flowStack.Count == 0)
                    {
                        return flowResult;  // 说明流程到了终点
                    }

                    if (context.RunState == RunState.Completion)
                    {
                        currentNode.Env.WriteLine(InfoType.INFO, $"流程执行到节点[{currentNode.Guid}]时提前结束，将返回当前执行结果。");
                        return flowResult; // 流程执行完成，返回结果
                    }

                    if (token.IsCancellationRequested)
                    {
                        throw new Exception($"流程执行到节点[{currentNode.Guid}]时被取消，未能获取到流程结果。");
                    }


                    #endregion
#if DEBUG
                    //await Task.Delay(1);
#endif
                }
            }
            finally
            {
                 flowStackPool.Free(flowStack);
                //processedNodesPool.Free(processedNodes);
            }
        }

        /// <summary>
        /// 获取所有参数
        /// </summary>
        /// <param name="nodeModel"></param>
        /// <param name="context"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static async Task<object[]> GetParametersAsync(this IFlowNode nodeModel, IFlowContext context, CancellationToken token)
        {
            var md = nodeModel.MethodDetails;
            var pds = md.ParameterDetailss;

            if (pds.Length == 0)
                return [];

            object[] args;
            object[] paramsArgs = null; // 改为强类型数组 object[]

            int paramsArgIndex = md.ParamsArgIndex;

            if (paramsArgIndex >= 0)
            {
                // 可变参数数量
                int paramsLength = pds.Length - paramsArgIndex;

                // 用 object[] 表示可变参数数组（如果类型固定也可以用 int[] 等）
                paramsArgs = new object[paramsLength];

                // 方法参数中占位，最后一项是 object[]
                args = new object[paramsArgIndex + 1];
                args[paramsArgIndex] = paramsArgs;
            }
            else
            {
                args = new object[pds.Length];
            }

            // 并发处理常规参数
            Task<object>[] mainArgTasks = new Task<object>[paramsArgIndex >= 0 ? paramsArgIndex : pds.Length];

            for (int i = 0; i < mainArgTasks.Length; i++)
            {
                var pd = pds[i];
                mainArgTasks[i] = pd.ToMethodArgData(context);
            }

            await Task.WhenAll(mainArgTasks);

            for (int i = 0; i < mainArgTasks.Length; i++)
            {
                args[i] = mainArgTasks[i].Result;
            }

            // 并发处理 params 类型的入参参数
            if (paramsArgs != null)
            {
                int paramsLength = paramsArgs.Length;
                Task<object>[] paramTasks = new Task<object>[paramsLength];

                for (int i = 0; i < paramsLength; i++)
                {
                    var pd = pds[paramsArgIndex + i];
                    paramTasks[i] = pd.ToMethodArgData(context);
                }

                await Task.WhenAll(paramTasks);

                for (int i = 0; i < paramsLength; i++)
                {
                    paramsArgs[i] = paramTasks[i].Result;
                }

                args[args.Length - 1] = paramsArgs;
            }

            return args;
        }



        /// <summary>
        /// 获取对应的参数数组
        /// </summary>
        public static async Task<object[]> GetParametersAsync2(this IFlowNode nodeModel, IFlowContext context, CancellationToken token)
        {
            if (nodeModel.MethodDetails.ParameterDetailss.Length == 0)
            {
                return []; // 无参数
            }
            var md = nodeModel.MethodDetails;
            var pds = md.ParameterDetailss;

            #region 定义返回的参数数组
            object[] args;
            Array paramsArgs = null; // 初始化可选参数
            int paramsArgIndex = 0; // 可选参数下标，与 object[] paramsArgs 一起使用
            if (md.ParamsArgIndex >= 0) // 存在可变入参参数
            {
                var paramsArgType = pds[md.ParamsArgIndex].DataType; // 获取可变参数的参数类型
                int paramsLength = pds.Length - md.ParamsArgIndex;  // 可变参数数组长度 = 方法参数个数 - （ 可选入参下标 + 1 ）
                paramsArgs = Array.CreateInstance(paramsArgType, paramsLength);// 可变参数
                args = new object[md.ParamsArgIndex + 1]; // 调用方法的入参数组
                args[md.ParamsArgIndex] = paramsArgs; // 如果存在可选参数，入参参数最后一项则为可变参数
            }
            else
            {
                // 不存在可选参数
                args = new object[pds.Length]; // 调用方法的入参数组
            }
            #endregion

            // 常规参数的获取
            for (int i = 0; i < args.Length; i++)
            {
                var pd = pds[i];

                args[i] = await pd.ToMethodArgData(context); // 获取数据
            }

            // 可选参数的获取
            if (md.ParamsArgIndex >= 0)
            {
                for (int i = 0; i < paramsArgs.Length; i++)
                {
                    var pd = md.ParameterDetailss[paramsArgIndex + i];
                    var data = await pd.ToMethodArgData(context); // 获取数据
                    paramsArgs.SetValue(data, i);// 设置到数组中
                }
                args[args.Length - 1] = paramsArgs;
            }

            return args;
        }



        /// <summary>
        /// 视为流程接口调用
        /// </summary>
        /// <param name="flowCallNode"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public static async Task<TResult> ApiInvokeAsync<TResult>(this IFlowNode flowCallNode, Dictionary<string,object> param)
        {
            var pds = flowCallNode.MethodDetails.ParameterDetailss;
            if (param.Keys.Count != pds.Length)
            {
                throw new ArgumentNullException($"参数数量不一致。传入参数数量：{param.Keys.Count}。接口入参数量：{pds.Length}。");
            }

            var context = new FlowContext(flowCallNode.Env);
            
            for (int index = 0; index < pds.Length; index++)
            {
                ParameterDetails pd = pds[index];
                if (param.TryGetValue(pd.Name, out var value))
                {
                    context.SetParamsTempData(flowCallNode.Guid, index, value); // 设置入参参数
                }
            }
            var cts = new CancellationTokenSource();
            var flowResult = await flowCallNode.StartFlowAsync(context, cts.Token);
            cts?.Cancel();
            cts?.Dispose();
            context.Exit();
            if (flowResult.Value is TResult result)
            {
                return result;
            }
            else if (flowResult is FlowResult && flowResult is TResult result2)
            {
                return result2;
            }
            else
            {
                throw new ArgumentNullException($"类型转换失败，流程返回数据与泛型不匹配，当前返回类型为[{flowResult.Value.GetType().FullName}]。");
            }
        }

        /// <summary>
        /// 检查监视表达式是否生效
        /// </summary>
        /// <param name="nodeModel">节点Moel</param>
        /// <param name="context">上下文</param>
        /// <param name="newData">新的数据</param>
        /// <returns></returns>
        /*public static async Task CheckExpInterrupt(this NodeModelBase nodeModel, IDynamicContext context,  object newData = null)
        {
            string guid = nodeModel.Guid;
            context.AddOrUpdate(guid, newData); // 上下文中更新数据
            if (newData is null)
            {
            }
            else
            {
                await nodeModel.MonitorObjExpInterrupt(context, newData, 0); // 首先监视对象
                await nodeModel.MonitorObjExpInterrupt(context, newData, 1); // 然后监视节点
                //nodeModel.FlowData = newData; // 替换数据
            }
        }*/

        /// <summary>
        /// 监视对象表达式中断
        /// </summary>
        /// <param name="nodeModel"></param>
        /// <param name="context"></param>
        /// <param name="data"></param>
        /// <param name="monitorType"></param>
        /// <returns></returns>
        /*private static async Task MonitorObjExpInterrupt(this NodeModelBase nodeModel, IDynamicContext context, object data, int monitorType)
        {
            MonitorObjectEventArgs.ObjSourceType sourceType;
            string key;
            if (monitorType == 0)
            {
                key = data?.GetType()?.FullName;
                sourceType = MonitorObjectEventArgs.ObjSourceType.IOCObj;
            }
            else
            {
                key = nodeModel.Guid;
                sourceType = MonitorObjectEventArgs.ObjSourceType.IOCObj;
            }
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            //(var isMonitor, var exps) = await context.Env.CheckObjMonitorStateAsync(key);
            //if (isMonitor) // 如果新的数据处于查看状态，通知UI进行更新？交给运行环境判断？
            //{
            //    context.Env.MonitorObjectNotification(nodeModel.Guid, data, sourceType); // 对象处于监视状态，通知UI更新数据显示
            //    if (exps.Length > 0)
            //    {
            //        // 表达式环境下判断是否需要执行中断
            //        bool isExpInterrupt = false;
            //        string exp = "";
            //        // 判断执行监视表达式，直到为 true 时退出
            //        for (int i = 0; i < exps.Length && !isExpInterrupt; i++)
            //        {
            //            exp = exps[i];
            //            if (string.IsNullOrEmpty(exp)) continue;
            //            // isExpInterrupt = SereinConditionParser.To(data, exp);
            //        }

            //        if (isExpInterrupt) // 触发中断
            //        {
            //            nodeModel.DebugSetting.IsInterrupt = true;
            //            if (await context.Env.SetNodeInterruptAsync(nodeModel.Guid,true))
            //            {
            //                context.Env.TriggerInterrupt(nodeModel.Guid, exp, InterruptTriggerEventArgs.InterruptTriggerType.Exp);
            //                var cancelType = await nodeModel.DebugSetting.GetInterruptTask();
            //                await Console.Out.WriteLineAsync($"[{data}]中断已{cancelType}，开始执行后继分支");
            //                nodeModel.DebugSetting.IsInterrupt = false;
            //            }
            //        }
            //    }

            //}
        }*/


        /// <summary>
        /// 不再中断
        /// </summary>
        public static void CancelInterrupt(IFlowNode nodeModel)
        {
            nodeModel.DebugSetting.IsInterrupt = false;
            nodeModel.DebugSetting.CancelInterrupt?.Invoke();
        }

#if DEBUG
        /// <summary>
        /// 程序集更新，更新节点方法描述、以及所有入参描述的类型
        /// </summary>
        /// <param name="nodeModel">节点Model</param>
        /// <param name="newMd">新的方法描述</param>
        public static void UploadMethod(this IFlowNode nodeModel, MethodDetails newMd)
        {
            var thisMd = nodeModel.MethodDetails;

            thisMd.ActingInstanceType = newMd.ActingInstanceType; // 更新方法需要的类型

            var thisPds = thisMd.ParameterDetailss;
            var newPds = newMd.ParameterDetailss;
            // 当前存在可变参数，且新的方法也存在可变参数，需要把可变参数的数目与值传递过去
            if (thisMd.HasParamsArg && newMd.HasParamsArg)
            {
                int paramsLength = thisPds.Length - thisMd.ParamsArgIndex - 1; // 确定扩容长度
                newMd.ParameterDetailss = ArrayHelper.Expansion(newPds, paramsLength);// 为新方法的入参参数描述进行扩容
                newPds = newMd.ParameterDetailss;
                int index = newMd.ParamsArgIndex; // 记录
                var templatePd = newPds[newMd.ParamsArgIndex]; // 新的入参模板
                for (int i = thisMd.ParamsArgIndex; i < thisPds.Length; i++)
                {
                    ParameterDetails thisPd = thisPds[i];
                    var newPd = templatePd.CloneOfModel(nodeModel); // 复制参数描述
                    newPd.Index = i + 1; // 更新索引
                    newPd.IsParams = true;
                    newPd.DataValue = thisPd.DataValue; // 保留参数值
                    newPd.ArgDataSourceNodeGuid = thisPd.ArgDataSourceNodeGuid; // 保留参数来源信息
                    newPd.ArgDataSourceType = thisPd.ArgDataSourceType;  // 保留参数来源信息
                    newPd.IsParams = thisPd.IsParams; // 保留显式参数设置
                    newPds[index++] = newPd;
                }
            }


            var thidPdLength = thisMd.HasParamsArg ? thisMd.ParamsArgIndex : thisPds.Length;
            // 遍历当前的参数描述（不包含可变参数），找到匹配项，复制必要的数据进行保留
            for (int i = 0; i < thisPds.Length; i++)
            {
                ParameterDetails thisPd = thisPds[i];
                var newPd = newPds.FirstOrDefault(t_newPd => !t_newPd.IsParams // 不为可变参数
                                                         && t_newPd.Name.Equals(thisPd.Name, StringComparison.OrdinalIgnoreCase) // 存在相同名称
                                                         && t_newPd.DataType.Name.Equals(thisPd.DataType.Name) // 存在相同入参类型名称（以类型作为区分）
                                                         );
                if (newPd != null) // 如果匹配上了
                {
                    newPd.DataValue = thisPd.DataValue; // 保留参数值
                    newPd.ArgDataSourceNodeGuid = thisPd.ArgDataSourceNodeGuid; // 保留参数来源信息
                    newPd.ArgDataSourceType = thisPd.ArgDataSourceType;  // 保留参数来源信息
                    newPd.IsParams = thisPd.IsParams; // 保留显式参数设置
                }
            }
            thisMd.ReturnType = newMd.ReturnType;
            nodeModel.MethodDetails = newMd;

        }
#endif








    }
}
