using Serein.NodeFlow.Model;
using Serein.WorkBench.Node.View;

namespace Serein.WorkBench.Node.ViewModel
{
    public class ExpOpNodeViewModel: NodeControlViewModelBase
    {
        public readonly SingleExpOpNode node;

        public string Expression
        {
            get => node.Expression;
            set
            {
                node.Expression = value;
                OnPropertyChanged();
            }
        }


        public ExpOpNodeViewModel(SingleExpOpNode node) : base(node)
        { 
            this.node = node;
        }
    }
}
