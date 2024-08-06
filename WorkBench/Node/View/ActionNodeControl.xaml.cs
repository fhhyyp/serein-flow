using Serein.Flow.NodeModel;
using Serein.WorkBench.Node.ViewModel;

namespace Serein.WorkBench.Node.View
{
    /// <summary>
    /// ActionNode.xaml 的交互逻辑
    /// </summary>
    public partial class ActionNodeControl : NodeControlBase
    {
        private readonly ActionNodeControlViewModel actionNodeControlViewModel;
        public ActionNodeControl(SingleActionNode node) : base(node)
        {
            Node = node;
            actionNodeControlViewModel = new ActionNodeControlViewModel(node);
            DataContext = actionNodeControlViewModel;
            InitializeComponent();
        }


    }
}
