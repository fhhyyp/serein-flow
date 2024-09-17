﻿using Serein.Library.Api;
using Serein.Library.Core.NodeFlow;
using Serein.Library.Entity;
using Serein.Library.Enums;
using Serein.Library.Utils;
using Serein.Library.Web;
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
        /// <param name="runNodeMd">环境中已加载的所有节点方法</param>
        /// <param name="flipflopNodes">触发器节点</param>
        /// <returns></returns>
        public async Task RunAsync(NodeModelBase startNode,
                                   IFlowEnvironment env,
                                   List<MethodDetails> runNodeMd,
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

            #region 选择运行环境的上下文

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
            #endregion

            #region 初始化运行环境的Ioc容器
            // 清除节点使用的对象
            var thisRuningMds = new List<MethodDetails>();
            thisRuningMds.AddRange(runNodeMd);
            thisRuningMds.AddRange(initMethods);
            thisRuningMds.AddRange(loadingMethods);
            thisRuningMds.AddRange(exitMethods);

            // .AddRange(initMethods).AddRange(loadingMethods).a
            foreach (var nodeMd in thisRuningMds)
            {
                nodeMd.ActingInstance = null;
            }

            SereinIOC.Reset(); // 开始运行时清空ioc中注册的实例
            // 初始化ioc容器中的类型对象
            foreach (var md in thisRuningMds)
            {
                if(md.ActingInstanceType != null)
                {
                    SereinIOC.Register(md.ActingInstanceType);
                }
            }
            SereinIOC.Build(); // 流程启动前的初始化
            foreach (var md in thisRuningMds)
            {
                if (md.ActingInstanceType != null)
                {
                    md.ActingInstance = SereinIOC.GetOrRegisterInstantiate(md.ActingInstanceType);
                }
            }

            //foreach (var md in flipflopNodes.Select(it => it.MethodDetails).ToArray())
            //{
            //    md.ActingInstance = SereinIoc.GetOrCreateServiceInstance(md.ActingInstanceType);
            //}
            #endregion

            #region 检查并修正初始化、加载时、退出时方法作用的对象，保证后续不会报错
            foreach (var md in initMethods) // 初始化
            {
                md.ActingInstance ??= Context.SereinIoc.GetOrRegisterInstantiate(md.ActingInstanceType);
            }
            foreach (var md in loadingMethods) // 加载
            {
                md.ActingInstance ??= Context.SereinIoc.GetOrRegisterInstantiate(md.ActingInstanceType);
            }
            foreach (var md in exitMethods) // 初始化
            {
                md.ActingInstance ??= Context.SereinIoc.GetOrRegisterInstantiate(md.ActingInstanceType);
            }
            #endregion

            #region 执行初始化，绑定IOC容器，再执行加载时，设置流程退出时的回调函数

            object?[]? args = [Context];
            foreach (var md in initMethods) // 初始化
            {
                object?[]? data = [md.ActingInstance, args];
                md.MethodDelegate.DynamicInvoke(data);
            }
            Context.SereinIoc.Build(); // 绑定初始化时注册的类型
            foreach (var md in loadingMethods) // 加载
            {
                object?[]? data = [md.ActingInstance, args];
                md.MethodDelegate.DynamicInvoke(data);
            }
            Context.SereinIoc.Build(); // 预防有人在加载时才注册类型，再绑定一次
            ExitAction = () =>
            {
                SereinIOC.Run<WebServer>(web => {
                    web?.Stop();
                });

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
            #endregion

            #region 开始启动流程

            try
            {

                if (flipflopNodes.Count > 0)
                {
                    FlipFlopState = RunState.Running;
                    // 如果存在需要启动的触发器，则开始启动
                    FlipFlopCts = SereinIOC.GetOrRegisterInstantiate<NodeRunCts>();
                    // 使用 TaskCompletionSource 创建未启动的触发器任务
                    var tasks = flipflopNodes.Select(async node =>
                    {
                        await FlipflopExecute(node, env);
                    }).ToArray();
                    _ = Task.WhenAll(tasks);
                }
                await startNode.StartExecution(Context);
                // 等待结束
                if (FlipFlopCts != null)
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
            #endregion
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

                    md.ActingInstance = context.SereinIoc.GetOrRegisterInstantiate(md.ActingInstanceType);

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
