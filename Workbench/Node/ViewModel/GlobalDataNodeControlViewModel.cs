using Serein.NodeFlow.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Node.ViewModel
{
    public class GlobalDataNodeControlViewModel : NodeControlViewModelBase
    {
        public new SingleGlobalDataNode NodelModel { get; }
        public GlobalDataNodeControlViewModel(SingleGlobalDataNode node) : base(node)
        {
            this.NodelModel = node;
        }
    }
}
