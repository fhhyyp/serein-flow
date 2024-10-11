using Serein.Library.Api;
using Serein.Library.Entity;
using Serein.Library.Enums;
using Serein.Library.Ex;
using Serein.Library.NodeFlow.Tool;
using Serein.Library.Utils;
using Serein.NodeFlow.Base;
using static Serein.Library.Utils.ChannelFlowInterrupt;

namespace Serein.NodeFlow.Model
{

    public class SingleFlipflopNode : NodeModelBase
    {
        //public override async Task<object?> Executing(IDynamicContext context)
        //public override Task<object?>  ExecutingAsync(IDynamicContext context)
        //{
        //    NextOrientation = Library.Enums.ConnectionType.IsError;
        //    RuningException = new FlipflopException ("无法以非await/async的形式调用触发器");
        //    return null;
        //}



        public override async Task<object?> ExecutingAsync(IDynamicContext context)
        {
            #region 执行前中断
            if (DebugSetting.InterruptClass != InterruptClass.None) // 执行触发前
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
                var args = GetParameters(context, this, md);
                var result = await dd.Invoke(md.ActingInstance, args);
                dynamic flipflopContext = result;
                FlipflopStateType flipflopStateType = flipflopContext.State;
                NextOrientation = flipflopStateType.ToContentType();
                if (flipflopContext.Type == TriggerType.Overtime)
                {
                    throw new FlipflopException(base.MethodDetails.MethodName + "触发器超时触发。Guid" + base.Guid);
                }
                return flipflopContext.Value;
            
            }
            catch (FlipflopException ex)
            {
                if(ex.Clsss == FlipflopException.CancelClass.Flow)
                {
                    throw;
                }
                await Console.Out.WriteLineAsync($"触发器[{this.MethodDetails.MethodName}]异常：" + ex);
                NextOrientation = ConnectionType.None;
                RuningException = ex;
                return null;
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync($"触发器[{this.MethodDetails.MethodName}]异常：" + ex);
                NextOrientation = ConnectionType.IsError;
                RuningException = ex;
                return null;
            }
            finally
            {
                // flipflopTask?.Dispose();
            }
        }
        public static object GetContextValueDynamic(dynamic context)
        {
            return context.Value; // dynamic 会在运行时处理类型
        }
        internal override Parameterdata[] GetParameterdatas()
        {
            if (base.MethodDetails.ExplicitDatas.Length > 0)
            {
                return MethodDetails.ExplicitDatas
                                     .Select(it => new Parameterdata
                                     {
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
