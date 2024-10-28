using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Core.NodeFlow;
using Serein.Library.Network.WebSocketCommunication;
using Serein.Library.Web;
using Serein.NodeFlow.Env;
using Serein.NodeFlow.Model;
using System.Collections.Concurrent;

namespace Serein.NodeFlow
{
    /// <summary>
    /// 流程启动器
    /// </summary>
    public class FlowStarter
    {
        /// <summary>
        /// 控制全局触发器的结束
        /// </summary>
        private CancellationTokenSource? _flipFlopCts;
        
        /// <summary>
        /// 是否停止启动
        /// </summary>
        private bool IsStopStart = false;

        /// <summary>
        /// 结束运行时需要执行的方法
        /// </summary>
        private  Func<Task>? ExitAction { get; set; }

        private void CheckStartState()
        {
            if (IsStopStart)
            {
                throw new Exception("停止启动");

            }
        }

        /// <summary>
        /// 从选定的节点开始运行
        /// </summary>
        /// <param name="env"></param>
        /// <param name="startNode"></param>
        /// <returns></returns>
        public async Task StartFlowInSelectNodeAsync(IFlowEnvironment env, NodeModelBase startNode)
        {
            IDynamicContext context;
#if NET6_0_OR_GREATER
            context = new Serein.Library.Core.NodeFlow.DynamicContext(env); // 从起始节点启动流程时创建上下文
#else
            Context = new Serein.Library.Framework.NodeFlow.DynamicContext(env);
#endif
            await startNode.StartFlowAsync(context); // 开始运行时从选定节点开始运行
            context.Exit();

/*
 
 foreach (var node in NodeModels.Values)
 {
     if (node is not null)
     {
         node.ReleaseFlowData(); // 退出时释放对象
     }
 }
 
 
 */
        }


