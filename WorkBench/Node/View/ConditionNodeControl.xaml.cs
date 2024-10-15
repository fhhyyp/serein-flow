using Serein.NodeFlow.Model;
using Serein.Workbench.Node.ViewModel;

namespace Serein.Workbench.Node.View
{
    /// <summary>
    /// ConditionNode.xaml 的交互逻辑
    /// </summary>
    public partial class ConditionNodeControl : NodeControlBase
    {
        public ConditionNodeControl() : base()
        {
            // 窗体初始化需要
            ViewModel = new ConditionNodeControlViewModel (new SingleConditionNode());
            DataContext = ViewModel;
            InitializeComponent();
        }

        public ConditionNodeControl(ConditionNodeControlViewModel viewModel):base(viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }

    }
}
