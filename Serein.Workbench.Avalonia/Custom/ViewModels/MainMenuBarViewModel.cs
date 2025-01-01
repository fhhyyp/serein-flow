using Microsoft.Extensions.DependencyInjection;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.Workbench.Avalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Avalonia.Custom.ViewModels
{
    internal partial class MainMenuBarViewModel : ViewModelBase
    {
        private IFlowEnvironment flowEnvironment { get; }

        
        public MainMenuBarViewModel()
        {
            flowEnvironment = App.GetService<IFlowEnvironment>();

            var uiContextOperation = App.GetService<UIContextOperation>();
        }

        public void SaveProjectCommand()
        {
            flowEnvironment.SaveProject();
        }


    }



}
