using Serein.Library;
using Serein.Workbench.Customs;
using Serein.Workbench.ViewModels;
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

namespace Serein.Workbench.Views
{
    /// <summary>
    /// BaseNodesView.xaml 的交互逻辑
    /// </summary>
    public partial class BaseNodesView : UserControl
    {
        /// <summary>
        /// 基础节点视图构造函数
        /// </summary>
        public BaseNodesView()
        {
            this.DataContext = App.GetService<Locator>().BaseNodesViewModel;
            InitializeComponent();
        }

        /// <summary>
        /// 基础节点的拖拽放置创建
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BaseNodeControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is UserControl control)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    // 创建一个 DataObject 用于拖拽操作，并设置拖拽效果
                    var dragData = new DataObject(MouseNodeType.CreateBaseNodeInCanvas, control.GetType());
                    try
                    {
                        DragDrop.DoDragDrop(control, dragData, DragDropEffects.Move);
                    }
                    catch (Exception ex)
                    {
                        SereinEnv.WriteLine(ex);
                    }
                }

            }
        }
    }
}
