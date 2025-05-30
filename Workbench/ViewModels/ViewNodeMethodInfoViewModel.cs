using CommunityToolkit.Mvvm.ComponentModel;
using Serein.Library;
using Serein.Workbench.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.ViewModels
{
    internal partial class ViewNodeMethodInfoViewModel : ObservableObject
    {
        private readonly FlowNodeService flowNodeService;

        /// <summary>
        /// 当前预览的节点
        /// </summary>
        [ObservableProperty]
        private MethodDetailsInfo mdInfo;

        public ViewNodeMethodInfoViewModel(FlowNodeService flowNodeService)
        {
            this.flowNodeService = flowNodeService;

            InitEvent();
        }
        private void InitEvent()
        {
            flowNodeService.OnViewMethodDetailsInfoChanged += OnViewMethodDetailsInfoChanged;
        }
        private void OnViewMethodDetailsInfoChanged(MethodDetailsInfo methodDetailsInfo)
        {
            MdInfo = methodDetailsInfo;
        }

    }
}
