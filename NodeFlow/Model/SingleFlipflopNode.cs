﻿using Serein.Library.Api;
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
            if (DebugSetting.IsInterrupt && TryCreateInterruptTask(context, this, out Task<CancelType>? task)) // 执行触发前
            {
                string guid = this.Guid.ToString();
                this.CancelInterruptCallback ??= () => context.FlowEnvironment.ChannelFlowInterrupt.TriggerSignal(guid);
                var cancelType = await task!;
                task?.ToString();
                await Console.Out.WriteLineAsync($"[{this.MethodDetails.MethodName}]中断已{(cancelType == CancelType.Manual ? "手动取消" : "自动取消")}，开始执行后继分支");
            }
            #endregion

            MethodDetails md = MethodDetails;
            var del = md.MethodDelegate.Clone();
            object instance = md.ActingInstance;
            // Task<IFlipflopContext>? flipflopTask = null;
            try
            {
                // 调用委托并获取结果
                Task<IFlipflopContext> flipflopTask = md.ExplicitDatas.Length  switch
                {
                    0 => ((Func<object, Task<IFlipflopContext>>)del).Invoke(md.ActingInstance),
                    _ => ((Func<object, object?[]?, Task<IFlipflopContext>>)del).Invoke(md.ActingInstance, GetParameters(context, md)), // 执行流程中的触发器方法时获取入参参数
                };
                //object?[]? parameters;
                //object? result = null;
                //if (haveParameter)
                //{
                //    var data = GetParameters(context, md);
                //    parameters = [instance, data];
                //}
                //else
                //{
                //    parameters = [instance];
                //}
                //flipflopTask = del.DynamicInvoke(parameters) as Task<IFlipflopContext>;
                //if (flipflopTask == null)
                //{
                //    throw new FlipflopException(base.MethodDetails.MethodName + "触发器返回值非 Task<IFlipflopContext> 类型");
                //}
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
