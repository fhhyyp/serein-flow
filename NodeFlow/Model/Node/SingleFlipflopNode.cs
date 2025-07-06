using Serein.Library.Api;
using Serein.Library;
using Serein.Library.Utils;
using System;

namespace Serein.NodeFlow.Model
{
    /// <summary>
    /// 触发器节点
    /// </summary>
    public class SingleFlipflopNode : NodeModelBase
    {
        public SingleFlipflopNode(IFlowEnvironment environment) : base(environment)
        {

        }


        /// <summary>
        /// 执行触发器进行等待触发
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public override async Task<FlowResult> ExecutingAsync(IDynamicContext context, CancellationToken token)
        {
            #region 执行前中断
            if (DebugSetting.IsInterrupt) // 执行触发前
            {
                string guid = Guid.ToString();
                await DebugSetting.GetInterruptTask.Invoke();
                await Console.Out.WriteLineAsync($"[{MethodDetails.MethodName}]中断已取消，开始执行后继分支");
            }
            #endregion

            MethodDetails md = MethodDetails;
            if (!context.Env.TryGetDelegateDetails(md.AssemblyName, md.MethodName, out var dd)) // 流程运行到某个节点
            {
                throw new Exception("不存在对应委托");
            }

            var instance = context.Env.IOC.Get(md.ActingInstanceType);
            if (instance is null)
            {
                Env.IOC.Register(md.ActingInstanceType).Build();
                instance = Env.IOC.Get(md.ActingInstanceType);
            }
            await dd.InvokeAsync(instance, [context]);
            var args = await this.GetParametersAsync(context, token);
            // 因为这里会返回不确定的泛型 IFlipflopContext<TRsult>
            // 而我们只需要获取到 State 和 Value（返回的数据）
            // 所以使用 dynamic 类型接收
            if (token.IsCancellationRequested)
            {
                return null;
            }
            dynamic dynamicFlipflopContext = await dd.InvokeAsync(instance, args);
            FlipflopStateType flipflopStateType = dynamicFlipflopContext.State;
            context.NextOrientation = flipflopStateType.ToContentType();


            if (dynamicFlipflopContext.Type == TriggerDescription.Overtime)
            {
                throw new FlipflopException(MethodDetails.MethodName + "触发器超时触发。Guid" + Guid);
            }
            object result = dynamicFlipflopContext.Value;
            var flowReslt = new FlowResult(this.Guid, context, result);
            return flowReslt;
        }

    }
}
