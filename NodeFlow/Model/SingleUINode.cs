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

        public override async Task<object> ExecutingAsync(IDynamicContext context)
        {
            if(Adapter is null)
            {

                var result = await base.ExecutingAsync(context);
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
                Adapter.GetFlowControl().OnExecuting(data);
            }
            
            return Task.FromResult<object?>(null);
        }
    }
}
