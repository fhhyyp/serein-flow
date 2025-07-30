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

        /// <summary>
        /// 全局数据控件构造函数，使用默认的全局数据节点模型
        /// </summary>
        public GlobalDataControl() : base()
        {
            // 窗体初始化需要
            var env = App.GetService<IFlowEnvironment>();
            viewModel = new GlobalDataNodeControlViewModel(new SingleGlobalDataNode(env));
            base.ViewModel = new GlobalDataNodeControlViewModel(new SingleGlobalDataNode(env));
            base.ViewModel.IsEnabledOnView = false;
            base.ViewModel.NodeModel.DisplayName = "[全局数据]";
            DataContext = ViewModel;
            InitializeComponent();
        }


        /// <summary>
        /// 全局数据控件构造函数，使用指定的全局数据节点模型
        /// </summary>
        /// <param name="viewModel"></param>
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


        /// <summary>
        /// 放置节点控件到全局数据面板中
        /// </summary>
        /// <param name="nodeControl"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 从全局数据面板中取出节点控件
        /// </summary>
        /// <param name="nodeControl"></param>
        /// <returns></returns>
        public bool TakeOutNode(NodeControlBase nodeControl)
        {
            if (!GlobalDataPanel.Children.Contains(nodeControl))
            {
                return false;
            }
            GlobalDataPanel.Children.Remove(nodeControl);
            return true;
        }

        /// <summary>
        /// 取出所有节点控件（用于删除容器）
        /// </summary>
        public void TakeOutAll()
        {
            GlobalDataPanel.Children.Clear();
        }

    }
}
