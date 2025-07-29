using Serein.Library.Api;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Model.Nodes;
using Serein.Workbench.Api;
using Serein.Workbench.Node.ViewModel;

namespace Serein.Workbench.Node.View
{
    /// <summary>
    /// UserControl1.xaml 的交互逻辑
    /// </summary>
    public partial class GlobalDataControl : NodeControlBase, INodeJunction, INodeContainerControl
    {
        private readonly GlobalDataNodeControlViewModel viewModel;

        public GlobalDataControl() : base()
        {
            // 窗体初始化需要
            var env = App.GetService<IFlowEnvironment>();
            base.ViewModel = new GlobalDataNodeControlViewModel(new SingleGlobalDataNode(env));
            base.ViewModel.IsEnabledOnView = false;
            base.ViewModel.NodeModel.DisplayName = "[全局数据]";
            DataContext = ViewModel;
            InitializeComponent();
        }

        public GlobalDataControl(GlobalDataNodeControlViewModel viewModel) : base(viewModel)
        {
            DataContext = viewModel;
            viewModel.NodeModel.DisplayName = "[全局数据]";
            InitializeComponent();
            this.viewModel = viewModel;
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
        JunctionControlBase INodeJunction.ReturnDataJunction => throw new NotImplementedException();

        /// <summary>
        /// 方法入参控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase[] INodeJunction.ArgDataJunction => throw new NotImplementedException();


        public bool PlaceNode(NodeControlBase nodeControl)
        {
            if (GlobalDataPanel.Children.Contains(nodeControl))
            {
                return false;
            }
            //viewModel.NodeModel is SingleGlobalDataNode
            GlobalDataPanel.Children.Add(nodeControl);
            return true;
        }

        public bool TakeOutNode(NodeControlBase nodeControl)
        {
            if (!GlobalDataPanel.Children.Contains(nodeControl))
            {
                return false;
            }
            GlobalDataPanel.Children.Remove(nodeControl);
            return true;
        }

        public void TakeOutAll()
        {
            GlobalDataPanel.Children.Clear();
        }

    }
}
