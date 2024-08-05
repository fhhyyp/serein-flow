using Serein.DynamicFlow.NodeModel;
using Serein.WorkBench.Node.View;

namespace Serein.WorkBench.Node.ViewModel
{
    public class FlipflopNodeControlViewModel : NodeControlViewModel
    {
        private readonly SingleFlipflopNode node;
         public FlipflopNodeControlViewModel(SingleFlipflopNode node)
        {
            this.node = node;
            MethodDetails = node.MethodDetails;
        }
    }
}
