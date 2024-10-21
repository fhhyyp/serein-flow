using Serein.NodeFlow.Model;
using Serein.Workbench.Node.View;

namespace Serein.Workbench.Node.ViewModel
{
    public class FlipflopNodeControlViewModel : NodeControlViewModelBase
    {
        public new SingleFlipflopNode NodelModel { get;}
         public FlipflopNodeControlViewModel(SingleFlipflopNode node) : base(node)
        {
            this.NodelModel = node;
        }
    }
}