        /// <summary>
        /// 开始运行（需要准备好方法信息）
        /// </summary>
        /// <param name="env">运行环境</param>
        /// <param name="nodes">环境中已加载的所有节点</param>
        /// <param name="initMethods">初始化方法</param>
        /// <param name="loadingMethods">加载时方法</param>
        /// <param name="exitMethods">结束时方法</param>
        /// <returns></returns>
        public async Task RunAsync(IFlowEnvironment env,
                                   List<NodeModelBase> nodes,
                                   Dictionary<RegisterSequence, List<Type>> autoRegisterTypes,
                                   List<MethodDetails> initMethods,
                                   List<MethodDetails> loadingMethods,
                                   List<MethodDetails> exitMethods)
        {

            env.FlowState = RunState.Running; // 开始运行
            NodeModelBase? startNode = nodes.FirstOrDefault(node => node.IsStart);
            if (startNode is null) {
                env.FlowState = RunState.Completion; // 不存在起点，退出流程
                return; 
            }

            #region 获取所有触发器，以及已加载节点的方法信息
            List<MethodDetails> runNodeMd;
            List<SingleFlipflopNode> flipflopNodes;

            flipflopNodes = nodes.Where(it => it.MethodDetails?.MethodDynamicType == NodeType.Flipflop && it.IsStart == false)
                                                          .Select(it => (SingleFlipflopNode)it)
                                                          .Where(node => node is SingleFlipflopNode flipflopNode && flipflopNode.NotExitPreviousNode())
                                                          .ToList();// 获取需要再运行开始之前启动的触发器节点
            runNodeMd = nodes.Select(item => item.MethodDetails).ToList(); // 获取环境中所有节点的方法信息 


            #endregion

            #region 选择运行环境的上下文

            // 判断使用哪一种流程上下文
            IDynamicContext Context;
#if NET6_0_OR_GREATER
            Context = new Serein.Library.Core.NodeFlow.DynamicContext(env); // 从起始节点启动流程时创建上下文
#else
            Context = new Serein.Library.Framework.NodeFlow.DynamicContext(env);
#endif
            #endregion

            #region 初始化运行环境的Ioc容器

            // 清除节点使用的对象，筛选出需要初始化的方法描述
            var thisRuningMds = new List<MethodDetails>();
            thisRuningMds.AddRange(runNodeMd.Where(md => md?.ActingInstanceType is not null));
            thisRuningMds.AddRange(initMethods.Where(md => md?.ActingInstanceType is not null));
            thisRuningMds.AddRange(loadingMethods.Where(md => md?.ActingInstanceType is not null));
            thisRuningMds.AddRange(exitMethods.Where(md => md?.ActingInstanceType is not null));

            // .AddRange(initMethods).AddRange(loadingMethods).a
            foreach (var nodeMd in thisRuningMds)
            {
                nodeMd.ActingInstance = null;
            }
            
            env.IOC.CustomRegisterInstance(typeof(ISereinIOC).FullName, env);
            // 初始化ioc容器中的类型对象
            foreach (var md in thisRuningMds)
            {
                if (md.ActingInstanceType != null)
                {
                    env.IOC.Register(md.ActingInstanceType);
                }
                else
                {
                    await Console.Out.WriteLineAsync($"{md.MethodName} - 没有类型声明");
                    IsStopStart = true;
                }
            }
            
            if (IsStopStart) return;// 检查所有dll节点是否存在类型

            env.IOC.Build(); // 流程启动前的初始化

            foreach (var md in thisRuningMds)
            {
                md.ActingInstance = env.IOC.Get(md.ActingInstanceType);
                if(md.ActingInstance is null)
                {
                    await Console.Out.WriteLineAsync($"{md.MethodName} - 无法获取类型[{md.ActingInstanceType}]的实例");
                    IsStopStart = true;
                }
            }
            if (IsStopStart)
            {
                return;// 调用节点初始化后检查状态
            }


            #endregion

            #region 执行初始化，绑定IOC容器，再执行加载时

            if (autoRegisterTypes.TryGetValue(RegisterSequence.FlowInit, out var flowInitTypes))
            {
                foreach (var type in flowInitTypes)
                {
                    env.IOC.Register(type); // 初始化前注册
                }
            }
            Context.Env.IOC.Build(); // 绑定初始化时注册的类型
            //object?[]? args = [Context];
            foreach (var md in initMethods) // 初始化
            {
                if (!env.TryGetDelegateDetails(md.MethodName, out var dd))
                {
                    throw new Exception("不存在对应委托");
                }
                await dd.InvokeAsync(md.ActingInstance, [Context]);
                //((Func<object, object[], object>)dd.EmitDelegate).Invoke(md.ActingInstance, [Context]);
            }
            Context.Env.IOC.Build(); // 绑定初始化时注册的类型

            if(autoRegisterTypes.TryGetValue(RegisterSequence.FlowLoading,out var flowLoadingTypes))
            {
                foreach (var type in flowLoadingTypes)
                {
                    env.IOC.Register(type); // 初始化前注册
                }
            }
            Context.Env.IOC.Build(); // 绑定初始化时注册的类型
            foreach (var md in loadingMethods) // 加载
            {
                //object?[]? data = [md.ActingInstance, args];
                //md.MethodDelegate.DynamicInvoke(data);
                if (!env.TryGetDelegateDetails(md.MethodName, out var dd))
                {
                    throw new Exception("不存在对应委托");
                }
                await dd.InvokeAsync(md.ActingInstance, [Context]);
                //((Action<object, object?[]?>)del).Invoke(md.ActingInstance, [Context]);
                //((Func<object, object[], object>)dd.EmitDelegate).Invoke(md.ActingInstance, [Context]);
            }
            Context.Env.IOC.Build(); // 预防有人在加载时才注册类型，再绑定一次
            #endregion

            #region 设置流程退出时的回调函数
            ExitAction = async () =>
            {
                env.IOC.Run<WebApiServer>(web => {
                    web?.Stop();
                });
                env.IOC.Run<WebSocketServer>(server => {
                    server?.Stop();
                });

                foreach (MethodDetails? md in exitMethods)
                {
                    if (!env.TryGetDelegateDetails(md.MethodName, out var dd))
                    {
                        throw new Exception("不存在对应委托");
                    }
                    await dd.InvokeAsync(md.ActingInstance, [Context]);
                }

                TerminateAllGlobalFlipflop();

                if (_flipFlopCts != null && !_flipFlopCts.IsCancellationRequested)
                {
                    _flipFlopCts?.Cancel();
                    _flipFlopCts?.Dispose();
                }
                env.FlowState = RunState.Completion;
                env.FlipFlopState = RunState.Completion;

            };
            #endregion

            #region 开始启动流程
            
            try
            {
                
                if (flipflopNodes.Count > 0)
                {
                    env.FlipFlopState = RunState.Running;
                    // 如果存在需要启动的触发器，则开始启动
                    _flipFlopCts = new CancellationTokenSource();
                    env.IOC.CustomRegisterInstance(NodeStaticConfig.FlipFlopCtsName, _flipFlopCts,false);

                    // 使用 TaskCompletionSource 创建未启动的触发器任务
                    var tasks = flipflopNodes.Select(async node =>
                    {
                        await RunGlobalFlipflopAsync(env,node); // 启动流程时启动全局触发器
                    }).ToArray();
                    _ = Task.WhenAll(tasks);
                }
                await startNode.StartFlowAsync(Context); // 开始运行时从起始节点开始运行
                // 等待结束
                if(env.FlipFlopState == RunState.Running && _flipFlopCts is not null)
                {
                    while (!_flipFlopCts.IsCancellationRequested)
                    {
                        await Task.Delay(100);
                    }
                }
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync(ex.ToString());
            }
            finally
            {
                env.FlowState = RunState.Completion;
                Console.WriteLine($"流程运行完毕{Environment.NewLine}");;
            }
            #endregion
        }

