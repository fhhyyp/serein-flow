using CommunityToolkit.Mvvm.ComponentModel;
using Serein.Library.Api;
using Serein.NodeFlow.Env;
using Serein.Workbench.Api;
using Serein.Workbench.Models;
using Serein.Workbench.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Serein.Workbench.ViewModels
{
    internal partial class FlowWorkbenchViewModel : ObservableObject
    {
        private readonly IFlowEnvironment flowEnvironment;
        private readonly IWorkbenchEventService workbenchEventService;
        private readonly IKeyEventService keyEventService;

        public FlowWorkbenchViewModel(IFlowEnvironment flowEnvironment, 
                                      IWorkbenchEventService workbenchEventService,
                                      IKeyEventService keyEventService)
        {
            this.flowEnvironment = flowEnvironment;
            this.workbenchEventService = workbenchEventService;
            this.keyEventService = keyEventService;
            EventManager.RegisterClassHandler(typeof(Window), Keyboard.KeyDownEvent, new KeyEventHandler(OnKeyDown)); // 按下事件
            EventManager.RegisterClassHandler(typeof(Window), Keyboard.KeyUpEvent, new KeyEventHandler(OnKeyUp)); // 松开事件
        }
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            keyEventService.KeyDown(e.Key);
        }
        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            keyEventService.KeyUp(e.Key);
        }

      
    }
}
