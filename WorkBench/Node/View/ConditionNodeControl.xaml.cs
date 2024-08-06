using Serein.Flow;
using Serein.Flow.NodeModel;
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
