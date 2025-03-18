using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.ViewModels
{
    public partial class FlowCanvasViewModel : ObservableObject
    {

        [ObservableProperty]
        private bool _isConnectionInvokeNode;

        [ObservableProperty]
        private bool _isConnectionArgSourceNode;
    }
}
