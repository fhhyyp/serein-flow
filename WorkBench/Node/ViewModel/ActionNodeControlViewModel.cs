using Serein.NodeFlow.Model;
using Serein.Workbench.Node.View;

namespace Serein.Workbench.Node.ViewModel
{
    public class ActionNodeControlViewModel : NodeControlViewModelBase
    {
        private readonly SingleActionNode node;

        public ActionNodeControlViewModel(SingleActionNode node):base(node) 
        {
            this.node = node;
        }
    }
}
