using Serein.Library.Api;
using Serein.Library.Entity;
using Serein.Library.Enums;
using Serein.Library.Ex;
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
            if (!context.Env.TryGetDelegate(md.MethodName, out var del))
            {
                throw new Exception("不存在对应委托");
            }
            object instance = md.ActingInstance;
            try
            {
                Task<IFlipflopContext> flipflopTask;
                if (md.ExplicitDatas.Length == 0)
                {
                    if (del is Func<object, Task<IFlipflopContext>> function)
                    {
                        flipflopTask = function.Invoke(md.ActingInstance);
                    }
                    else
                    {
                        throw new FlipflopException("触发节点非预期的返回类型", true, FlipflopException.CancelClass.Flow);
                    }
                }
                else
                {
                    var parameters = GetParameters(context, this, md);
                    if(del is Func<object, object?[]?, Task<IFlipflopContext>> function)
                    {
                        flipflopTask = function.Invoke(md.ActingInstance, parameters);
                    }
                    else
                    {
                        throw new FlipflopException("触发节点非预期的返回类型", true,FlipflopException.CancelClass.Flow);
                    }
                }

                IFlipflopContext flipflopContext = (await flipflopTask) ?? throw new FlipflopException("没有返回上下文");
                NextOrientation = flipflopContext.State.ToContentType();
                if(flipflopContext.TriggerData is null || flipflopContext.TriggerData.Type == Library.NodeFlow.Tool.TriggerType.Overtime)
                {
                    throw new FlipflopException(base.MethodDetails.MethodName + "触发器超时触发。Guid"+base.Guid);
                }
                return flipflopContext.TriggerData.Value;
            }
            catch (FlipflopException ex)
            {
                if(ex.Clsss == FlipflopException.CancelClass.Flow)
                {
                    throw;
                }
                await Console.Out.WriteLineAsync($"触发器[{this.MethodDetails.MethodName}]异常：" + ex.Message);
                NextOrientation = ConnectionType.None;
                RuningException = ex;
                return null;
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync($"触发器[{this.MethodDetails.MethodName}]异常：" + ex.Message);
                NextOrientation = ConnectionType.IsError;
                RuningException = ex;
                return null;
            }
            finally
            {
                // flipflopTask?.Dispose();
            }
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
