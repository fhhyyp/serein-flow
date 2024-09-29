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
            // Task<IFlipflopContext>? flipflopTask = null;
            try
            {
                // 调用委托并获取结果
                //Task<IFlipflopContext> flipflopTask = md.ExplicitDatas.Length  switch
                //{
                //    0 => ((Func<object, Task<IFlipflopContext>>)del).Invoke(md.ActingInstance),
                //    _ => ((Func<object, object?[]?, Task<IFlipflopContext>>)del).Invoke(md.ActingInstance, GetParameters(context, this, md)), // 执行流程中的触发器方法时获取入参参数
                //};
                Task<IFlipflopContext> flipflopTask;
                if (md.ExplicitDatas.Length == 0)
                {
                    flipflopTask = ((Func<object, Task<IFlipflopContext>>)del).Invoke(md.ActingInstance);
                }
                else
                {
                    var parameters = GetParameters(context, this, md);
                    flipflopTask = ((Func<object, object?[]?, Task<IFlipflopContext>>)del).Invoke(md.ActingInstance, parameters);
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
                await Console.Out.WriteLineAsync(ex.ToString());
                NextOrientation = ConnectionType.None;
                RuningException = ex;
                return null;
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync(ex.ToString());
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
