using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.ViewModels
{
    internal class Locator
    {
        private static IServiceProvider ServiceProvide { get; set; }
        public Locator(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

      
        public MainViewModel MainViewModel => App.GetService<MainViewModel>() ?? throw new NotImplementedException();
        public MainMenuBarViewModel MainMenuBarViewModel => App.GetService<MainMenuBarViewModel>() ?? throw new NotImplementedException();
        public FlowWorkbenchViewModel FlowWorkbenchViewModel => App.GetService<FlowWorkbenchViewModel>() ?? throw new NotImplementedException();
        public BaseNodesViewModel BaseNodesViewModel => App.GetService<BaseNodesViewModel>() ?? throw new NotImplementedException();
        public FlowLibrarysViewModel FlowLibrarysViewModel => App.GetService<FlowLibrarysViewModel>() ?? throw new NotImplementedException();
        public FlowEditViewModel FlowEditViewModel => App.GetService<FlowEditViewModel>() ?? throw new NotImplementedException();
        public ViewNodeInfoViewModel NodeInfoViewModel => App.GetService<ViewNodeInfoViewModel>() ?? throw new NotImplementedException();
        public ViewNodeMethodInfoViewModel ViewNodeMethodInfoViewModel => App.GetService<ViewNodeMethodInfoViewModel>() ?? throw new NotImplementedException();



        public FlowCanvasViewModel FlowCanvasViewModel => App.GetService<FlowCanvasViewModel>() ?? throw new NotImplementedException();
        public ViewCanvasInfoViewModel CanvasNodeTreeViewModel => App.GetService<ViewCanvasInfoViewModel>() ?? throw new NotImplementedException();

        public IServiceProvider ServiceProvider { get; }
    }
}
