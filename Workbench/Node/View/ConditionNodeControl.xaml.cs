using Serein.Library.Api;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Model.Nodes;
using Serein.Workbench.Node.ViewModel;

namespace Serein.Workbench.Node.View
{
    /// <summary>
    /// ConditionNode.xaml 的交互逻辑
    /// </summary>
    public partial class ConditionNodeControl : NodeControlBase, INodeJunction
    {
        /// <summary>
        /// 条件节点控件（用于条件控件）
        /// </summary>
        public ConditionNodeControl() : base()
        {

            // 窗体初始化需要
            var env = App.GetService<IFlowEnvironment>();
            base.ViewModel = new ConditionNodeControlViewModel (new SingleConditionNode(env));
            base.ViewModel.IsEnabledOnView = false;
            DataContext = ViewModel;
            base.ViewModel.NodeModel.DisplayName = "[条件节点]";
            InitializeComponent();
        }

        /// <summary>
        /// 条件节点控件（用于条件控件）
        /// </summary>
        /// <param name="viewModel"></param>

        public ConditionNodeControl(ConditionNodeControlViewModel viewModel):base(viewModel)
        {
            DataContext = viewModel;
            viewModel.NodeModel.DisplayName = "[条件节点]";
            InitializeComponent();
        }

        /// <summary>
        /// 入参控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase INodeJunction.ExecuteJunction => this.ExecuteJunctionControl;

        /// <summary>
        /// 下一个调用方法控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase INodeJunction.NextStepJunction => this.NextStepJunctionControl;

        /// <summary>
        /// 返回值控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase INodeJunction.ReturnDataJunction => this.ResultJunctionControl;

        /// <summary>
        /// 方法入参控制点（可能有，可能没）
        /// </summary>
        private JunctionControlBase[] argDataJunction;
        /// <summary>
        /// 方法入参控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase[] INodeJunction.ArgDataJunction
        {
            get
            {
                argDataJunction = new JunctionControlBase[1];
                argDataJunction[0] = this.ArgJunctionControl;
                return argDataJunction;
            }
        }

    }
}
