using Serein.Library;
using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Model.Nodes
{
    /// <summary>
    /// 单个UI节点，适用于需要在流程中嵌入用户自定义控件的场景。
    /// </summary>
    public class SingleUINode : NodeModelBase
    {
        /// <summary>
        /// 适配的UI控件，必须实现IEmbeddedContent接口。
        /// </summary>
        public IEmbeddedContent? Adapter {  get; private set; }

        /// <summary>
        /// 单个UI节点构造函数，初始化流程环境。
        /// </summary>
        /// <param name="environment"></param>
        public SingleUINode(IFlowEnvironment environment) : base(environment)
        {
        }

        /// <summary>
        /// 执行节点逻辑，适用于嵌入式UI控件的流程节点。
        /// </summary>
        /// <param name="context"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public override async Task<FlowResult> ExecutingAsync(IFlowContext context, CancellationToken token)
        {
            if (token.IsCancellationRequested) return FlowResult.Fail(this.Guid, context, "流程已通过token取消");
            if(Adapter is null)
            {

                var result = await base.ExecutingAsync(context, token);
                if (result.Value is IEmbeddedContent adapter) 
                {
                    Adapter = adapter;
                    context.NextOrientation = ConnectionInvokeType.IsSucceed;
                }
                else
                {
                    context.NextOrientation = ConnectionInvokeType.IsError;
                }
            }
            else
            {
                var p = context.GetPreviousNode(this.Guid);
                var data = context.GetFlowData(p).Value;
                var iflowContorl = Adapter.GetFlowControl();
                iflowContorl.OnExecuting(data);
            }

            return FlowResult.OK(this.Guid, context, null);
        }
    }
}
