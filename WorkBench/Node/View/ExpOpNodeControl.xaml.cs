using Serein.NodeFlow.Model;
using Serein.WorkBench.Node.ViewModel;

namespace Serein.WorkBench.Node.View
{
    /// <summary>
    /// ExprOpNodeControl.xaml 的交互逻辑
    /// </summary>
    public partial class ExpOpNodeControl : NodeControlBase
    {
        public ExpOpNodeViewModel ViewModel { get; }

        public ExpOpNodeControl()
        {
            ViewModel = new (new());
            DataContext = ViewModel;
            InitializeComponent();
        }
        public ExpOpNodeControl(SingleExpOpNode node):base(node)
        {
            Node = node;
            ViewModel = new(node);
            DataContext = ViewModel;
            InitializeComponent();
        }

    }
}
