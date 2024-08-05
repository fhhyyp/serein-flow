using Serein.DynamicFlow;
using Serein.DynamicFlow.NodeModel;
using Serein.WorkBench.Node.ViewModel;
using static Serein.WorkBench.MainWindow;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using static Dm.net.buffer.ByteArrayBuffer;

namespace Serein.WorkBench.Node.View
{
    /// <summary>
    /// ConditionNode.xaml 的交互逻辑
    /// </summary>
    public partial class ConditionNodeControl : NodeControlBase
    {
        private readonly ConditionNodeControlViewModel viewModel;

        public ConditionNodeControl() : base()
        {
            
            viewModel = new (new ());
            DataContext = viewModel;
            InitializeComponent();
        }

        public ConditionNodeControl(SingleConditionNode node) : base(node)
        {
            Node = node;
            viewModel = new ConditionNodeControlViewModel(node);
            DataContext = viewModel;
            InitializeComponent();
        }


    }
}
