using Serein.Library.Http;
using Serein.Library.IOC;
using Serein.NodeFlow;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Tool;
using SqlSugar;

namespace Serein.NodeFlow
{

    public class NodeRunTcs : CancellationTokenSource
    {

    }


    public class NodeFlowStarter(IServiceContainer serviceContainer, List<MethodDetails> methodDetails)

    {
        private readonly IServiceContainer ServiceContainer = serviceContainer;
        private readonly List<MethodDetails> methodDetails = methodDetails;

        private Action ExitAction = null;


        private DynamicContext context = null;


        public NodeRunTcs MainCts;

        /// <summary>
        /// 运行测试
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        //public async Task RunAsync1(List<NodeBase> nodes)
        //{
        //    await Task.Run(async ()=> await StartRunAsync(nodes));
        //}

        public async Task RunAsync(List<NodeBase> nodes)
        {
            var startNode = nodes.FirstOrDefault(p => p.IsStart);
            if (startNode == null) { return; }
            context = new(ServiceContainer);

            MainCts = ServiceContainer.CreateServiceInstance<NodeRunTcs>();

            var initMethods = methodDetails.Where(it => it.MethodDynamicType == DynamicNodeType.Init).ToList();
            var loadingMethods = methodDetails.Where(it => it.MethodDynamicType == DynamicNodeType.Loading).ToList();
            var exitMethods = methodDetails.Where(it => it.MethodDynamicType == DynamicNodeType.Exit).ToList();
            ExitAction = () =>
            {
                ServiceContainer.Run<WebServer>((web) =>
                {
                    web?.Stop();
                });
                foreach (MethodDetails? md in exitMethods)
                {
                    object?[]? args = [context];
                    object?[]? data = [md.ActingInstance, args];
                    md.MethodDelegate.DynamicInvoke(data);
                }
                if (context != null && context.NodeRunCts != null && !context.NodeRunCts.IsCancellationRequested)
                {
                    context.NodeRunCts.Cancel();
                }
                if (MainCts != null && !MainCts.IsCancellationRequested) MainCts.Cancel();
                ServiceContainer.Reset();
            };


            foreach (var md in initMethods) // 初始化 - 调用方法
            {
                //md.ActingInstance = context.ServiceContainer.Get(md.ActingInstanceType);
                object?[]? args = [context];
                object?[]? data = [md.ActingInstance, args];
                md.MethodDelegate.DynamicInvoke(data);
            }
            context.Biuld();

            foreach (var md in loadingMethods) // 加载
            {
                //md.ActingInstance = context.ServiceContainer.Get(md.ActingInstanceType);
                object?[]? args = [context];
                object?[]? data = [md.ActingInstance, args];
                md.MethodDelegate.DynamicInvoke(data);
            }

            var flipflopNodes = nodes.Where(it => it.MethodDetails?.MethodDynamicType == DynamicNodeType.Flipflop
                                               && it.PreviousNodes.Count == 0
                                               && it.IsStart != true).ToArray();

            var singleFlipflopNodes = flipflopNodes.Select(it => (SingleFlipflopNode)it).ToArray();

            // 使用 TaskCompletionSource 创建未启动的任务
            var tasks = singleFlipflopNodes.Select(async node =>
            {
                await FlipflopExecute(node);
            }).ToArray();


            try
            {
                await Task.WhenAll([startNode.ExecuteStack(context), .. tasks]);
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync(ex.ToString());
            }

        }

        private async Task FlipflopExecute(SingleFlipflopNode singleFlipFlopNode)
        {
            DynamicContext context = new DynamicContext(ServiceContainer);
            MethodDetails md = singleFlipFlopNode.MethodDetails;

            try
            {

                if (!DelegateCache.GlobalDicDelegates.TryGetValue(md.MethodName, out Delegate del))
                {
                    return;
                }

                var func = md.ExplicitDatas.Length == 0 ? (Func<object, object, Task<FlipflopContext>>)del : (Func<object, object[], Task<FlipflopContext>>)del;

                while (!MainCts.IsCancellationRequested) // 循环中直到栈为空才会退出
                {
                    object?[]? parameters = singleFlipFlopNode.GetParameters(context, md);
                    // 调用委托并获取结果

                    FlipflopContext flipflopContext = await func.Invoke(md.ActingInstance, parameters);


                    if (flipflopContext == null)
                    {
                        break;
                    }
                    else if (flipflopContext.State == FfState.Cancel)
                    {
                        break;
                    }
                    else if (flipflopContext.State == FfState.Succeed)
                    {
                        singleFlipFlopNode.FlowState = true;
                        singleFlipFlopNode.FlowData = flipflopContext.Data;
                        var tasks = singleFlipFlopNode.SucceedBranch.Select(nextNode =>
                        {
                            var context = new DynamicContext(ServiceContainer);
                            nextNode.PreviousNode = singleFlipFlopNode;
                            return nextNode.ExecuteStack(context);
                        }).ToArray();
                        Task.WaitAll(tasks);
                    }
                }
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync(ex.ToString());
            }
        }


        public void Exit()
        {
            ExitAction?.Invoke();
        }
    }
}
