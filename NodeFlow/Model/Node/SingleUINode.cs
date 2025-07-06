using Serein.Library;
using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Model
{
    public class SingleUINode : NodeModelBase
    {
        public IEmbeddedContent Adapter {  get; private set; }
        public SingleUINode(IFlowEnvironment environment) : base(environment)
        {
        }

        public override async Task<FlowResult> ExecutingAsync(IDynamicContext context, CancellationToken token)
        {
            if (token.IsCancellationRequested) return new FlowResult(this.Guid, context);
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

            return new FlowResult(this.Guid, context);
        }
    }
}
