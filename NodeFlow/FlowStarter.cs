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
    public class FlowStarter
    {
        public FlowStarter(ISereinIoc serviceContainer/*, List<MethodDetails> methodDetails*/)
        {
            SereinIoc = serviceContainer;
            
        }

        private ISereinIoc SereinIoc { get; }
        // private List<MethodDetails> MethodDetailss { get; }
        private Action ExitAction { get; set; }  = null; //退出方法
        private IDynamicContext Context { get; set; }  = null;  //上下文
        public NodeRunCts MainCts { get; set; }

        /// <summary>
        /// 开始运行
        /// </summary>
        /// <param name="nodes"></param>
        /// <returns></returns>
        // public async Task RunAsync(List<NodeModelBase> nodes, IFlowEnvironment flowEnvironment)
        public async Task RunAsync(NodeModelBase startNode, IFlowEnvironment flowEnvironment, List<MethodDetails> methodDetailss, List<SingleFlipflopNode> flipflopNodes)
        {
            // var startNode = nodes.FirstOrDefault(p => p.IsStart);
            if (startNode == null) { return; }

            var isNetFramework = true;

            if (isNetFramework)
            {
                Context = new Serein.Library.Framework.NodeFlow.DynamicContext(SereinIoc, flowEnvironment);
            }
            else
            {
                Context = new Serein.Library.Core.NodeFlow.DynamicContext(SereinIoc, flowEnvironment);
            }

            MainCts = SereinIoc.CreateServiceInstance<NodeRunCts>();
  
            foreach (var md in methodDetailss)
            {
                SereinIoc.Register(md.ActingInstanceType);
            }
            SereinIoc.Build();
            foreach (var md in flipflopNodes.Select(it => it.MethodDetails).ToArray())
            {
                md.ActingInstance = SereinIoc.GetOrCreateServiceInstance(md.ActingInstanceType);
            }
            foreach (var md in methodDetailss)
            {
                md.ActingInstance = SereinIoc.GetOrCreateServiceInstance(md.ActingInstanceType);
            }

            var initMethods = methodDetailss.Where(it => it.MethodDynamicType == NodeType.Init).ToList();
            var loadingMethods = methodDetailss.Where(it => it.MethodDynamicType == NodeType.Loading).ToList();
            var exitMethods = methodDetailss.Where(it => it.MethodDynamicType == NodeType.Exit).ToList();
            ExitAction = () =>
            {
                //ServiceContainer.Run<WebServer>((web) =>
                //{
                //    web?.Stop();
                //});
                foreach (MethodDetails? md in exitMethods)
                {
                    md.ActingInstance = Context.SereinIoc.GetOrInstantiate(md.ActingInstanceType);
                    object?[]? args = [Context];
                    object?[]? data = [md.ActingInstance, args];
                    md.MethodDelegate.DynamicInvoke(data);
                }
                if (Context != null && Context.NodeRunCts != null && !Context.NodeRunCts.IsCancellationRequested)
                {
                    Context.NodeRunCts.Cancel();
                }
                if (MainCts != null && !MainCts.IsCancellationRequested) MainCts.Cancel();
                SereinIoc.Reset();
            };
            Context.SereinIoc.Build();
            foreach (var md in initMethods) // 初始化 - 调用方法
            {
                md.ActingInstance ??= Context.SereinIoc.GetOrInstantiate(md.ActingInstanceType);
                object?[]? args = [Context];
                object?[]? data = [md.ActingInstance, args];
                md.MethodDelegate.DynamicInvoke(data);
            }
            Context.SereinIoc.Build();
            foreach (var md in loadingMethods) // 加载
            {
                md.ActingInstance ??= Context.SereinIoc.GetOrInstantiate(md.ActingInstanceType);
                object?[]? args = [Context];
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
            _ = Task.WhenAll(tasks);
            try
            {
                await Task.Run(async () =>
                {
                    await startNode.StartExecution(Context);
                    //await Task.WhenAll([startNode.StartExecution(Context), .. tasks]);
                });
                // 等待结束
                while (!MainCts.IsCancellationRequested)
                {
                    await Task.Delay(100);
                }
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
            DynamicContext context = new DynamicContext(SereinIoc, flowEnvironment);
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

                    md.ActingInstance = context.SereinIoc.GetOrInstantiate(md.ActingInstanceType);

                    IFlipflopContext flipflopContext = await func.Invoke(md.ActingInstance, parameters);

                    if (flipflopContext.State == FlowStateType.Succeed)
                    {
                        singleFlipFlopNode.FlowState = FlowStateType.Succeed;
                        singleFlipFlopNode.FlowData = flipflopContext.Data;
                        var tasks = singleFlipFlopNode.SuccessorNodes[ConnectionType.IsSucceed].Select(nextNode =>
                        {
                            var context = new DynamicContext(SereinIoc,flowEnvironment);
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
