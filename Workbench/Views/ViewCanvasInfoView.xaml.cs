using Serein.Library;
using Serein.Library.Api;
using Serein.Workbench.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace Serein.Workbench.Views
{
    /// <summary>
    /// CanvasNodeTreeView.xaml 的交互逻辑
    /// </summary>
    public partial class ViewCanvasInfoView : UserControl
    {
        private readonly ViewCanvasInfoViewModel ViewModel;
        private readonly ViewNodeInfoViewModel NodeInfoViewModel;

        /// <summary>
        /// 画布信息查看视图
        /// </summary>
        public ViewCanvasInfoView()
        {
            this.ViewModel = App.GetService<ViewCanvasInfoViewModel>();
            this.NodeInfoViewModel = App.GetService<ViewNodeInfoViewModel>();
            this.DataContext = this.ViewModel;
            InitializeComponent();
        }

        private void Grid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Grid grid && grid.DataContext is IFlowNode nodeModel)
            {
                NodeInfoViewModel.ViewNodeModel = nodeModel;
                App.GetService<IFlowEnvironment>().FlowEdit.NodeLocate(nodeModel.Guid);
            }

            // 定位节点
            //if (e.ClickCount == 2)
            //{
            //}
        }
    }
}
