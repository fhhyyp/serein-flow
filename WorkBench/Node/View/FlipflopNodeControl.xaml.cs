using Serein.NodeFlow.Model;
using Serein.WorkBench.Node.ViewModel;

namespace Serein.WorkBench.Node.View
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
