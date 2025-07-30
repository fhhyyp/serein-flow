using Serein.Library;
using Serein.NodeFlow.Model;
using Serein.Workbench.Node.ViewModel;
using Serein.Workbench.Themes;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Serein.Workbench.Node.View
{
    /// <summary>
    /// ActionNode.xaml 的交互逻辑
    /// </summary>
    public partial class ActionNodeControl : NodeControlBase, INodeJunction
    {
        /// <summary>
        /// 构造函数，传入ViewModel
        /// </summary>
        /// <param name="viewModel"></param>
        public ActionNodeControl(ActionNodeControlViewModel viewModel) : base(viewModel) 
        {
            DataContext = viewModel;
            InitializeComponent();
        }

        /// <summary>
        /// 入参控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase INodeJunction.ExecuteJunction => this.ExecuteJunctionControl;

        /// <summary>
        /// 下一个调用方法控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase INodeJunction.NextStepJunction => this.NextStepJunctionControl;

        /// <summary>
        /// 返回值控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase INodeJunction.ReturnDataJunction => this.ResultJunctionControl;



        /// <summary>
        /// 方法入参控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase[] INodeJunction.ArgDataJunction => GetArgJunction(this, MethodDetailsControl);

    }
}
