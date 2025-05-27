using Serein.Library;
using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Model
{
    /// <summary>
    /// 流程调用节点
    /// </summary>
    public class SingleFlowCallNode : NodeModelBase
    {
        public SingleFlowCallNode(IFlowEnvironment environment) : base(environment)
        {

        }

        /// <summary>
        /// 需要调用其它流程图中的某个节点
        /// </summary>
        /// <param name="context"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public override async Task<FlowResult> ExecutingAsync(IDynamicContext context, CancellationToken token)
        { 
            await base.ExecutingAsync(context, token);
            return new FlowResult(this, context, null);
        }
    }
}