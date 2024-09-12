using Serein.Library.Api;
using Serein.Library.Core.NodeFlow;

namespace Serein.NodeFlow.Model
{

    public class SingleFlipflopNode : NodeBase
    {
        public override object Execute(IDynamicContext context)
        {
            throw new NotImplementedException("无法以非await/async的形式调用触发器");
        }

    }
}
