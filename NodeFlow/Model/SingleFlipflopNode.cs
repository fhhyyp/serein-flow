using Serein.Library.Api;
using Serein.Library;
using Serein.Library.Utils;
using Serein.NodeFlow.Env;
using static Serein.Library.Utils.ChannelFlowInterrupt;

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
        public override async Task<object?> ExecutingAsync(IDynamicContext context)
        {
            #region 执行前中断
            if (DebugSetting.IsInterrupt) // 执行触发前
            {
                string guid = this.Guid.ToString();
                var cancelType = await this.DebugSetting.GetInterruptTask();
                await Console.Out.WriteLineAsync($"[{this.MethodDetails.MethodName}]中断已{cancelType}，开始执行后继分支");
            }
            #endregion

            MethodDetails md = MethodDetails;
            if (!context.Env.TryGetDelegateDetails(md.AssemblyName, md.MethodName, out var dd)) // 流程运行到某个节点
            {
                throw new Exception("不存在对应委托");
            }
            object instance = md.ActingInstance;

            var args = await GetParametersAsync(context, this);
            // 因为这里会返回不确定的泛型 IFlipflopContext<TRsult>
            // 而我们只需要获取到 State 和 Value（返回的数据）
            // 所以使用 dynamic 类型接收
            dynamic dynamicFlipflopContext = await dd.InvokeAsync(md.ActingInstance, args);
            FlipflopStateType flipflopStateType = dynamicFlipflopContext.State;
            context.NextOrientation = flipflopStateType.ToContentType();
            if (dynamicFlipflopContext.Type == TriggerType.Overtime)
            {
                throw new FlipflopException(base.MethodDetails.MethodName + "触发器超时触发。Guid" + base.Guid);
            }
            return dynamicFlipflopContext.Value;
        }

    }
}
