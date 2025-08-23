using Microsoft.CodeAnalysis;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Model.Nodes;
using Serein.NodeFlow.Tool;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks.Dataflow;
using System.Xml.Linq;

namespace Serein.NodeFlow.Services
{
    /// <summary>
    /// 流程任务管理
    /// </summary>
    public class FlowWorkManagement
    {
        /// <summary>
        /// 触发器对应的Cts
        /// </summary>
        private ConcurrentDictionary<SingleFlipflopNode, CancellationTokenSource> _globalFlipflops = [];

        /// <summary>
        /// 结束运行时需要执行的方法
        /// </summary>
        private  Func<Task>? _exitAction { get; set; }

        /// <summary>
        /// 初始化选项
        /// </summary>
        public FlowWorkOptions WorkOptions { get; }

        /// <summary>
        /// 流程任务管理
        /// </summary>
        /// <param name="options"></param>
        public FlowWorkManagement(FlowWorkOptions options)
        {
            WorkOptions = options;
          
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <returns></returns>
        public async Task<bool> RunAsync(CancellationToken token)
        {
            var sw = Stopwatch.StartNew();
            var checkpoints = new Dictionary<string, TimeSpan>();


            #region 注册所有节点所属的类的类型，如果注册失败则退出
            List<IFlowNode> nodes = new List<IFlowNode>();
            var flowTask = WorkOptions.Flows.Values.ToArray();
            foreach (var item in flowTask)
            {
                var temp = item?.GetNodes?.Invoke() ;
                if (temp is null)
                    continue;
                nodes.AddRange(temp);
            }
            if (!RegisterAllType(nodes))
            {
                return false;
            }
            checkpoints["注册所有节点类型"] = sw.Elapsed; // 记录注册所有节点类型的时间
            #endregion

            #region 调用所有流程类的Init、Load事件

            var initState = await TryInit();
            if (!initState) 
                return false;
            checkpoints["调用Init事件"] = sw.Elapsed; // 记录调用Init事件的时间
            var loadState = await TryLoadAsync();
            if (!loadState) 
                return false;
            checkpoints["调用Load事件"] = sw.Elapsed; // 记录调用Load事件的时间
            #endregion
            var last = TimeSpan.Zero;
            foreach (var kv in checkpoints)
            {
                SereinEnv.WriteLine(InfoType.INFO, $"{kv.Key} 耗时: {(kv.Value - last).TotalMilliseconds} ms");
                last = kv.Value;
            }

            // 开始调用流程
            foreach (var kvp in WorkOptions.Flows)
            {
                var guid = kvp.Key;
                var flow = kvp.Value;
                var flowNodes = flow.GetNodes?.Invoke();
                if (flowNodes is null)
                    continue;
                IFlowNode? startNode = flow.GetStartNode?.Invoke();
                // 找到流程的起始节点，开始运行
                if (startNode is null)
                    continue;
                // 是否后台运行当前画布流程
                if (flow.IsWaitStartFlow)
                {
                    _ = Task.Run(async () => await CallNode(startNode), token); // 后台调用流程中的触发器
                }
                else
                {
                    await CallNode(startNode);

                }
                await CallFlipflopNode(flow); // 后台调用流程中的触发器
            }

            // 等待流程运行完成
            await CallExit();

            return true;
        }

        #region 初始化
        /// <summary>
        /// 初始化节点所需的所有类型
        /// </summary>
        /// <returns></returns>
        private bool RegisterAllType(List<IFlowNode> nodes)
        {
            var env = WorkOptions.Environment;
            var ioc = WorkOptions.FlowIOC;

            HashSet<Type> types = new HashSet<Type>();
            var nodeMds = nodes.Select(item => item.MethodDetails).ToList(); // 获取环境中所有节点的方法信息 
            var allMds = new List<MethodDetails>();
            IEnumerable<MethodDetails> Where(IEnumerable<MethodDetails> mds)
            {
                return mds.Where(md => md?.ActingInstanceType is not null && !md.ActingInstanceType.IsSealed && !md.ActingInstanceType.IsAbstract);
            }
            allMds.AddRange(Where(nodeMds));
            allMds.AddRange(Where(WorkOptions.InitMds));
            allMds.AddRange(Where(WorkOptions.ExitMds));
            allMds.AddRange(Where(WorkOptions.LoadMds));
            var isSuccessful = true;
            foreach (var md in allMds)
            {
               
                Type? type = md.ActingInstanceType;
                if (type is null)
                {
                    SereinEnv.WriteLine(InfoType.ERROR, "{md.MethodName} - 没有类型声明");
                    isSuccessful = false;
                }
                else if (types.Add(type))
                {
                    ioc.Register(md.ActingInstanceType);
                }
            }
            if(types.Count == 0)
            {
                return true;
            }

            ioc.Build(); // 绑定初始化时注册的类型
            foreach (var md in allMds)
            {
                var instance = ioc.Get(md.ActingInstanceType);
                if (instance is null)
                {
                    SereinEnv.WriteLine(InfoType.ERROR, $"{md.MethodName} - 无法获取类型[{md.ActingInstanceType}]的实例");
                    isSuccessful = false;
                }
            }

            return isSuccessful;
        }

        /// <summary>
        /// 尝试初始化
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task<bool> TryInit()
        {
            var env = WorkOptions.Environment;
            var initMds = WorkOptions.InitMds;
            var pool = WorkOptions.FlowContextPool;
            var ioc = WorkOptions.FlowIOC;
            foreach (var md in initMds) // 初始化
            {
                if (!env.TryGetDelegateDetails(md.AssemblyName, md.MethodName, out var dd)) // 流程运行初始化
                {
                    throw new Exception("不存在对应委托");
                }
                var context = pool.Allocate();
                var instance = ioc.Get(md.ActingInstanceType);
                await dd.InvokeAsync(instance, [context]);
                pool.Free(context);
            }
            ioc.Build(); // 绑定初始化时注册的类型
            var isSuccessful = true;
            return isSuccessful;
        }

        /// <summary>
        /// 尝试加载流程
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task<bool> TryLoadAsync()
        {
            var env = WorkOptions.Environment;
            var loadMds = WorkOptions.LoadMds;
            var pool = WorkOptions.FlowContextPool;
            var ioc = WorkOptions.FlowIOC;
            foreach (var md in loadMds) // 加载时
            {
                if (!env.TryGetDelegateDetails(md.AssemblyName, md.MethodName, out var dd)) // 流程运行初始化
                {
                    throw new Exception("不存在对应委托");
                }
                var context = pool.Allocate();
                var instance = ioc.Get(md.ActingInstanceType);
                await dd.InvokeAsync(instance, [context]);
                pool.Free(context);
            }
            ioc.Build(); // 绑定初始化时注册的类型
            var isSuccessful = true;
            return isSuccessful;

        }

        /// <summary>
        /// 结束流程时调用的方法
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task<bool> CallExit()
        {
            var env = WorkOptions.Environment;
            var mds = WorkOptions.ExitMds;
            var pool = WorkOptions.FlowContextPool;
            var ioc = WorkOptions.FlowIOC;

            foreach (var md in mds) // 结束时
            {
                if (!env.TryGetDelegateDetails(md.AssemblyName, md.MethodName, out var dd)) // 流程运行初始化
                {
                    throw new Exception("不存在对应委托");
                }
                var context = pool.Allocate();
                var instance = ioc.Get(md.ActingInstanceType);
                await dd.InvokeAsync(instance, [context]);
                pool.Free(context);
            }

            TerminateAllGlobalFlipflop(); // 确保所有触发器不再运行
            SereinEnv.ClearFlowGlobalData(); // 清空全局数据缓存
            NativeDllHelper.FreeLibrarys(); // 卸载所有已加载的 Native Dll

            var isSuccessful = true;
            return isSuccessful;
        }

        /// <summary>
        /// 调用流程中的触发器节点
        /// </summary>
        /// <param name="flow"></param>
        /// <returns></returns>
        private async Task CallFlipflopNode(FlowTask flow)
        {
            var env = WorkOptions.Environment;
            var nodes = flow.GetNodes?.Invoke();
            if (nodes is null)
            {
                SereinEnv.WriteLine(InfoType.WARN, "流程中没有触发器节点可供执行");
                return;
            }
            var flipflopNodes = nodes.Where(item => item is SingleFlipflopNode node
                                                  && node.DebugSetting.IsEnable
                                                  && node.NotExitPreviousNode())
                                        .OfType<SingleFlipflopNode>()
                                        .Select(async node =>
                                        {
                                            await RunGlobalFlipflopAsync(env, node); // 启动流程时启动全局触发器
                                        });
            var tasks = flipflopNodes.ToArray();
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 从某个节点开始执行
        /// </summary>
        /// <param name="startNode"></param>
        /// <returns></returns>
        private async Task CallNode(IFlowNode startNode)
        {
            var pool = WorkOptions.FlowContextPool;
            var token = WorkOptions.CancellationTokenSource.Token;
            var context = pool.Allocate();
            await startNode.StartFlowAsync(context, token);
            pool.Free(context);
            return;
        }

        #endregion

        /// <summary>
        /// 从选定的节点开始运行
        /// </summary>
        /// <param name="startNode"></param>
        /// <returns></returns>
        public async Task<FlowResult> StartFlowInSelectNodeAsync(IFlowNode startNode)
        {
            var pool = WorkOptions.FlowContextPool;

            var sw = Stopwatch.StartNew();
            var checkpoints = new Dictionary<string, TimeSpan>();

           
            var context = pool.Allocate();
            checkpoints["准备Context"] = sw.Elapsed;

            var result = await startNode.StartFlowAsync(context, WorkOptions.CancellationTokenSource.Token); // 开始运行时从选定节点开始运行
            checkpoints["执行流程"] = sw.Elapsed;

            pool.Free(context);
            checkpoints["释放Context"] = sw.Elapsed;

            _ = Task.Run(() =>
            {
                //var checkpoints = new Dictionary<string, TimeSpan>();
                var last = TimeSpan.Zero;
                foreach (var kv in checkpoints)
                {
                    SereinEnv.WriteLine(InfoType.INFO, $"{kv.Key} 耗时: {(kv.Value - last).TotalMilliseconds} ms");
                    last = kv.Value;
                }
            });

            return result;
        }

        /// <summary>
        /// 运行全局触发器
        /// </summary>
        /// <param name="singleFlipFlopNode"></param>
        /// <param name="env"></param>
        public async Task RunGlobalFlipflopAsync(IFlowEnvironment env, SingleFlipflopNode singleFlipFlopNode)
        {
            using var cts = new CancellationTokenSource();
            if (_globalFlipflops.TryAdd(singleFlipFlopNode, cts))
            {
                await FlipflopExecuteAsync(singleFlipFlopNode, cts.Token);
            }
        }

        /// <summary>
        /// 尝试移除全局触发器
        /// </summary>
        /// <param name="singleFlipFlopNode"></param>
        public void TerminateGlobalFlipflopRuning(SingleFlipflopNode singleFlipFlopNode)
        {
            if (_globalFlipflops.TryRemove(singleFlipFlopNode, out var cts))
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
            foreach ((var node, var cts) in _globalFlipflops)
            {
                if (!cts.IsCancellationRequested)
                {
                    cts.Cancel();
                }
                cts.Dispose();
            }
            _globalFlipflops.Clear();
        }

        /// <summary>
        /// 启动全局触发器
        /// </summary>
        /// <param name="flipflopNode">需要全局监听信号的触发器</param>
        /// <param name="token">单个触发器持有的</param>
        /// <returns></returns>
        private async Task FlipflopExecuteAsync(SingleFlipflopNode flipflopNode,
                                                CancellationToken token)
        {

            var pool = WorkOptions.FlowContextPool;
            while (true)
            {
                if(token.IsCancellationRequested)
                {
                    break;
                }
                var context = pool.Allocate(); // 从上下文池取出新的实例
                try
                {
                    var result = await flipflopNode.ExecutingAsync(context, token); // 等待触发获取结果
                    context.AddOrUpdateFlowData(flipflopNode.Guid, result);
                    if (context.NextOrientation == ConnectionInvokeType.None)
                    {
                        continue;
                    }
                    await CallSuccessorNodesAsync(flipflopNode, token, pool, context);
                }
                catch (FlipflopException ex) 
                {
                    SereinEnv.WriteLine(InfoType.ERROR, $"触发器[{flipflopNode.MethodDetails.MethodName}]因非预期异常终止。"+ex.Message);
                    if (ex.Type == FlipflopException.CancelClass.CancelFlow)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    SereinEnv.WriteLine(InfoType.ERROR, $"触发器[{flipflopNode.Guid}]异常。"+ ex.Message);
                    await Task.Delay(100);
                }
                finally
                {
                    pool.Free(context);
                }
            }

        }

        /// <summary>
        /// 全局触发器触发后的调用
        /// </summary>
        /// <param name="singleFlipFlopNode"></param>
        /// <param name="singleToken"></param>
        /// <param name="pool"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        private static async Task CallSuccessorNodesAsync(SingleFlipflopNode singleFlipFlopNode, CancellationToken singleToken, ObjectPool<IFlowContext> pool, IFlowContext context)
        {
            var flowState = context.NextOrientation; // 记录一下流程状态
            var nextNodes = singleFlipFlopNode.SuccessorNodes[ConnectionInvokeType.Upstream]; // 优先调用上游分支
            for (int i = nextNodes.Count - 1; i >= 0 && !singleToken.IsCancellationRequested; i--)
            {
                // 筛选出启用的节点
                if (!nextNodes[i].DebugSetting.IsEnable)
                {
                    continue;
                }
                context.SetPreviousNode(nextNodes[i].Guid, singleFlipFlopNode.Guid); // 设置调用关系

                if (nextNodes[i].DebugSetting.IsInterrupt) // 执行触发前检查终端
                {
                    await nextNodes[i].DebugSetting.GetInterruptTask.Invoke(); 
                    await Console.Out.WriteLineAsync($"[{nextNodes[i].MethodDetails.MethodName}]中断已取消，开始执行后继分支");
                }
                await nextNodes[i].StartFlowAsync(context, singleToken); // 启动执行触发器后继分支的节点
            }

            nextNodes = singleFlipFlopNode.SuccessorNodes[flowState]; // 调用对应分支
            for (int i = nextNodes.Count - 1; i >= 0 && !singleToken.IsCancellationRequested; i--)
            {
                // 筛选出启用的节点
                if (!nextNodes[i].DebugSetting.IsEnable)
                {
                    continue;
                }

                context.SetPreviousNode(nextNodes[i].Guid, singleFlipFlopNode.Guid);
                if (nextNodes[i].DebugSetting.IsInterrupt) // 执行触发前
                {
                    await nextNodes[i].DebugSetting.GetInterruptTask.Invoke();
                    await Console.Out.WriteLineAsync($"[{nextNodes[i].MethodDetails.MethodName}]中断已取消，开始执行后继分支");
                }
                await nextNodes[i].StartFlowAsync(context, singleToken); // 启动执行触发器后继分支的节点
            }

        }

        /// <summary>
        /// 结束流程
        /// </summary>
        public void Exit()
        {
            _exitAction?.Invoke();

        }

    }
}




