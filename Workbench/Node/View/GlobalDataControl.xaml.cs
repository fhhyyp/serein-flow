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
    /// UserControl1.xaml 的交互逻辑
    /// </summary>
    public partial class GlobalDataControl : NodeControlBase, INodeJunction, INodeContainerControl
    {
        public GlobalDataControl() : base()
        {
            // 窗体初始化需要
            base.ViewModel = new GlobalDataNodeControlViewModel(new SingleGlobalDataNode(null));
            DataContext = ViewModel;
            InitializeComponent();
        }

        public GlobalDataControl(GlobalDataNodeControlViewModel viewModel) : base(viewModel)
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
        JunctionControlBase INodeJunction.ReturnDataJunction => throw new NotImplementedException();

        /// <summary>
        /// 方法入参控制点（可能有，可能没）
        /// </summary>
        JunctionControlBase[] INodeJunction.ArgDataJunction => throw new NotImplementedException();


        public bool PlaceNode(NodeControlBase nodeControl)
        {
            if (GlobalDataPanel.Children.Contains(nodeControl))
            {
                return false;
            }
            GlobalDataPanel.Children.Add(nodeControl);
            return true;
        }

        public bool TakeOutNode(NodeControlBase nodeControl)
        {
            if (!GlobalDataPanel.Children.Contains(nodeControl))
            {
                return false;
            }
            GlobalDataPanel.Children.Remove(nodeControl);
            return true;
        }

        public void TakeOutAll()
        {
            GlobalDataPanel.Children.Clear();
        }

    }
}
