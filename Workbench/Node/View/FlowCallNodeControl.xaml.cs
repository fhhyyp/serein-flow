using Serein.Library;
using Serein.Library.Api;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Model.Nodes;
using Serein.Workbench.Node.ViewModel;
using System.Windows;
using System.Windows.Controls;

namespace Serein.Workbench.Node.View
{
    /// <summary>
    /// FlowCallNodeControl.xaml 的交互逻辑
    /// </summary>
    public partial class FlowCallNodeControl : NodeControlBase, INodeJunction
    {
        private new FlowCallNodeControlViewModel ViewModel { get; set; }

        /// <summary>
        /// 流程接口节点控件构造函数
        /// </summary>
        public FlowCallNodeControl()
        {
            var env = App.GetService<IFlowEnvironment>();
            base.ViewModel = new FlowCallNodeControlViewModel(new SingleFlowCallNode(env));
            base.ViewModel.IsEnabledOnView = false;
            DataContext = base.ViewModel;
            base.ViewModel.NodeModel.DisplayName = "[流程接口]";
            InitializeComponent();
        }

        /// <summary>
        /// 流程接口节点控件构造函数，传入ViewModel
        /// </summary>
        /// <param name="viewModel"></param>
        public FlowCallNodeControl(FlowCallNodeControlViewModel viewModel) : base(viewModel)
        {
            DataContext = viewModel;
            ViewModel = viewModel;
            viewModel.NodeModel.DisplayName = "[流程接口]";
            InitializeComponent();
            ViewModel.UploadNode = UploadMethodDetailsControl;

        }

        private void UploadMethodDetailsControl(IFlowNode? flowNode)
        {
            if(flowNode is null)
            {
                this.MaxWidth = 300;
            }
            else
            {
                this.MaxWidth = 300;
            }
        }

        /// <summary>
        /// 入参控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase INodeJunction.ExecuteJunction => this.ExecuteJunctionControl;

        /// <summary>
        /// 下一个调用方法控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase INodeJunction.NextStepJunction => throw new NotImplementedException("不存在下一个调用控制点");

        /// <summary>
        /// 返回值控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase INodeJunction.ReturnDataJunction => throw new NotImplementedException("不存在返回值控制点");


        /// <summary>
        /// 方法入参控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase[] INodeJunction.ArgDataJunction => GetArgJunction(this,MethodDetailsControl);

      

    }
}
