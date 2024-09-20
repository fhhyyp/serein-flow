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
            if (TryCreateInterruptTask(context, this, out Task<CancelType>? task))
            {
                var cancelType = await task!;
                await Console.Out.WriteLineAsync($"[{this.MethodDetails.MethodName}]中断已{(cancelType == CancelType.Manual ? "手动取消" : "自动取消")}，开始执行后继分支");
            }
            #endregion

            MethodDetails md = MethodDetails;
            Delegate del = md.MethodDelegate;
            object instance = md.ActingInstance;
            var haveParameter = md.ExplicitDatas.Length >= 0;
            try
            {
                // 调用委托并获取结果
                Task<IFlipflopContext> flipflopTask = haveParameter switch
                {
                    true => ((Func<object, object?[]?, Task<IFlipflopContext>>)del).Invoke(instance, GetParameters(context, md)), // 执行流程中的触发器方法时获取入参参数
                    false => ((Func<object, Task<IFlipflopContext>>)del).Invoke(instance),
                };

                IFlipflopContext flipflopContext = (await flipflopTask) ?? throw new FlipflopException("没有返回上下文");
                NextOrientation = flipflopContext.State.ToContentType();
                if(flipflopContext.TriggerData.Type == Library.NodeFlow.Tool.TriggerType.Overtime)
                {
                    throw new FlipflopException("");
                }
                return flipflopContext.TriggerData.Value;
            }
            catch (FlipflopException ex)
            {
                NextOrientation = ConnectionType.None;
                RuningException = ex;
                throw;
            }
            catch (Exception ex)
            {
                NextOrientation = ConnectionType.IsError;
                RuningException = ex;
                return null;
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
