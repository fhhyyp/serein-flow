using Serein.NodeFlow.Model;
using Serein.Workbench.Node.View;

namespace Serein.Workbench.Node.ViewModel
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
