using Microsoft.CodeAnalysis;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Tool;
using System;
using System.Collections.Concurrent;
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
        private ConcurrentDictionary<SingleFlipflopNode, CancellationTokenSource> dictGlobalFlipflop = [];


        /// <summary>
        /// 结束运行时需要执行的方法
        /// </summary>
        private  Func<Task>? ExitAction { get; set; }

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
            #region 注册所有节点所属的类的类型，如果注册失败则退出
            List<IFlowNode> nodes = new List<IFlowNode>();
            foreach (var item in WorkOptions.Flows.Values)
            {
                var temp = item.GetNodes();
                nodes.AddRange(temp);
            }
            if (!RegisterAllType(nodes))
            {
                return false;
            }
            #endregion

            #region 调用所有流程类的Init、Load事件

            var initState = await TryInit();
            if (!initState)
            {
                return false;
            }
            ;
            var loadState = await TryLoadAsync();
            if (!loadState)
            {
                return false;
            }
            ;
            #endregion

            // 开始调用流程
            foreach (var kvp in WorkOptions.Flows)
            {
                var guid = kvp.Key;
                var flow = kvp.Value;
                var flowNodes = flow.GetNodes();

                // 找到流程的起始节点，开始运行
                IFlowNode startNode = flow.GetStartNode();
                // 是否后台运行当前画布流程
                if (flow.IsTaskAsync)
                {
                    _ = Task.Run(async () => await CallStartNode(startNode), token); // 后台调用流程中的触发器

                }
                else
                {
                    await CallStartNode(startNode);

                }
                _ = Task.Run(async () => await CallFlipflopNode(flow), token); // 后台调用流程中的触发器
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

            

            var nodeMds = nodes.Select(item => item.MethodDetails).ToList(); // 获取环境中所有节点的方法信息 
            var allMds = new List<MethodDetails>();
            allMds.AddRange(nodeMds.Where(md => md?.ActingInstanceType is not null));
            allMds.AddRange(WorkOptions.InitMds.Where(md => md?.ActingInstanceType is not null));
            allMds.AddRange(WorkOptions.LoadMds.Where(md => md?.ActingInstanceType is not null));
            allMds.AddRange(WorkOptions.ExitMds.Where(md => md?.ActingInstanceType is not null));
            var isSuccessful = true;
            foreach (var md in allMds)
            {
                if (md.ActingInstanceType != null)
                {
                    env.IOC.Register(md.ActingInstanceType);
                }
                else
                {
                    SereinEnv.WriteLine(InfoType.ERROR, "{md.MethodName} - 没有类型声明");
                    isSuccessful = false ;
                }
            }
            env.IOC.Build(); // 绑定初始化时注册的类型
            foreach (var md in allMds)
            {
                var instance = env.IOC.Get(md.ActingInstanceType);
                if (instance is null)
                {
                    SereinEnv.WriteLine(InfoType.ERROR, $"{md.MethodName} - 无法获取类型[{md.ActingInstanceType}]的实例");
                    isSuccessful = false;
                }
            }

            return isSuccessful;
        }

        private async Task<bool> TryInit()
        {
            var env = WorkOptions.Environment;
            var initMds = WorkOptions.InitMds;
            var pool = WorkOptions.FlowContextPool;
            var ioc = WorkOptions.Environment.IOC;
            foreach (var md in initMds) // 初始化
            {
                if (!env.TryGetDelegateDetails(md.AssemblyName, md.MethodName, out var dd)) // 流程运行初始化
                {
                    throw new Exception("不存在对应委托");
                }
                var context = pool.Allocate();
                var instance = ioc.Get(md.ActingInstanceType);
                await dd.InvokeAsync(instance, [context]);
                context.Reset();
                pool.Free(context);
            }
            env.IOC.Build(); // 绑定初始化时注册的类型
            var isSuccessful = true;
            return isSuccessful;
        }
        private async Task<bool> TryLoadAsync()
        {
            var env = WorkOptions.Environment;
            var loadMds = WorkOptions.LoadMds;
            var pool = WorkOptions.FlowContextPool;
            var ioc = WorkOptions.Environment.IOC;
            foreach (var md in loadMds) // 加载时
            {
                if (!env.TryGetDelegateDetails(md.AssemblyName, md.MethodName, out var dd)) // 流程运行初始化
                {
                    throw new Exception("不存在对应委托");
                }
                var context = pool.Allocate();
                var instance = ioc.Get(md.ActingInstanceType);
                await dd.InvokeAsync(instance, [context]);
                context.Reset();
                pool.Free(context);
            }
            env.IOC.Build(); // 绑定初始化时注册的类型
            var isSuccessful = true;
            return isSuccessful;

        }
        private async Task<bool> CallExit()
        {
            var env = WorkOptions.Environment;
            var mds = WorkOptions.ExitMds;
            var pool = WorkOptions.FlowContextPool;
            var ioc = WorkOptions.Environment.IOC;

            // var fit = ioc.Get<FlowInterruptTool>();
            // fit.CancelAllTrigger(); // 取消所有中断
            foreach (var md in mds) // 结束时
            {
                if (!env.TryGetDelegateDetails(md.AssemblyName, md.MethodName, out var dd)) // 流程运行初始化
                {
                    throw new Exception("不存在对应委托");
                }
                var context = pool.Allocate();
                var instance = ioc.Get(md.ActingInstanceType);
                await dd.InvokeAsync(instance, [context]);
                context.Reset();
                pool.Free(context);
            }

            TerminateAllGlobalFlipflop(); // 确保所有触发器不再运行
            SereinEnv.ClearFlowGlobalData(); // 清空全局数据缓存
            NativeDllHelper.FreeLibrarys(); // 卸载所有已加载的 Native Dll

            var isSuccessful = true;
            return isSuccessful;
        }

        private async Task CallFlipflopNode(FlowTask flow)
        {
            var env = WorkOptions.Environment;
            var flipflopNodes = flow.GetNodes().Where(item => item is SingleFlipflopNode node
                                                 
                                                  && node.DebugSetting.IsEnable
                                                  && node.NotExitPreviousNode())
                                        .Select(item => (SingleFlipflopNode)item);
                                        //.ToList();// 获取需要再运行开始之前启动的触发器节点

            if (flipflopNodes.Count() > 0)
            {
                var tasks = flipflopNodes.Select(async node =>
                {
                    await RunGlobalFlipflopAsync(env, node); // 启动流程时启动全局触发器
                });
                await Task.WhenAll(tasks);
            }
        }

        /// <summary>
        /// 从某一个节点开始执行
        /// </summary>
        /// <param name="startNode"></param>
        /// <returns></returns>
        private async Task CallStartNode(IFlowNode startNode)
        {
            var pool = WorkOptions.FlowContextPool;
            var token = WorkOptions.CancellationTokenSource.Token;
            var context = pool.Allocate();
            context.Reset();
            await startNode.StartFlowAsync(context, token);
            context.Exit();
            pool.Free(context);
            return;
        }

        #endregion

        /// <summary>
        /// 从选定的节点开始运行
        /// </summary>
        /// <param name="startNode"></param>
        /// <returns></returns>
        public async Task StartFlowInSelectNodeAsync(IFlowNode  startNode)
        {
            var pool = WorkOptions.FlowContextPool;
            var context = pool.Allocate();
            var token = WorkOptions.CancellationTokenSource.Token;
            await startNode.StartFlowAsync(context, token); // 开始运行时从选定节点开始运行
            context.Reset();
            pool.Free(context);
        }


     

        /// <summary>
        /// 尝试添加全局触发器
        /// </summary>
        /// <param name="singleFlipFlopNode"></param>
        /// <param name="env"></param>
        public async Task RunGlobalFlipflopAsync(IFlowEnvironment env, SingleFlipflopNode singleFlipFlopNode)
        {
            if (dictGlobalFlipflop.TryAdd(singleFlipFlopNode, new CancellationTokenSource()))
            {
                var cts = dictGlobalFlipflop[singleFlipFlopNode];
                await FlipflopExecuteAsync(singleFlipFlopNode, cts.Token);
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
        /// <param name="singleFlipFlopNode">需要全局监听信号的触发器</param>
        /// <param name="singleToken">单个触发器持有的</param>
        /// <returns></returns>
        private async Task FlipflopExecuteAsync(SingleFlipflopNode singleFlipFlopNode,
                                                CancellationToken singleToken)
        {

            var pool = WorkOptions.FlowContextPool;
            while (!singleToken.IsCancellationRequested && !singleToken.IsCancellationRequested)
            {
                try
                {
                    var context = pool.Allocate(); // 启动全局触发器时新建上下文
                    var newFlowData = await singleFlipFlopNode.ExecutingAsync(context, singleToken); // 获取触发器等待Task
                    context.AddOrUpdate(singleFlipFlopNode, newFlowData);
                    if (context.NextOrientation == ConnectionInvokeType.None)
                    {
                        continue;
                    }
                    _ = Task.Run(() => CallSubsequentNode(singleFlipFlopNode, singleToken, pool, context)); // 重新启动触发器

                }
                catch (FlipflopException ex) 
                {
                    SereinEnv.WriteLine(InfoType.ERROR, $"触发器[{singleFlipFlopNode.MethodDetails.MethodName}]因非预期异常终止。"+ex.Message);
                    if (ex.Type == FlipflopException.CancelClass.CancelFlow)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    SereinEnv.WriteLine(InfoType.ERROR, $"触发器[{singleFlipFlopNode.Guid}]异常。"+ ex.Message);
                    await Task.Delay(100);
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
        private static async Task? CallSubsequentNode(SingleFlipflopNode singleFlipFlopNode, CancellationToken singleToken, ObjectPool<IDynamicContext> pool, IDynamicContext context)
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
                context.SetPreviousNode(nextNodes[i], singleFlipFlopNode); // 设置调用关系

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

                context.SetPreviousNode(nextNodes[i], singleFlipFlopNode);
                if (nextNodes[i].DebugSetting.IsInterrupt) // 执行触发前
                {
                    await nextNodes[i].DebugSetting.GetInterruptTask.Invoke();
                    await Console.Out.WriteLineAsync($"[{nextNodes[i].MethodDetails.MethodName}]中断已取消，开始执行后继分支");
                }
                await nextNodes[i].StartFlowAsync(context, singleToken); // 启动执行触发器后继分支的节点
            }

            context.Reset();
            pool.Free(context);
        }

        /// <summary>
        /// 结束流程
        /// </summary>
        public void Exit()
        {
            ExitAction?.Invoke();

        }

    }
}




