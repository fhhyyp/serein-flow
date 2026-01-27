using CommunityToolkit.Mvvm.ComponentModel;
using Serein.Library;
using Serein.Library.Api;
using Serein.NodeFlow.Env;
using Serein.NodeFlow.Services;
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
    internal partial class FlowLibrarysViewModel : ObservableObject
    {
        private readonly IFlowEEForwardingService flowEEForwardingService;
        private readonly IFlowEnvironment flowEnvironment;
        [ObservableProperty]
        private ObservableCollection<Models.FlowLibraryInfo> flowLibraryInfos; 

        public FlowLibrarysViewModel(IFlowEEForwardingService flowEEForwardingService,IFlowEnvironment flowEnvironment)
        {
            this.flowEEForwardingService = flowEEForwardingService;
            this.flowEnvironment = flowEnvironment;
            FlowLibraryInfos = new ObservableCollection<Models.FlowLibraryInfo>();
            flowEEForwardingService.DllLoad += FlowEEForwardingService_OnDllLoad;


            //var baseLibrary = App.GetService<IFlowLibraryService>().LoadBaseLibrary();
        }
        /// <summary>
        /// 加载文件依赖
        /// </summary>
        /// <param name="filePath"></param>
        public void LoadFileLibrary(string filePath)
        {
            try
            {
                flowEnvironment.LoadLibrary(filePath);
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(ex);
                return;
            }
        }

        private void FlowEEForwardingService_OnDllLoad(Library.Api.LoadDllEventArgs eventArgs)
        {
            if (!eventArgs.IsSucceed) return;
            List<MethodDetailsInfo> mds = eventArgs.NodeLibraryInfo.MethodInfos.ToList() ;
            Library.FlowLibraryInfo libraryInfo = eventArgs.NodeLibraryInfo;

            var methodInfo = new ObservableCollection<MethodDetailsInfo>();
            foreach (var md in mds) 
            {
                methodInfo.Add(md);
            }
            var flInfo = new Models.FlowLibraryInfo
            {
                LibraryName = libraryInfo.AssemblyName,
                FilePath = libraryInfo.FilePath,
                MethodInfo = methodInfo
            };

            FlowLibraryInfos.Add(flInfo);



        }
    }
}