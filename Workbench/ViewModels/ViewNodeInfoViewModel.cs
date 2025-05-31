using CommunityToolkit.Mvvm.ComponentModel;
using Serein.Library;
using Serein.Library.Api;
using Serein.Workbench.Node.View;
using Serein.Workbench.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.ViewModels
{
    internal partial class ViewNodeInfoViewModel : ObservableObject
    {
        private readonly FlowNodeService flowNodeService;

        /// <summary>
        /// 当前预览的节点
        /// </summary>
        [ObservableProperty]
        private IFlowNode viewNodeModel;

        public ViewNodeInfoViewModel(FlowNodeService flowNodeService)
        {
            this.flowNodeService = flowNodeService;
            InitEvent();
        }

        private void InitEvent()
        {
            flowNodeService.OnViewNodeControlChanged += OnViewNodeControlChanged;
        }
        private void OnViewNodeControlChanged(NodeControlBase viewNodeControl)
        {
            if(viewNodeControl is null)
            {
                ViewNodeModel = null;
            }
            else
            {
                ViewNodeModel = viewNodeControl.ViewModel.NodeModel;
            }

        }
    }
}
