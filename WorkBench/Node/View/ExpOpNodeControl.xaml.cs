using Serein.NodeFlow.Model;
using Serein.Workbench.Node.ViewModel;

namespace Serein.Workbench.Node.View
{
    /// <summary>
    /// ExprOpNodeControl.xaml 的交互逻辑
    /// </summary>
    public partial class ExpOpNodeControl : NodeControlBase
    {
        public ExpOpNodeControl() : base()
        {
            // 窗体初始化需要
            ViewModel = new ExpOpNodeViewModel(new SingleExpOpNode(null));
            DataContext = ViewModel;
            InitializeComponent();
        }
        public ExpOpNodeControl(ExpOpNodeViewModel viewModel) :base(viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
