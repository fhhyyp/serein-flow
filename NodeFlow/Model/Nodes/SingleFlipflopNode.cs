using Serein.Library.Api;
using Serein.Library;
using Serein.Library.Utils;
using System;

namespace Serein.NodeFlow.Model.Nodes
{
    [FlowDataProperty(ValuePath = NodeValuePath.Node, IsNodeImp = true)]
    public partial class SingleFlipflopNode
    {
        /// <summary>
        /// <para>是否等待后继节点（仅对于全局触发器）</para>
        /// <para>如果为 true，则在触发器获取结果后，等待后继节点执行完成，才会调用触发器</para>
        /// <para>如果为 false，则触发器获取到结果后，将使用 _ = Task.Run(...) 再次调用触发器</para>
        /// </summary>

        private bool _isWaitSuccessorNodes = true;
    }

    /// <summary>
    /// 触发器节点
    /// </summary>
    public partial class SingleFlipflopNode : NodeModelBase
    {
        /// <summary>
        /// 构造一个新的单触发器节点实例。
        /// </summary>
        /// <param name="environment"></param>
        public SingleFlipflopNode(IFlowEnvironment environment) : base(environment)
        {

        }


        /// <summary>
        /// 执行触发器进行等待触发
        /// </summary>
        /// <param name="context"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public override async Task<FlowResult> ExecutingAsync(IFlowContext context, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return FlowResult.Fail(Guid, context, "流程操作已取消");
            }

            #region 执行前中断
            if (DebugSetting.IsInterrupt) // 执行触发前
            {
                SereinEnv.WriteLine(InfoType.INFO, $"[{MethodDetails.MethodName}]进入中断");
                await DebugSetting.GetInterruptTask.Invoke();
                SereinEnv.WriteLine(InfoType.INFO, $"[{MethodDetails.MethodName}]中断已取消，开始执行后继分支");
            }
            #endregion

            MethodDetails md = MethodDetails;
            if (!context.Env.TryGetDelegateDetails(md.AssemblyName, md.MethodName, out var dd)) // 流程运行到某个节点
            {
                context.Exit();
                context.ExceptionOfRuning = new FlipflopException($"无法获取到委托 {md.MethodName} 的详细信息。请检查流程配置。");
                return FlowResult.Fail(Guid, context, "不存在对应委托");
            }


            var ioc = Env.FlowControl.IOC;
            var instance = ioc.Get(md.ActingInstanceType);
            if (instance is null)
            {
                ioc.Register(md.ActingInstanceType).Build();
                instance = ioc.Get(md.ActingInstanceType);
            }

            var args = MethodDetails.ParameterDetailss.Length == 0 ? [] 
                       : await this.GetParametersAsync(context, token);

            var result = await dd.InvokeAsync(instance, args);
            var flowResult = FlowResult.OK(this.Guid, context, result);
            return flowResult;
        }

    }
}
