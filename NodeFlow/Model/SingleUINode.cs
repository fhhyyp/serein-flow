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

        public override async Task<object?> ExecutingAsync(IDynamicContext context, CancellationToken token)
        {
            if (token.IsCancellationRequested) return null;
            if(Adapter is null)
            {

                var result = await base.ExecutingAsync(context, token);
                if (result is IEmbeddedContent adapter) 
                {
                    this.Adapter = adapter;
                    context.NextOrientation = ConnectionInvokeType.IsSucceed;
                }
                else
                {
                    context.NextOrientation = ConnectionInvokeType.IsError;
                }
            }
            else
            {
                var p = context.GetPreviousNode(this);
                var data = context.GetFlowData(p.Guid);
                var iflowContorl = Adapter.GetFlowControl();
                iflowContorl.OnExecuting(data);
            }
            
            return null;
        }
    }
}
