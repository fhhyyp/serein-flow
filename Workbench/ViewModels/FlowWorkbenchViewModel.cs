using CommunityToolkit.Mvvm.ComponentModel;
using Serein.Workbench.Api;
using Serein.Workbench.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.ViewModels
{
    internal partial class FlowWorkbenchViewModel : ObservableObject
    {
        private readonly IFlowEEForwardingService flowEEForwardingService;



        public FlowWorkbenchViewModel(IFlowEEForwardingService flowEEForwardingService)
        {
            this.flowEEForwardingService = flowEEForwardingService;
            //flowEEForwardingService.OnDllLoad += FlowEEForwardingService_OnDllLoad;
        }
    }
}
