using CommunityToolkit.Mvvm.ComponentModel;
using Serein.Library;
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
    internal partial class FlowLibrarysViewModel : ObservableObject
    {
        private readonly IFlowEEForwardingService flowEEForwardingService;

        [ObservableProperty]
        private ObservableCollection<FlowLibraryInfo> flowLibraryInfos; 



        public FlowLibrarysViewModel(IFlowEEForwardingService flowEEForwardingService)
        {
            this.flowEEForwardingService = flowEEForwardingService;
            FlowLibraryInfos = new ObservableCollection<FlowLibraryInfo>();
            flowEEForwardingService.OnDllLoad += FlowEEForwardingService_OnDllLoad;
        }

        private void FlowEEForwardingService_OnDllLoad(Library.Api.LoadDllEventArgs eventArgs)
        {
            if (!eventArgs.IsSucceed) return;
            List<MethodDetailsInfo> mds = eventArgs.MethodDetailss;
            NodeLibraryInfo libraryInfo = eventArgs.NodeLibraryInfo;

            var methodInfo = new ObservableCollection<FlowLibraryMethodDetailsInfo>();
            foreach (var md in mds) 
            {
                methodInfo.Add(new FlowLibraryMethodDetailsInfo(md));
            }
            var flInfo = new FlowLibraryInfo
            {
                LibraryName = libraryInfo.AssemblyName,
                FilePath = libraryInfo.FilePath,
                MethodInfo = methodInfo
            };

            FlowLibraryInfos.Add(flInfo);



        }
    }
}