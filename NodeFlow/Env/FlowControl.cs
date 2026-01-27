using Microsoft.CodeAnalysis.CSharp.Syntax;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Model.Nodes;
using Serein.NodeFlow.Services;
using Serein.NodeFlow.Tool;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Env
{

    internal class FlowControl : IFlowControl
    {
        private readonly IFlowEnvironment flowEnvironment;
        private readonly IFlowEnvironmentEvent flowEnvironmentEvent;
        private readonly IFlowLibraryService flowLibraryService;
        private readonly FlowOperationService flowOperationService;
        private readonly FlowModelService flowModelService;
        private readonly Lazy<UIContextOperation> uiContextOperation;

        public FlowControl(IFlowEnvironment flowEnvironment,
                           IFlowEnvironmentEvent flowEnvironmentEvent,
                           IFlowLibraryService flowLibraryService,
                           FlowOperationService flowOperationService,
                           FlowModelService flowModelService)
        {
            this.flowEnvironment = flowEnvironment;
            this.flowEnvironmentEvent = flowEnvironmentEvent;
            this.flowLibraryService = flowLibraryService;
            this.flowOperationService = flowOperationService;
            this.flowModelService = flowModelService;
            uiContextOperation = new Lazy<UIContextOperation>(() => flowEnvironment.IOC.Get<UIContextOperation>());
            contexts = new ObjectPool<IFlowContext>(() => new FlowContext(flowEnvironment), context => context.Reset());
            flowTaskOptions = new FlowWorkOptions
            {
                FlowIOC = IOC,
                Environment = flowEnvironment, // 流程
                FlowContextPool = contexts, // 上下文对象池
            };
            flowTaskManagementPool = new ObjectPool<FlowWorkManagement>(()=> new FlowWorkManagement(flowTaskOptions), fwm => fwm.Exit());
        }

        private ObjectPool<IFlowContext> contexts;
        private ObjectPool<FlowWorkManagement> flowTaskManagementPool;
        private FlowWorkOptions flowTaskOptions;

       

        private ISereinIOC? externalIOC;
        private Action<ISereinIOC>? setDefultMemberOnReset;
        private bool IsUseExternalIOC = false;
        private readonly object lockObj = new object();

        /// <summary>
        /// 如果全局触发器还在运行，则为 Running 。
        /// </summary>
        private RunState FlipFlopState = RunState.NoStart;

        /// <summary>
        /// 运行时的IOC容器
        /// </summary>
        public ISereinIOC IOC
        {
            get
            {
                lock (lockObj)
                {
                    if (externalIOC is null) externalIOC = new SereinIOC();
                    return externalIOC;
                }

            }
            private set
            {
                lock (lockObj) 
                {
                    externalIOC = value;
                }
            }
        }

        private readonly List<FlowWorkManagement> flowWorkManagements = [];
        private FlowWorkManagement GetFWM()
        {
            var fwm = flowTaskManagementPool.Allocate();
            flowWorkManagements.Add(fwm);
            return fwm;
        }
        private void ReturnFWM(FlowWorkManagement fwm)
        {
            if (flowWorkManagements.Contains(fwm))
            {
                flowWorkManagements.Remove(fwm);
            }
            flowTaskManagementPool.Free(fwm);
        }


        /// <inheritdoc/>
        public async Task<bool> StartFlowAsync(string[] canvasGuids)
        {
            
            #region 校验参数
            HashSet<string> guids = new HashSet<string>();
            bool isBreak = false;
            foreach (var canvasGuid in canvasGuids)
            {
                if (guids.Contains(canvasGuid))
                {
                    flowEnvironment.WriteLine(InfoType.WARN, $"画布重复，停止运行。{canvasGuid}");
                    isBreak = true;
                }
                else if (!flowModelService.ContainsCanvasModel(canvasGuid))
                {
                    SereinEnv.WriteLine(InfoType.WARN, $"画布不存在，停止运行。{canvasGuid}");
                    isBreak = true;
                }
                else if (!flowModelService.IsExsitNodeOnCanvas(canvasGuid))
                {
                    SereinEnv.WriteLine(InfoType.WARN, $"画布没有节点，停止运行。{canvasGuid}");
                    isBreak = true;
                }
                else
                {
                    guids.Add(canvasGuid);
                }
            }
            if (isBreak)
            {
                guids.Clear();
                return false;
            }
            #endregion

           
            // 初始化每个画布的数据，转换为流程任务
            var flowTasks = guids.Select(guid =>
                                  {
                                      if (!flowModelService.TryGetCanvasModel(guid, out var canvasModel))
                                      {
                                          SereinEnv.WriteLine(InfoType.WARN, $"画布不存在，将不会运行。{guid}");
                                          return default;
                                      }
                                      if (canvasModel.StartNode is null)
                                      {
                                          SereinEnv.WriteLine(InfoType.WARN, $"画布不存在起始节点，将不会运行。{guid}");
                                          return default;
                                      }
                                      return canvasModel;
                                  })
                                 .Where(canvasModel => canvasModel != default && canvasModel.StartNode != null)
                                 .OfType<FlowCanvasDetails>()
                                 .ToDictionary(key => key.Guid,
                                               value => new FlowTask
                                                {
                                                    GetStartNode = () => value.StartNode!,
                                                    GetNodes = () => flowModelService.GetAllNodeModel(value.Guid),
                                                    IsWaitStartFlow = false
                                                });


            if(flowTasks.Values.Count == 0)
            {
                return false; 
            }

            // 初始化IOC
            setDefultMemberOnReset?.Invoke(IOC);
            IOC.Reset();
            IOC.Register<IFlowEnvironment>(() => flowEnvironment);
            var flowWorkManagement = GetFWM();
            flowWorkManagement.WorkOptions.Flows = flowTasks;
            //flowWorkManagement.WorkOptions.AutoRegisterTypes = flowLibraryService.GetaAutoRegisterType(); // 需要自动实例化的类型
            flowWorkManagement.WorkOptions.InitMds = flowLibraryService.GetMdsOnFlowStart(NodeType.Init);
            flowWorkManagement.WorkOptions.LoadMds = flowLibraryService.GetMdsOnFlowStart(NodeType.Loading);
            flowWorkManagement.WorkOptions.ExitMds = flowLibraryService.GetMdsOnFlowStart(NodeType.Exit);
            using var cts = new CancellationTokenSource();
            try
            {
                var t = await flowWorkManagement.RunAsync(cts.Token);
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(ex);
            }
            finally
            {
                SereinEnv.WriteLine(InfoType.INFO, $"流程运行完毕{Environment.NewLine}"); ;
            }
            ReturnFWM(flowWorkManagement);
            return true;
        }

        /// <inheritdoc/>
        public async Task<TResult> StartFlowAsync<TResult>(string startNodeGuid)
        {
            var sw = Stopwatch.StartNew();
            var checkpoints = new Dictionary<string, TimeSpan>();
            var flowWorkManagement = GetFWM();
            if (!flowModelService.TryGetNodeModel(startNodeGuid, out IFlowNode? nodeModel))
            {
                throw new Exception($"节点不存在【{startNodeGuid}】");
            }
            if(nodeModel is SingleFlipflopNode)
            {
                throw new Exception("不能从[Flipflop]节点开始");
            }


            var flowContextPool = flowWorkManagement.WorkOptions.FlowContextPool;
            var context = flowContextPool.Allocate();
            checkpoints["准备调用环境"] = sw.Elapsed;
            var flowResult =  await nodeModel.StartFlowAsync(context, flowWorkManagement.WorkOptions.CancellationTokenSource.Token); // 开始运行时从选定节点开始运行
            checkpoints["调用节点流程"] = sw.Elapsed;

            var last = TimeSpan.Zero;
            foreach (var kv in checkpoints)
            {
                SereinEnv.WriteLine(InfoType.INFO, $"{kv.Key} 耗时: {(kv.Value - last).TotalMilliseconds} ms");
                last = kv.Value;
            }
            //await BenchmarkHelpers.BenchmarkAsync(flowTaskManagement.StartFlowInSelectNodeAsync(nodeModel));
            if (context.IsRecordInvokeInfo)
            {
                var invokeInfos = context.GetAllInvokeInfos();
                _ = Task.Delay(100).ContinueWith(async (task) =>
                {
                    await task;
                    if (invokeInfos.Count < 255)
                    {
                        foreach (var info in invokeInfos)
                        {
                            SereinEnv.WriteLine(InfoType.INFO, info.ToString());
                        }
                    }
                    else
                    {
                        double total = 0;
                        for (int i = 0; i < invokeInfos.Count; i++)
                        {
                            total += invokeInfos[i].TS.TotalSeconds;
                        }
                        SereinEnv.WriteLine(InfoType.INFO, $"运行次数：{invokeInfos.Count}");
                        SereinEnv.WriteLine(InfoType.INFO, $"平均耗时：{total / invokeInfos.Count}");
                        SereinEnv.WriteLine(InfoType.INFO, $"总耗时：{total}");
                    }
                });
            }
            flowContextPool.Free(context);
            ReturnFWM(flowWorkManagement); // 释放流程任务管理器
            if (flowResult.Value is TResult result)
            {
                return result;
            }
            else if (flowResult is FlowResult && flowResult is TResult result2)
            {
                return result2;
            }
            else
            {
                throw new ArgumentNullException($"类型转换失败，流程返回数据与泛型不匹配，当前返回类型为[{flowResult.Value.GetType().FullName}]。");
            }
        }

        
        /// <inheritdoc/>
        public async Task StartFlowAsync(string startNodeGuid)
        {
            var sw = Stopwatch.StartNew();
            var checkpoints = new Dictionary<string, TimeSpan>();
            var flowWorkManagement = GetFWM();
            if (!flowModelService.TryGetNodeModel(startNodeGuid, out IFlowNode? nodeModel))
            {
                throw new Exception($"节点不存在【{startNodeGuid}】");
            }
            if(nodeModel is SingleFlipflopNode)
            {
                throw new Exception("不能从[Flipflop]节点开始");
            }


            var flowContextPool = flowWorkManagement.WorkOptions.FlowContextPool;
            var context = flowContextPool.Allocate();
            checkpoints["准备调用环境"] = sw.Elapsed;
            var flowResult =  await nodeModel.StartFlowAsync(context, flowWorkManagement.WorkOptions.CancellationTokenSource.Token); // 开始运行时从选定节点开始运行
            checkpoints["调用节点流程"] = sw.Elapsed;

            var last = TimeSpan.Zero;
            foreach (var kv in checkpoints)
            {
                SereinEnv.WriteLine(InfoType.INFO, $"{kv.Key} 耗时: {(kv.Value - last).TotalMilliseconds} ms");
                last = kv.Value;
            }
            //await BenchmarkHelpers.BenchmarkAsync(flowTaskManagement.StartFlowInSelectNodeAsync(nodeModel));
            if (context.IsRecordInvokeInfo)
            {
                var invokeInfos = context.GetAllInvokeInfos();
                _ = Task.Delay(100).ContinueWith(async (task) =>
                {
                    await task;
                    if (invokeInfos.Count < 255)
                    {
                        foreach (var info in invokeInfos)
                        {
                            SereinEnv.WriteLine(InfoType.INFO, info.ToString());
                        }
                    }
                    else
                    {
                        double total = 0;
                        for (int i = 0; i < invokeInfos.Count; i++)
                        {
                            total += invokeInfos[i].TS.TotalSeconds;
                        }
                        SereinEnv.WriteLine(InfoType.INFO, $"运行次数：{invokeInfos.Count}");
                        SereinEnv.WriteLine(InfoType.INFO, $"平均耗时：{total / invokeInfos.Count}");
                        SereinEnv.WriteLine(InfoType.INFO, $"总耗时：{total}");
                    }
                });
            }
            flowContextPool.Free(context);
            ReturnFWM(flowWorkManagement); // 释放流程任务管理器
        }


        /// <inheritdoc/>
        public Task<bool> ExitFlowAsync()
        {
            foreach(var flowWorkManagement in flowWorkManagements)
            {
                flowWorkManagement.Exit();
            }
            uiContextOperation.Value.Invoke(() => flowEnvironmentEvent.OnFlowRunComplete(new FlowEventArgs()));
            IOC.Reset();
            GC.Collect();
            return Task.FromResult(true);
        }



        /// <inheritdoc/>
        public void ActivateFlipflopNode(string nodeGuid)
        {
            /*if (!TryGetNodeModel(nodeGuid, out var nodeModel))
            {
                return;
            }
            if (nodeModel is null) return;
            if (flowTaskManagement is not null && nodeModel is SingleFlipflopNode flipflopNode) // 子节点为触发器
            {
                if (FlowState != RunState.Completion
                    && flipflopNode.NotExitPreviousNode()) // 正在运行，且该触发器没有上游节点
                {
                    _ = flowTaskManagement.RunGlobalFlipflopAsync(this, flipflopNode);// 被父节点移除连接关系的子节点若为触发器，且无上级节点，则当前流程正在运行，则加载到运行环境中

                }
            }*/
        }
        /// <inheritdoc/>
        public void TerminateFlipflopNode(string nodeGuid)
        {
            /* if (!TryGetNodeModel(nodeGuid, out var nodeModel))
             {
                 return;
             }
             if (nodeModel is null) return;
             if (flowTaskManagement is not null && nodeModel is SingleFlipflopNode flipflopNode) // 子节点为触发器
             {
                 flowTaskManagement.TerminateGlobalFlipflopRuning(flipflopNode);
             }*/
        }

        /// <inheritdoc/>
        public void UseExternalIOC(ISereinIOC ioc, Action<ISereinIOC>? setDefultMemberOnReset = null)
        {
            IOC = ioc; // 设置IOC容器
            this.setDefultMemberOnReset = setDefultMemberOnReset;
            IsUseExternalIOC = true;
        }

        /// <inheritdoc/>
        public void MonitorObjectNotification(string nodeGuid, object monitorData, MonitorObjectEventArgs.ObjSourceType sourceType)
        {
            flowEnvironmentEvent.OnMonitorObjectChanged(new MonitorObjectEventArgs(nodeGuid, monitorData, sourceType));
        }

        /// <inheritdoc/>
        public void TriggerInterrupt(string nodeGuid, string expression, InterruptTriggerEventArgs.InterruptTriggerType type)
        {
            flowEnvironmentEvent.OnInterruptTriggered(new InterruptTriggerEventArgs(nodeGuid, expression, type));
        }




        #region 流程接口调用

        /// <inheritdoc/>
        public async Task<object> InvokeAsync(string apiGuid, Dictionary<string, object> dict)
        {
            var result = await InvokeAsync<object>(apiGuid, dict);
            return result;
        }

        /// <inheritdoc/>
        public async Task<TResult> InvokeAsync<TResult>(string apiGuid, Dictionary<string, object>  dict)
        {
            if (!flowModelService.TryGetNodeModel(apiGuid, out var nodeModel))
            {
                throw new ArgumentNullException($"不存在流程接口：{apiGuid}");
            }
            if (nodeModel is not SingleFlowCallNode flowCallNode)
            {
                throw new ArgumentNullException($"目标节点并非流程接口：{apiGuid}");
            }
            var pds = flowCallNode.MethodDetails.ParameterDetailss;
            if (dict.Keys.Count != pds.Length)
            {
                throw new ArgumentNullException($"参数数量不一致。传入参数数量：{dict.Keys.Count}。接口入参数量：{pds.Length}。");
            }

            IFlowContext context = contexts.Allocate();
            for (int index = 0; index < pds.Length; index++)
            {
                ParameterDetails pd = pds[index];
                if (dict.TryGetValue(pd.Name, out var value))
                {
                    context.SetParamsTempData(flowCallNode.Guid, index, value); // 设置入参参数
                }
            }
            CancellationTokenSource cts = new CancellationTokenSource();

#if DEBUG

            FlowResult flowResult = await BenchmarkHelpers.BenchmarkAsync(async () =>
            {
                var flowResult = await flowCallNode.StartFlowAsync(context, cts.Token);
                return flowResult;
            });
#else
            var flowResult = await flowCallNode.StartFlowAsync(context, cts.Token);
#endif
            cts?.Cancel();
            cts?.Dispose();
            if (flowResult.Value is TResult result)
            {
                return result;
            }
            else if (flowResult is FlowResult && flowResult is TResult result2)
            {
                return result2;
            }
            else
            {
                throw new ArgumentNullException($"类型转换失败，流程返回数据与泛型不匹配，当前返回类型为[{flowResult.Value.GetType().FullName}]。");
            }
        }

        private bool IsHasSuccessorNodes(IFlowNode nodeModel)
        {
            var nextTypes = new[]
            {
                ConnectionInvokeType.Upstream,
                ConnectionInvokeType.IsSucceed,
                ConnectionInvokeType.IsFail,
                ConnectionInvokeType.IsError
            };
            foreach (var type in nextTypes)
            {
                if (nodeModel.SuccessorNodes.TryGetValue(type, out var nextNodes))
                {
                    if(nextNodes.Count > 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        #endregion
    }
}
