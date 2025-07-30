using Serein.Library;
using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Model.Nodes
{
    public class SingleUINode : NodeModelBase
    {
        public IEmbeddedContent Adapter {  get; private set; }
        public SingleUINode(IFlowEnvironment environment) : base(environment)
        {
        }

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
