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
        /// 加载完成后调用的方法
        /// </summary>
        public override void OnLoading()
        {
            Console.WriteLine("SingleFlipflopNode 暂未实现 OnLoading");
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
            if (!context.Env.TryGetDelegateDetails(md.MethodName, out var dd))
            {
                throw new Exception("不存在对应委托");
            }
            object instance = md.ActingInstance;
            try
            {
                var args = await GetParametersAsync(context, this, md);
                var result = await dd.InvokeAsync(md.ActingInstance, args);
                dynamic flipflopContext = result;
                FlipflopStateType flipflopStateType = flipflopContext.State;
                context.NextOrientation = flipflopStateType.ToContentType();
                if (flipflopContext.Type == TriggerType.Overtime)
                {
                    throw new FlipflopException(base.MethodDetails.MethodName + "触发器超时触发。Guid" + base.Guid);
                }
                return flipflopContext.Value;
            
            }
            catch (FlipflopException ex)
            {
                if(ex.Type == FlipflopException.CancelClass.CancelFlow)
                {
                    throw;
                }
                await Console.Out.WriteLineAsync($"触发器[{this.MethodDetails.MethodName}]异常：" + ex);
                context.NextOrientation = ConnectionInvokeType.None;
                RuningException = ex;
                return null;
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync($"触发器[{this.MethodDetails.MethodName}]异常：" + ex);
                context.NextOrientation = ConnectionInvokeType.IsError;
                RuningException = ex;
                return null;
            }
            finally
            {
                // flipflopTask?.Dispose();
            }
        }

        /// <summary>
        /// 获取触发器参数
        /// </summary>
        /// <returns></returns>
        public override ParameterData[] GetParameterdatas()
        {
            if (base.MethodDetails.ParameterDetailss.Length > 0)
            {
                return MethodDetails.ParameterDetailss
                                     .Select(it => new ParameterData
                                     {
                                         SourceNodeGuid = it.ArgDataSourceNodeGuid,
                                         SourceType = it.ArgDataSourceType.ToString(),
                                         State = it.IsExplicitData,
                                         Value = it.DataValue
                                     })
                                     .ToArray();
            }
            else
            {
                return [];
            }
        }
    }
}
