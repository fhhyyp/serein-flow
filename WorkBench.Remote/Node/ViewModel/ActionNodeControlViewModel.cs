using Serein.NodeFlow.Model;
using Serein.WorkBench.Node.View;

namespace Serein.WorkBench.Node.ViewModel
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
