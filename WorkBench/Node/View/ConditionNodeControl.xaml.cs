using Serein.NodeFlow.Model;
using Serein.WorkBench.Node.ViewModel;

namespace Serein.WorkBench.Node.View
{
    /// <summary>
    /// ConditionNode.xaml 的交互逻辑
    /// </summary>
    public partial class ConditionNodeControl : NodeControlBase
    {
        public ConditionNodeControlViewModel ViewModel { get; }

        public ConditionNodeControl() : base()
        {

            ViewModel = new (new ());
            DataContext = ViewModel;
            InitializeComponent();
        }

        public ConditionNodeControl(SingleConditionNode node) : base(node)
        {
            Node = node;
            ViewModel = new ConditionNodeControlViewModel(node);
            DataContext = ViewModel;
            InitializeComponent();
        }


    }
}
