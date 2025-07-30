using Serein.Workbench.Node.ViewModel;
using System.Windows;
using System.Windows.Controls;

namespace Serein.Workbench.Node.View
{
    /// <summary>
    /// StateNode.xaml 的交互逻辑
    /// </summary>
    public partial class FlipflopNodeControl : NodeControlBase, INodeJunction
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="viewModel"></param>
        public FlipflopNodeControl(FlipflopNodeControlViewModel viewModel) : base(viewModel)
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
