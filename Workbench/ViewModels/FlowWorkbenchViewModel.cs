using CommunityToolkit.Mvvm.ComponentModel;
using Serein.Workbench.Api;
using Serein.Workbench.Models;
using Serein.Workbench.Services;
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
        private readonly IWorkbenchEventService workbenchEventService;

        public FlowWorkbenchViewModel(IFlowEEForwardingService flowEEForwardingService, IWorkbenchEventService workbenchEventService)
        {
            this.flowEEForwardingService = flowEEForwardingService;
            this.workbenchEventService = workbenchEventService;
            //flowEEForwardingService.OnDllLoad += FlowEEForwardingService_OnDllLoad;
        }
    }
}
