using Newtonsoft.Json;
using Serein.Library;
using Serein.Library.Api;
using Serein.Workbench.Avalonia.Api;
using Serein.Workbench.Avalonia.Services;
using Serein.Workbench.Avalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Avalonia.Custom.ViewModels
{
    internal partial class FlowLibrarysViewModel : ViewModelBase
    {
        /// <summary>
        /// 运行环境
        /// </summary>
        private IFlowEnvironment flowEnvironment { get; }
        /// <summary>
        /// 流程运行环境事件服务
        /// </summary>
        private IFlowEEForwardingService feefService { get; }

        /// <summary>
        /// 运行环境加载的依赖
        /// </summary>
        public ObservableCollection<LibraryMds> LibraryList { get; } = new ObservableCollection<LibraryMds>();



        public FlowLibrarysViewModel()
        {
            flowEnvironment = App.GetService<IFlowEnvironment>();
            feefService = App.GetService<IFlowEEForwardingService>();
            feefService.OnDllLoad += FeefService_OnDllLoad; ; 

        }


        /// <summary>
        /// 加载了依赖信息
        /// </summary>
        /// <param name="e"></param>
        private void FeefService_OnDllLoad(LoadDllEventArgs e)
        {
            Debug.WriteLine(e.NodeLibraryInfo.AssemblyName + "  count :" + e.MethodDetailss.Count);
            var libraryMds = new LibraryMds { AssemblyName = e.NodeLibraryInfo.AssemblyName, Mds = e.MethodDetailss.ToArray() };
            LibraryList.Add(libraryMds);
        }



    }
}
