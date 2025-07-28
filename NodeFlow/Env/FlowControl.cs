using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Env
{

    internal class FlowControl : IFlowControl
    {
        private readonly IFlowEnvironment flowEnvironment;
        private readonly IFlowEnvironmentEvent flowEnvironmentEvent;
        private readonly FlowLibraryService flowLibraryService;
        private readonly FlowOperationService flowOperationService;
        private readonly FlowModelService flowModelService;
        private readonly UIContextOperation UIContextOperation;

        public FlowControl(IFlowEnvironment flowEnvironment,
                           IFlowEnvironmentEvent flowEnvironmentEvent,
                           FlowLibraryService flowLibraryService,
                           FlowOperationService flowOperationService,
                           FlowModelService flowModelService,
                           UIContextOperation UIContextOperation)
        {
            this.flowEnvironment = flowEnvironment;
            this.flowEnvironmentEvent = flowEnvironmentEvent;
            this.flowLibraryService = flowLibraryService;
            this.flowOperationService = flowOperationService;
            this.flowModelService = flowModelService;
            this.UIContextOperation = UIContextOperation;

            contexts = new ObjectPool<IFlowContext>(() => new FlowContext(flowEnvironment));
        }

        private ObjectPool<IFlowContext> contexts;
        private FlowWorkManagement flowWorkManagement;
        private ISereinIOC externalIOC;
        private Action<ISereinIOC> setDefultMemberOnReset;
        private bool IsUseExternalIOC = false;
        private object lockObj = new object();
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


            #region 初始化每个画布的数据，转换为流程任务
            Dictionary<string, FlowTask> flowTasks = [];
            foreach (var guid in guids)
            {
                if (!flowModelService.TryGetCanvasModel(guid, out var canvasModel))
                {
                    SereinEnv.WriteLine(InfoType.WARN, $"画布不存在，停止运行。{guid}");
                    return false;
                }
                var ft = new FlowTask();
                ft.GetNodes = () => flowModelService.GetAllNodeModel(guid);
                if (canvasModel.StartNode?.Guid is null)
                {
                    SereinEnv.WriteLine(InfoType.WARN, $"画布不存在起始节点，将停止运行。{guid}");
                    return false;
                }
                ft.GetStartNode = () => canvasModel.StartNode;
                flowTasks.Add(guid, ft);
            }
            #endregion
            IOC.Reset();
            setDefultMemberOnReset?.Invoke(IOC);
            IOC.Register<IFlowEnvironment>(() => flowEnvironment);
            //externalIOC.Register<IScriptFlowApi, ScriptFlowApi>(); // 注册脚本接口

            var flowTaskOptions = new FlowWorkOptions
            {
                FlowIOC = IOC,
                Environment = flowEnvironment, // 流程
                Flows = flowTasks,
                FlowContextPool = contexts, // 上下文对象池
                AutoRegisterTypes = flowLibraryService.GetaAutoRegisterType(), // 需要自动实例化的类型
                InitMds = flowLibraryService.GetMdsOnFlowStart(NodeType.Init),
                LoadMds = flowLibraryService.GetMdsOnFlowStart(NodeType.Loading),
                ExitMds = flowLibraryService.GetMdsOnFlowStart(NodeType.Exit),
            };


            flowWorkManagement = new FlowWorkManagement(flowTaskOptions);
            var cts = new CancellationTokenSource();
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
            flowTaskOptions = null;
            return true;
        }

        /// <inheritdoc/>
        public async Task<TResult> StartFlowAsync<TResult>(string startNodeGuid)
        {
            var flowTaskOptions = new FlowWorkOptions
            {
                FlowIOC = IOC,
                Environment = flowEnvironment, // 流程
                FlowContextPool = contexts, // 上下文对象池
            };
            var flowTaskManagement = new FlowWorkManagement(flowTaskOptions);

            if (!flowModelService.TryGetNodeModel(startNodeGuid, out var nodeModel) || nodeModel is SingleFlipflopNode)
            {
                throw new Exception();
            }

#if DEBUG

            FlowResult flowResult = await BenchmarkHelpers.BenchmarkAsync(async () =>
            await flowTaskManagement.StartFlowInSelectNodeAsync(nodeModel));
#else
            //FlowResult flowResult =  await flowTaskManagement.StartFlowInSelectNodeAsync(nodeModel);
            FlowResult flowResult = await BenchmarkHelpers.BenchmarkAsync(async () => await flowTaskManagement.StartFlowInSelectNodeAsync(nodeModel));

#endif


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

        /*/// <summary>
        /// 单独运行一个节点
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <returns></returns>
        public async Task<object> InvokeNodeAsync(IDynamicContext context, string nodeGuid)
        {
            object result = Unit.Default;
            if (this.NodeModels.TryGetValue(nodeGuid, out var model))
            {
                CancellationTokenSource cts = new CancellationTokenSource();    
                result =  await model.ExecutingAsync(context, cts.Token);
                cts?.Cancel();
            }
            return result;
        }*/
        /// <inheritdoc/>
        public Task<bool> ExitFlowAsync()
        {
            flowWorkManagement?.Exit();
            UIContextOperation?.Invoke(() => flowEnvironmentEvent.OnFlowRunComplete(new FlowEventArgs()));
            IOC.Reset();
            flowWorkManagement = null;
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
        public void UseExternalIOC(ISereinIOC ioc, Action<ISereinIOC> setDefultMemberOnReset = null)
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
