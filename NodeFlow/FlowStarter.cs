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
        public FlowStarter()
        {
            SereinIOC = new SereinIOC();
        }
        /// <summary>
        /// 流程运行状态
        /// </summary>
        public enum RunState
        {
            /// <summary>
            /// 等待开始
            /// </summary>
            NoStart,
            /// <summary>
            /// 正在运行
            /// </summary>
            Running,
            /// <summary>
            /// 运行完成
            /// </summary>
            Completion,
        }
        /// <summary>
        /// 控制触发器的结束
        /// </summary>
        private NodeRunCts FlipFlopCts { get; set; } = null;

        /// <summary>
        /// 运行状态
        /// </summary>
        public RunState FlowState { get; private set; } = RunState.NoStart;
        public RunState FlipFlopState { get; private set; } = RunState.NoStart;

        /// <summary>
        /// 运行时的IOC容器
        /// </summary>
        private ISereinIOC SereinIOC { get; } = null; 

        /// <summary>
        /// 结束运行时需要执行的方法
        /// </summary>
        private Action ExitAction { get; set; }  = null; 
        /// <summary>
        /// 运行的上下文
        /// </summary>
        private IDynamicContext Context { get; set; }  = null; 



        /// <summary>
        /// 开始运行
        /// </summary>
        /// <param name="startNode">起始节点</param>
        /// <param name="env">运行环境</param>
        /// <param name="runMd">环境中已加载的所有节点方法</param>
        /// <param name="flipflopNodes">触发器节点</param>
        /// <returns></returns>
        public async Task RunAsync(NodeModelBase startNode,
                                   IFlowEnvironment env,
                                   List<MethodDetails> runMd,
                                   List<MethodDetails> initMethods,
                                   List<MethodDetails> loadingMethods,
                                   List<MethodDetails> exitMethods,
                                   List<SingleFlipflopNode> flipflopNodes)
        {
            
            FlowState = RunState.Running; // 开始运行

            if (startNode == null) {
                FlowState = RunState.Completion; // 不存在起点，退出流程
                return; 
            }

            // 判断使用哪一种流程上下文
            var isNetFramework = true;
            if (isNetFramework)
            {
                Context = new Serein.Library.Framework.NodeFlow.DynamicContext(SereinIOC, env);
            }
            else
            {
                Context = new Serein.Library.Core.NodeFlow.DynamicContext(SereinIOC, env);
            }

            #region 初始化运行环境的Ioc容器
            // 清除节点使用的对象
            foreach (var nodeMd in runMd)
            {
                nodeMd.ActingInstance = null;
            }
            SereinIOC.Reset(); // 开始运行时清空ioc中注册的实例
            // 初始化ioc容器中的类型对象
            foreach (var md in runMd)
            {
                SereinIOC.Register(md.ActingInstanceType);
            }
            SereinIOC.Build();
            foreach (var md in runMd)
            {
                md.ActingInstance = SereinIOC.GetOrInstantiate(md.ActingInstanceType);
            }

            //foreach (var md in flipflopNodes.Select(it => it.MethodDetails).ToArray())
            //{
            //    md.ActingInstance = SereinIoc.GetOrCreateServiceInstance(md.ActingInstanceType);
            //}
            #endregion


            #region 创建Node中初始化、加载时、退出时调用的方法

            foreach (var md in initMethods) // 初始化
            {
                md.ActingInstance ??= Context.SereinIoc.GetOrInstantiate(md.ActingInstanceType);
            }
            foreach (var md in loadingMethods) // 加载
            {
                md.ActingInstance ??= Context.SereinIoc.GetOrInstantiate(md.ActingInstanceType);
            }
            foreach (var md in exitMethods) // 初始化
            {
                md.ActingInstance ??= Context.SereinIoc.GetOrInstantiate(md.ActingInstanceType);
            }

            object?[]? args = [Context];
            ExitAction = () =>
            {
                foreach (MethodDetails? md in exitMethods)
                {
                    object?[]? data = [md.ActingInstance, args];
                    md.MethodDelegate.DynamicInvoke(data);
                }
                if (Context != null && Context.NodeRunCts != null && !Context.NodeRunCts.IsCancellationRequested)
                {
                    Context.NodeRunCts.Cancel();
                }
                if (FlipFlopCts != null && !FlipFlopCts.IsCancellationRequested)
                {
                    FlipFlopCts.Cancel();
                }
                FlowState = RunState.Completion;
                FlipFlopState = RunState.Completion;
            };
            Context.SereinIoc.Build();
            #endregion

            #region 执行初始化，然后绑定IOC容器，再执行加载时
            
            foreach (var md in initMethods) // 初始化 - 调用方法
            {
                object?[]? data = [md.ActingInstance, args];
                md.MethodDelegate.DynamicInvoke(data);
            }
            Context.SereinIoc.Build();
            foreach (var md in loadingMethods) // 加载
            {
                object?[]? data = [md.ActingInstance, args];
                md.MethodDelegate.DynamicInvoke(data);
            } 
            #endregion

            
            // 节点任务的启动
            try
            {
                
                if (flipflopNodes.Count > 0)
                {
                    FlipFlopState = RunState.Running;
                    // 如果存在需要启动的触发器，则开始启动
                    FlipFlopCts = SereinIOC.GetOrInstantiate<NodeRunCts>();
                    // 使用 TaskCompletionSource 创建未启动的触发器任务
                    var tasks = flipflopNodes.Select(async node =>
                    {
                        await FlipflopExecute(node, env);
                    }).ToArray();
                    _ = Task.WhenAll(tasks);
                }
                await startNode.StartExecution(Context);
                // 等待结束
                if(FlipFlopCts != null)
                {
                    while (!FlipFlopCts.IsCancellationRequested)
                    {
                        await Task.Delay(100);
                    }
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
            DynamicContext context = new DynamicContext(SereinIOC, flowEnvironment);
            MethodDetails md = singleFlipFlopNode.MethodDetails;
            var del = md.MethodDelegate;
            try
            {
                //var func = md.ExplicitDatas.Length == 0 ? (Func<object, object, Task<FlipflopContext<dynamic>>>)del : (Func<object, object[], Task<FlipflopContext<dynamic>>>)del;
                var func = md.ExplicitDatas.Length == 0 ? (Func<object, object, Task<IFlipflopContext>>)del : (Func<object, object[], Task<IFlipflopContext>>)del;

                while (!FlipFlopCts.IsCancellationRequested) // 循环中直到栈为空才会退出
                {
                    object?[]? parameters = singleFlipFlopNode.GetParameters(context, md);
                    // 调用委托并获取结果

                    md.ActingInstance = context.SereinIoc.GetOrInstantiate(md.ActingInstanceType);

                    IFlipflopContext flipflopContext = await func.Invoke(md.ActingInstance, parameters);

                    ConnectionType connection = flipflopContext.State.ToContentType(); 

                    if (connection != ConnectionType.None)
                    {
                        singleFlipFlopNode.NextOrientation = connection;
                        singleFlipFlopNode.FlowData = flipflopContext.Data;

                        var tasks = singleFlipFlopNode.SuccessorNodes[connection].Select(nextNode =>
                        {
                            var context = new DynamicContext(SereinIOC,flowEnvironment);
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
