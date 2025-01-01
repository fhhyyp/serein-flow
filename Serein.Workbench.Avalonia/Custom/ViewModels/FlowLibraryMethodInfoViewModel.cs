using CommunityToolkit.Mvvm.ComponentModel;
using Serein.Library;
using Serein.Workbench.Avalonia.Services;
using Serein.Workbench.Avalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Avalonia.Custom.ViewModels
{
    
    internal partial class FlowLibraryMethodInfoViewModel : ViewModelBase
    {
        /// <summary>
        /// 当前预览的方法信息
        /// </summary>

        [ObservableProperty]
        private MethodDetailsInfo methodDetailsInfo;


        private IWorkbenchEventService workbenchEventService;

        public FlowLibraryMethodInfoViewModel()
        {
            workbenchEventService = App.GetService<IWorkbenchEventService>();
            workbenchEventService.OnPreviewlMethodInfo += WorkbenchEventService_OnPreviewlMethodInfo;
            methodDetailsInfo = new MethodDetailsInfo
            {
                AssemblyName = "wait selection...",
                MethodAnotherName = "wait selection...",
                MethodName = "wait selection...",
                NodeType = "wait selection...",
                ReturnTypeFullName = "wait selection...",
                IsParamsArgIndex = -1,
                ParameterDetailsInfos = []
            };
        }


        private void WorkbenchEventService_OnPreviewlMethodInfo(PreviewlMethodInfoEventArgs eventArgs)
        {
            var mdInfo = eventArgs.MethodDetailsInfo;
            MethodDetailsInfo = mdInfo;
            Debug.WriteLine($"预览了 {mdInfo.AssemblyName } - {mdInfo.MethodAnotherName}  方法");
        }











    }





}
