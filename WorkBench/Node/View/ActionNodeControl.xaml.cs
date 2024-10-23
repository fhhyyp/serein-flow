using Serein.NodeFlow.Model;
using Serein.Workbench.Node.ViewModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace Serein.Workbench.Node.View
{
    /// <summary>
    /// ActionNode.xaml 的交互逻辑
    /// </summary>
    public partial class ActionNodeControl : NodeControlBase
    {
        public ActionNodeControl(ActionNodeControlViewModel viewModel) : base(viewModel) 
        {
            DataContext = viewModel;
            InitializeComponent();
            ExecuteJunctionControl.NodeGuid = viewModel.NodeModel.Guid;
        }

    }
}
