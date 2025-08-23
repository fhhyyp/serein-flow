using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Serein.Library
{
    /// <summary>
    /// 轻量级流程控制器
    /// </summary>
    public class LightweightFlowControl : IFlowControl
    {
        private readonly IFlowCallTree flowCallTree;
        private readonly IFlowEnvironment flowEnvironment;

        /// <summary>
        /// 轻量级流程上下文池，使用对象池模式来管理流程上下文的创建和回收。
        /// </summary>
        public static Serein.Library.Utils.ObjectPool<IFlowContext> FlowContextPool { get; set; }

        /// <summary>
        /// 单例IOC容器，用于依赖注入和服务定位。
        /// </summary>
        public ISereinIOC IOC => throw new NotImplementedException();

        /// <summary>
        /// 轻量级流程控制器构造函数，接受流程调用树和流程环境作为参数。
        /// </summary>
        /// <param name="flowCallTree"></param>
        /// <param name="flowEnvironment"></param>
        public LightweightFlowControl(IFlowCallTree flowCallTree, IFlowEnvironment flowEnvironment)
        {
            this.flowCallTree = flowCallTree;
            this.flowEnvironment = flowEnvironment;
            ((LightweightFlowEnvironment)flowEnvironment).FlowControl = this;

            FlowContextPool = new Utils.ObjectPool<IFlowContext>(() =>
            {
                return new FlowContext(flowEnvironment);
            }, context =>
            {
                context.Reset();
            });
        }

       

        /// <inheritdoc/>
        public Task<object> InvokeAsync(string apiGuid, Dictionary<string, object> dict)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public Task<TResult> InvokeAsync<TResult>(string apiGuid, Dictionary<string, object> dict)
        {
            throw new NotImplementedException();
        }


        //private readonly DefaultObjectPool<IDynamicContext> _stackPool = new DefaultObjectPool<IDynamicContext>(new DynamicContext(this));

        /// <inheritdoc/>
        public async Task<TResult> StartFlowAsync<TResult>(string startNodeGuid)
        {
            IFlowContext context = Serein.Library.LightweightFlowControl.FlowContextPool.Allocate();
            CancellationTokenSource cts = new CancellationTokenSource();
            FlowResult flowResult;
#if DEBUG
            flowResult = await BenchmarkHelpers.BenchmarkAsync(async () =>
            {
                var node = flowCallTree.Get(startNodeGuid);
                var flowResult = await node.StartFlowAsync(context, cts.Token);
                return flowResult;
            });
#else
            var node = flowCallTree.Get(startNodeGuid);
            try
            {
                flowResult = await node.StartFlowAsync(context, cts.Token);
            }
            catch (global::System.Exception)
            {
                throw;
            }
            finally
            {
                context.Reset();
                FlowContextPool.Free(context);
            }
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

        /// <inheritdoc/>
        public async Task StartFlowAsync(string startNodeGuid)
        {
            IFlowContext context = Serein.Library.LightweightFlowControl.FlowContextPool.Allocate();
            CancellationTokenSource cts = new CancellationTokenSource();
            FlowResult flowResult;
#if DEBUG
            flowResult = await BenchmarkHelpers.BenchmarkAsync(async () =>
            {
                var node = flowCallTree.Get(startNodeGuid);
                var flowResult = await node.StartFlowAsync(context, cts.Token);
                return flowResult;
            });
#else
            var node = flowCallTree.Get(startNodeGuid);
            try
            {
                flowResult = await node.StartFlowAsync(context, cts.Token);
            }
            catch (global::System.Exception)
            {
                throw;
            }
            finally
            {
                context.Reset();
                FlowContextPool.Free(context);
            }
#endif

            cts?.Cancel();
            cts?.Dispose();
        }

        /// <inheritdoc/>
        public  Task<bool> StartFlowAsync(string[] canvasGuids)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<bool> ExitFlowAsync()
        {
            throw new NotImplementedException();
        }

        #region 无须实现
        /// <inheritdoc/>
        public void ActivateFlipflopNode(string nodeGuid)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public void MonitorObjectNotification(string nodeGuid, object monitorData, MonitorObjectEventArgs.ObjSourceType sourceType)
        {
            throw new NotImplementedException();
        }


        /// <inheritdoc/>
        public void TerminateFlipflopNode(string nodeGuid)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public void TriggerInterrupt(string nodeGuid, string expression, InterruptTriggerEventArgs.InterruptTriggerType type)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public void UseExternalIOC(ISereinIOC ioc)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public void UseExternalIOC(ISereinIOC ioc, Action<ISereinIOC> setDefultMemberOnReset = null)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
