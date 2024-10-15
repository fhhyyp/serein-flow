using Serein.NodeFlow.Model;
using Serein.Workbench.Node.ViewModel;

namespace Serein.Workbench.Node.View
{
    /// <summary>
    /// StateNode.xaml 的交互逻辑
    /// </summary>
    public partial class FlipflopNodeControl : NodeControlBase
    {
        public FlipflopNodeControl(FlipflopNodeControlViewModel viewModel) : base(viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
