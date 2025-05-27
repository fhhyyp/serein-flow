using Serein.NodeFlow.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Node.ViewModel
{
    public partial class FlowCallNodeControlViewModel : NodeControlViewModelBase
    {
        public new SingleFlowCallNode NodelModel { get; }
        public FlowCallNodeControlViewModel(SingleFlowCallNode node) : base(node)
        {
            this.NodelModel = node;
        }
    }
}
