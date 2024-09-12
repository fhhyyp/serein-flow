using Serein.Library.Api;
using Serein.Library.Enums;
using Serein.Library.Core.NodeFlow;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Tool;

namespace Serein.NodeFlow
{

    public class NodeRunTcs : CancellationTokenSource
    {

    }


    public class NodeFlowStarter(ISereinIoc serviceContainer, List<MethodDetails> methodDetails)

    {
        private readonly ISereinIoc ServiceContainer = serviceContainer;
        private readonly List<MethodDetails> methodDetails = methodDetails;

        private Action ExitAction = null;


        private IDynamicContext context = null;


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
            if (false)
            {
                context = new Serein.Library.Core.NodeFlow.DynamicContext(ServiceContainer);
            }
            else
            {
                context = new Serein.Library.Framework.NodeFlow.DynamicContext(ServiceContainer);
            }

            MainCts = ServiceContainer.CreateServiceInstance<NodeRunTcs>();

            var initMethods = methodDetails.Where(it => it.MethodDynamicType == NodeType.Init).ToList();
            var loadingMethods = methodDetails.Where(it => it.MethodDynamicType == NodeType.Loading).ToList();
            var exitMethods = methodDetails.Where(it => it.MethodDynamicType == NodeType.Exit).ToList();
            ExitAction = () =>
            {
                //ServiceContainer.Run<WebServer>((web) =>
                //{
                //    web?.Stop();
                //});
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
            context.SereinIoc.Build();

            foreach (var md in loadingMethods) // 加载
            {
                //md.ActingInstance = context.ServiceContainer.Get(md.ActingInstanceType);
                object?[]? args = [context];
                object?[]? data = [md.ActingInstance, args];
                md.MethodDelegate.DynamicInvoke(data);
            }

            var flipflopNodes = nodes.Where(it => it.MethodDetails?.MethodDynamicType == NodeType.Flipflop
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
                await Task.Run(async () =>
                {
                    await Task.WhenAll([startNode.StartExecution(context), .. tasks]);
                });
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
                

                //var func = md.ExplicitDatas.Length == 0 ? (Func<object, object, Task<FlipflopContext<dynamic>>>)del : (Func<object, object[], Task<FlipflopContext<dynamic>>>)del;
                var func = md.ExplicitDatas.Length == 0 ? (Func<object, object, Task<IFlipflopContext>>)del : (Func<object, object[], Task<IFlipflopContext>>)del;

                while (!MainCts.IsCancellationRequested) // 循环中直到栈为空才会退出
                {
                    object?[]? parameters = singleFlipFlopNode.GetParameters(context, md);
                    // 调用委托并获取结果

                    IFlipflopContext flipflopContext = await func.Invoke(md.ActingInstance, parameters);

                    if (flipflopContext == null)
                    {
                        break;
                    }
                    else if (flipflopContext.State == FlowStateType.Error)
                    {
                        break;
                    }
                    else if (flipflopContext.State == FlowStateType.Fail)
                    {
                        break;
                    }
                    else if (flipflopContext.State == FlowStateType.Succeed)
                    {
                        singleFlipFlopNode.FlowState = FlowStateType.Succeed;
                        singleFlipFlopNode.FlowData = flipflopContext.Data;
                        var tasks = singleFlipFlopNode.SucceedBranch.Select(nextNode =>
                        {
                            var context = new DynamicContext(ServiceContainer);
                            nextNode.PreviousNode = singleFlipFlopNode;
                            return nextNode.StartExecution(context);
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
