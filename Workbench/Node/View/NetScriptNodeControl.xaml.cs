using Serein.NodeFlow.Model;
using Serein.Workbench.Node.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Serein.Workbench.Node.View
{
    /// <summary>
    /// NetScriptNodeControl.xaml 的交互逻辑
    /// </summary>
    public partial class NetScriptNodeControl : NodeControlBase , INodeJunction
    {
        public NetScriptNodeControl()
        {
            base.ViewModel = new NetScriptNodeControlViewModel(new SingleNetScriptNode(null));
            base.ViewModel.IsEnabledOnView = false;
            base.DataContext = ViewModel;
            InitializeComponent();
        }

        public NetScriptNodeControl(NetScriptNodeControlViewModel viewModel) : base(viewModel)
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
        JunctionControlBase INodeJunction.ReturnDataJunction => throw new Exception();


        public JunctionControlBase[] ArgDataJunction => [];

    }
}
