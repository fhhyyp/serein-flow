using Serein.NodeFlow.Model;
using Serein.WorkBench.Node.View;

namespace Serein.WorkBench.Node.ViewModel
{
    public class FlipflopNodeControlViewModel : NodeControlViewModelBase
    {
        private readonly SingleFlipflopNode node;
         public FlipflopNodeControlViewModel(SingleFlipflopNode node) : base(node)
        {
            this.node = node;
        }
    }
}