        private ConcurrentDictionary<SingleFlipflopNode, CancellationTokenSource> dictGlobalFlipflop = [];

        /// <summary>
        /// 尝试添加全局触发器
        /// </summary>
        /// <param name="singleFlipFlopNode"></param>
        /// <param name="env"></param>
        public async Task RunGlobalFlipflopAsync(IFlowEnvironment env, SingleFlipflopNode singleFlipFlopNode)
        {
            if (dictGlobalFlipflop.TryAdd(singleFlipFlopNode, new CancellationTokenSource()))
            {
                singleFlipFlopNode.MethodDetails.ActingInstance ??= env.IOC.Get(singleFlipFlopNode.MethodDetails.ActingInstanceType);
                await FlipflopExecuteAsync(env, singleFlipFlopNode, dictGlobalFlipflop[singleFlipFlopNode]);
            }
        }

        /// <summary>
        /// 尝试移除全局触发器
        /// </summary>
        /// <param name="singleFlipFlopNode"></param>
        public void TerminateGlobalFlipflopRuning(SingleFlipflopNode singleFlipFlopNode)
        {
            if (dictGlobalFlipflop.TryRemove(singleFlipFlopNode, out var cts))
            {
                if (!cts.IsCancellationRequested)
                {
                    cts.Cancel();
                }
                cts.Dispose();
            }
        }
        
        /// <summary>
        /// 终结所有全局触发器
        /// </summary>
        private void TerminateAllGlobalFlipflop()
        {
            foreach ((var node, var cts) in dictGlobalFlipflop)
            {
                if (!cts.IsCancellationRequested)
                {
                    cts.Cancel();
                }
                cts.Dispose();
            }
            dictGlobalFlipflop.Clear();
        }

        /// <summary>
        /// 启动全局触发器
        /// </summary>
        /// <param name="env">流程运行全局环境</param>
        /// <param name="singleFlipFlopNode">需要全局监听信号的触发器</param>
        /// <returns></returns>
        private async Task FlipflopExecuteAsync(IFlowEnvironment env,
                                                SingleFlipflopNode singleFlipFlopNode,
                                                CancellationTokenSource cts)
        {
            if(_flipFlopCts is null)
            {
                Console.WriteLine("flowStarter -> FlipflopExecuteAsync -> _flipFlopCts is null");
                return;
            }
            while (!_flipFlopCts.IsCancellationRequested && !cts.IsCancellationRequested)
            {
                var context = new DynamicContext(env); // 启动全局触发器时新建上下文
                try
                {
                    var newFlowData = await singleFlipFlopNode.ExecutingAsync(context); // 获取触发器等待Task
                    context.AddOrUpdate(singleFlipFlopNode.Guid, newFlowData);
                    await NodeModelBase.RefreshFlowDataAndExpInterrupt(context, singleFlipFlopNode, newFlowData); // 全局触发器触发后刷新该触发器的节点数据
                    if (context.NextOrientation == ConnectionInvokeType.None)
                    {
                        continue;
                    }
                    _ = Task.Run(async () => {
                        var nextNodes = singleFlipFlopNode.SuccessorNodes[context.NextOrientation];
                        for (int i = nextNodes.Count - 1; i >= 0 && !_flipFlopCts.IsCancellationRequested; i--)
                        {
                            // 筛选出启用的节点
                            if (!nextNodes[i].DebugSetting.IsEnable)
                            {
                                continue ;
                            }

                            context.SetPreviousNode(nextNodes[i], singleFlipFlopNode);
                            if (nextNodes[i].DebugSetting.IsInterrupt) // 执行触发前
                            {
                                var cancelType = await nextNodes[i].DebugSetting.GetInterruptTask();
                                await Console.Out.WriteLineAsync($"[{nextNodes[i].MethodDetails.MethodName}]中断已{cancelType}，开始执行后继分支");
                            }
                            await nextNodes[i].StartFlowAsync(context); // 启动执行触发器后继分支的节点
                            context.Exit();
                        }
                    });
                   
                }
                catch (FlipflopException ex) 
                {
                    await Console.Out.WriteLineAsync($"触发器[{singleFlipFlopNode.MethodDetails.MethodName}]因非预期异常终止。"+ex.Message);
                    if (ex.Type == FlipflopException.CancelClass.CancelFlow)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    await Console.Out.WriteLineAsync(ex.Message);
                }
                finally
                {
                    
                }
            }

        }


        public void Exit()
        {
            ExitAction?.Invoke();
        }


    }
}




