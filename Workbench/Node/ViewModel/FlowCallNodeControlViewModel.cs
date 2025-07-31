using CommunityToolkit.Mvvm.ComponentModel;
using Serein.Library;
using Serein.Library.Api;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Model.Nodes;
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
        public Action<IFlowNode?> UploadNode;


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

        /// <summary>
        /// 流程接口节点构造函数
        /// </summary>
        /// <param name="node"></param>
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

        /// <summary>
        /// 选择画布发生改变
        /// </summary>
        /// <param name="value"></param>
        partial void OnSelectCanvasChanged(FlowCanvasViewModel value)
        {
            FlowCallNode.ResetTargetNode();  // 改变画布直接重置
        }

        /// <summary>
        /// 选择的节点发生改变
        /// </summary>
        /// <param name="value"></param>
        partial void OnSelectNodeChanged(IFlowNode value)
        {
            if(value is null)
            {
                UploadNode?.Invoke(null);
                FlowCallNode.ResetTargetNode(); // 如果是不选择了，则重置一下
                return;
            }
            UploadNode?.Invoke(value);
            FlowCallNode.SetTargetNode(value.Guid); // 重新设置目标节点
        }

        /// <summary>
        /// 刷新可选画布
        /// </summary>
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
