using CommunityToolkit.Mvvm.ComponentModel;
using Serein.Library;
using Serein.Library.Api;
using Serein.NodeFlow.Model;
using Serein.Workbench.Api;
using Serein.Workbench.Services;
using Serein.Workbench.ViewModels;
using Serein.Workbench.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Node.ViewModel
{
    public partial class FlowCallNodeControlViewModel : NodeControlViewModelBase
    {
        /// <summary>
        /// 刷新方法控件
        /// </summary>
        public Action<MethodDetails> UploadMethodDetailsControl;


        [ObservableProperty]
        private SingleFlowCallNode flowCallNode;

         /// <summary>
         /// 当前所选画布
         /// </summary>
         [ObservableProperty]
        private FlowCanvasViewModel _selectCanvas; 

        /// <summary>
        /// 当前所选节点
        /// </summary>
        [ObservableProperty]
        private IFlowNode _selectNode;


        [ObservableProperty]
        private FlowCanvasViewModel[] canvass;

       

        private readonly FlowNodeService flowNodeService;
        private readonly IFlowEEForwardingService flowEEForwardingService;

        public FlowCallNodeControlViewModel(SingleFlowCallNode node) : base(node)
        {
            this.FlowCallNode = node;
            flowNodeService = App.GetService<FlowNodeService>();
            flowEEForwardingService = App.GetService<IFlowEEForwardingService>();
            RershCanvass(); // 首次加载
            InitNodeData();
            InitEvent();
        }
        private void InitNodeData()
        {
            if (string.IsNullOrEmpty(FlowCallNode.TargetNodeGuid))
            {
                return;
            }
            var targetNodeControl = flowNodeService.FlowNodeControls.FirstOrDefault(n => n.ViewModel.NodeModel.Guid.Equals(FlowCallNode.TargetNodeGuid));
            if (targetNodeControl is null)
            {
                return;
            }
            if (targetNodeControl.FlowCanvas is FlowCanvasView view )
            {
                SelectCanvas = view.ViewModel;
                SelectNode = targetNodeControl.ViewModel.NodeModel;
            }
        }

        private void InitEvent()
        {
            flowEEForwardingService.CanvasCreated += (e) => RershCanvass(); // 画布创建了
            flowEEForwardingService.CanvasRemoved += (e) => RershCanvass(); // 画布移除了

        }


        partial void OnSelectCanvasChanged(FlowCanvasViewModel value)
        {
            FlowCallNode.ResetTargetNode();
        }

        partial void OnSelectNodeChanged(IFlowNode value)
        {
            if(value is null)
            {
                FlowCallNode.ResetTargetNode();
                return;
            }
            FlowCallNode.SetTargetNode(value.Guid);
        }

        private void RershCanvass()
        {
            var canvass = flowNodeService.FlowCanvass.Select(f => (FlowCanvasViewModel)f.DataContext).ToArray(); // .Where(f => f.Model.PublicNodes.Count > 0)
            Canvass = canvass;
        }



        /*private void RershMds()
        {
            if (NodeModel.IsShareParam && SelectNode is not null)
            {
                UploadMethodDetailsControl?.Invoke(SelectNode.MethodDetails);

            }
            else
            {
                UploadMethodDetailsControl?.Invoke(base.NodeModel.MethodDetails);
            }
        }
*/
        
    }
}
