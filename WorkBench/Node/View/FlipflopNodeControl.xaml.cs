using Serein.NodeFlow.Model;
using Serein.WorkBench.Node.ViewModel;

namespace Serein.WorkBench.Node.View
{
    /// <summary>
    /// StateNode.xaml 的交互逻辑
    /// </summary>
    public partial class FlipflopNodeControl : NodeControlBase
    {
        private readonly FlipflopNodeControlViewModel viewModel;

        public FlipflopNodeControl(SingleFlipflopNode node) : base(node)
        {
            Node = node;
            viewModel = new FlipflopNodeControlViewModel(node);
            DataContext = viewModel;
            InitializeComponent();

        }

        //private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    var comboBox = sender as ComboBox;
        //    if (comboBox == null)
        //    {
        //        return;
        //    }
        //    var selectedExplicitData = comboBox.DataContext as ExplicitData;
        //    if (selectedExplicitData == null)
        //    {
        //        return;
        //    }

        //    Console.WriteLine (selectedExplicitData.DataValue, "Selected Value");
        //}
    }
}
