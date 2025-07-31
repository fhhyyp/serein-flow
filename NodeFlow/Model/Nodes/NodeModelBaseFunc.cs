using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using System.Diagnostics;

namespace Serein.NodeFlow.Model.Nodes
{

    




    /// <summary>
    /// 节点基类
    /// </summary>
    public abstract partial class NodeModelBase : ISereinFlow
    {
        /// <summary>
        /// 实体节点创建完成后调用的方法，调用时间早于 LoadInfo() 方法
        /// </summary>
        public virtual void OnCreating()
        {

        }

        /// <summary>
        /// 保存自定义信息
        /// </summary>
        /// <returns></returns>
        public virtual NodeInfo SaveCustomData(NodeInfo nodeInfo)
        {
            return nodeInfo;
        }

        /// <summary>
        /// 加载自定义数据
        /// </summary>
        /// <param name="nodeInfo"></param>
        public virtual void LoadCustomData(NodeInfo nodeInfo)
        {
            return;
        }

        /// <summary>
        /// 执行节点对应的方法
        /// </summary>
        /// <param name="context">流程上下文</param>
        /// <param name="token"></param>
        /// <returns>节点传回数据对象</returns>
        public virtual async Task<FlowResult> ExecutingAsync(IFlowContext context, CancellationToken token)
        {
            // 执行触发检查是否需要中断
            if (DebugSetting.IsInterrupt)
            {
                context.Env.FlowControl.TriggerInterrupt(Guid, "", InterruptTriggerEventArgs.InterruptTriggerType.Monitor); // 通知运行环境该节点中断了
                await DebugSetting.GetInterruptTask.Invoke();
                SereinEnv.WriteLine(InfoType.INFO, $"[{this.MethodDetails?.MethodName}]中断已取消，开始执行后继分支");
                if (token.IsCancellationRequested) { return null; }
            }

            MethodDetails md = MethodDetails;
            if (md is null)
            {
                throw new Exception($"节点{Guid}不存在方法信息，请检查是否需要重写节点的ExecutingAsync");
            }
            if (!context.Env.TryGetDelegateDetails(md.AssemblyName, md.MethodName, out var dd))  // 流程运行到某个节点
            {
                throw new Exception($"节点{this.Guid}不存在对应委托");
            }

            if (md.IsStatic)
            {
                object[] args = md.ParameterDetailss.Length == 0 ? [] : await this.GetParametersAsync(context, token);
                var result = await dd.InvokeAsync(null, args);
                var flowReslt =  FlowResult.OK(this.Guid, context, result);
                return flowReslt;
            }
            else
            {
                var instance = Env.FlowControl.IOC.Get(md.ActingInstanceType);
                if (instance is null)
                {
                    Env.FlowControl.IOC.Register(md.ActingInstanceType).Build();
                    instance = Env.FlowControl.IOC.Get(md.ActingInstanceType);
                }
                object[] args = await this.GetParametersAsync(context, token);
                var result = await dd.InvokeAsync(instance, args);
                var flowReslt = FlowResult.OK(this.Guid, context, result);
                return flowReslt;
            }
            

        }


        private readonly static ObjectPool<Stack<IFlowNode>> flowStackPool = new ObjectPool<Stack<IFlowNode>>(() => new Stack<IFlowNode>());

        /// <summary>
        /// 开始执行
        /// </summary>
        /// <param name="context"></param>
        /// <param name="token">流程运行</param>
        /// <returns></returns>
        public async Task<FlowResult> StartFlowAsync(IFlowContext context, CancellationToken token)
        {
#if false

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
            sw.Restart();
            checkpoints.Clear();
#endif
            IFlowNode? previousNode = null;
            IFlowNode? currentNode = null;
            FlowResult? flowResult = null;

            IFlowNode nodeModel = this;
            Stack<IFlowNode> flowStack = flowStackPool.Allocate();
            flowStack.Push(nodeModel);
            //HashSet<IFlowNode> processedNodes = processedNodesPool.Allocate() ; // 用于记录已处理上游节点的节点
#if false
            checkpoints[$"[{nodeModel?.Guid}]\t流程开始准备"] = sw.Elapsed;
#endif
            try
            {
                while (true)
                {

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
#if false
                    checkpoints[$"[{currentNode.Guid}]\t创建调用信息"] = sw.Elapsed;
#endif

                Label_NotRecordInvoke:
                    context.NextOrientation = ConnectionInvokeType.IsSucceed; // 默认执行成功
                    
                    try
                    {
                        flowResult = await currentNode.ExecutingAsync(context, token);
                    }
                    catch (Exception ex)
                    {
                        flowResult = FlowResult.Fail(currentNode.Guid, context, ex.Message);
                        context.Env.WriteLine(InfoType.ERROR, $"节点[{currentNode.Guid}]异常：" + ex);
                        context.NextOrientation = ConnectionInvokeType.IsError;
                        context.ExceptionOfRuning = ex;
                    }
#if false
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

#if false
                    checkpoints[$"[{currentNode.Guid}]\t更新调用信息"] = sw.Elapsed;
#endif

                    #region 执行完成时更新栈
                    context.AddOrUpdateFlowData(currentNode.Guid, flowResult); // 上下文中更新数据
#if false
                    checkpoints[$"[{currentNode.Guid}]\t执行完成时更新栈"] = sw.Elapsed;
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

#if false
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

#if false
                    checkpoints[$"[{currentNode.Guid}]\t上游分支推入栈中"] = sw.Elapsed;
#endif
                    #endregion




                    #region 执行完成后检查
                    if (flowStack.Count == 0)
                    {
                        break;  // 说明流程到了终点
                    }

                    if (context.RunState == RunState.Completion)
                    {
                        flowResult = null;
                        currentNode.Env.WriteLine(InfoType.INFO, $"流程执行到节点[{currentNode.Guid}]时提前结束，未能获取到流程结果。");
                        break; // 流程执行完成，返回结果
                    }

                    if (token.IsCancellationRequested)
                    {
                        flowResult = null;
                        currentNode.Env.WriteLine(InfoType.INFO, "流程执行到节点[{currentNode.Guid}]时被取消，未能获取到流程结果。");
                        break;
                    }

                    #endregion
                }
            }
            finally
            {
                flowStackPool.Free(flowStack);

            }

#if false
            var theTS = sw.Elapsed;
            checkpoints[$"[{nodeModel?.Guid}]\t流程完毕"] = theTS;
            var last = TimeSpan.Zero;
            foreach (var kv in checkpoints)
            {
                SereinEnv.WriteLine(InfoType.INFO, $"{kv.Key} 耗时: {(kv.Value - last).TotalMilliseconds} ms");
                last = kv.Value;
            }

            SereinEnv.WriteLine(InfoType.INFO, $"总耗时：{theTS.TotalSeconds} ms");
            SereinEnv.WriteLine(InfoType.INFO, $"------");

#endif
            return flowResult;
        }



        /// <summary>
        /// 获取所有参数
        /// </summary>
        /// <param name="context"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<object[]> GetParametersAsync(IFlowContext context, CancellationToken token)
        {
            IFlowNode nodeModel = this;
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

    }


}
