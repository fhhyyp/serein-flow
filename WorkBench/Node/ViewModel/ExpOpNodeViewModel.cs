using Serein.DynamicFlow.NodeModel;
using Serein.WorkBench.Node.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.WorkBench.Node.ViewModel
{
    public class ExpOpNodeViewModel: NodeControlViewModel
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


        public ExpOpNodeViewModel(SingleExpOpNode node) 
        { 
            this.node = node;
        }
    }
}
