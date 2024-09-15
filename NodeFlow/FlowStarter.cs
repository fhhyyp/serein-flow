using Serein.Library.Api;
using Serein.Library.Core.NodeFlow;
using Serein.Library.Entity;
using Serein.Library.Enums;
using Serein.Library.Utils;
using Serein.NodeFlow.Base;
using Serein.NodeFlow.Model;

namespace Serein.NodeFlow
{

    /// <summary>
    /// 流程启动器
    /// </summary>
    /// <param name="serviceContainer"></param>
    /// <param name="methodDetails"></param>
    public class FlowStarter(ISereinIoc serviceContainer, List<MethodDetails> methodDetails)

    {
        private readonly ISereinIoc ServiceContainer = serviceContainer;
        private readonly List<MethodDetails> methodDetails = methodDetails;
        private Action ExitAction = null; //退出方法
        private IDynamicContext context = null;  //上下文
        public NodeRunCts MainCts;

        /// <summary>
        /// 开始运行
        /// </summary>
        /// <param name="nodes"></param>
        /// <returns></returns>
        // public async Task RunAsync(List<NodeModelBase> nodes, IFlowEnvironment flowEnvironment)
        public async Task RunAsync(NodeModelBase startNode, IFlowEnvironment flowEnvironment, List<SingleFlipflopNode> flipflopNodes)
        {
            // var startNode = nodes.FirstOrDefault(p => p.IsStart);
            if (startNode == null) { return; }

            var isNetFramework = true;

            if (isNetFramework)
            {
                context = new Serein.Library.Framework.NodeFlow.DynamicContext(ServiceContainer, flowEnvironment);
            }
            else
            {
                context = new Serein.Library.Core.NodeFlow.DynamicContext(ServiceContainer, flowEnvironment);
            }

            MainCts = ServiceContainer.CreateServiceInstance<NodeRunCts>();

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
                object?[]? args = [context];
                object?[]? data = [md.ActingInstance, args];
                md.MethodDelegate.DynamicInvoke(data);
            }
            context.SereinIoc.Build();

            foreach (var md in loadingMethods) // 加载
            {
                object?[]? args = [context];
                object?[]? data = [md.ActingInstance, args];
                md.MethodDelegate.DynamicInvoke(data);
            }

            // 运行触发器节点
            var singleFlipflopNodes = flipflopNodes.Select(it => (SingleFlipflopNode)it).ToArray();

            // 使用 TaskCompletionSource 创建未启动的任务
            var tasks = singleFlipflopNodes.Select(async node =>
            {
                await FlipflopExecute(node, flowEnvironment);
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

        /// <summary>
        /// 启动触发器
        /// </summary>
        private async Task FlipflopExecute(SingleFlipflopNode singleFlipFlopNode, IFlowEnvironment flowEnvironment)
        {
            DynamicContext context = new DynamicContext(ServiceContainer, flowEnvironment);
            MethodDetails md = singleFlipFlopNode.MethodDetails;
            var del = md.MethodDelegate;
            try
            {


                //var func = md.ExplicitDatas.Length == 0 ? (Func<object, object, Task<FlipflopContext<dynamic>>>)del : (Func<object, object[], Task<FlipflopContext<dynamic>>>)del;
                var func = md.ExplicitDatas.Length == 0 ? (Func<object, object, Task<IFlipflopContext>>)del : (Func<object, object[], Task<IFlipflopContext>>)del;

                while (!MainCts.IsCancellationRequested) // 循环中直到栈为空才会退出
                {
                    object?[]? parameters = singleFlipFlopNode.GetParameters(context, md);
                    // 调用委托并获取结果

                    IFlipflopContext flipflopContext = await func.Invoke(md.ActingInstance, parameters);

                    if (flipflopContext.State == FlowStateType.Succeed)
                    {
                        singleFlipFlopNode.FlowState = FlowStateType.Succeed;
                        singleFlipFlopNode.FlowData = flipflopContext.Data;
                        var tasks = singleFlipFlopNode.PreviousNodes[ConnectionType.IsSucceed].Select(nextNode =>
                        {
                            var context = new DynamicContext(ServiceContainer,flowEnvironment);
                            nextNode.PreviousNode = singleFlipFlopNode;
                            return nextNode.StartExecution(context);
                        }).ToArray();
                        Task.WaitAll(tasks);
                    }
                    else
                    {
                        break;
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
