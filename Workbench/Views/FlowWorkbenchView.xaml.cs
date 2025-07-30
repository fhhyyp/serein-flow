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
using System.Windows.Shapes;

namespace Serein.Workbench.Views
{
    /// <summary>
    /// FlowWorkbenchView.xaml 的交互逻辑
    /// </summary>
    public partial class FlowWorkbenchView : Window
    {
        private FlowWorkbenchViewModel ViewModel => ViewModel as FlowWorkbenchViewModel;

        /// <summary>
        /// FlowWorkbenchView 的交互逻辑
        /// </summary>
        public FlowWorkbenchView()
        {
            this.DataContext = App.GetService<Locator>().FlowWorkbenchViewModel;
            InitializeComponent();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.MaxHeight = SystemParameters.PrimaryScreenHeight;
            this.WindowState = WindowState.Maximized;
            // 设置全屏
            /*this.WindowState = System.Windows.WindowState.Normal;
            this.WindowStyle = System.Windows.WindowStyle.None;
            this.ResizeMode = System.Windows.ResizeMode.NoResize;
            this.Topmost = true;

            this.Left = 0.0;
            this.Top = 0.0;
            this.Width = System.Windows.SystemParameters.PrimaryScreenWidth;
            this.Height = System.Windows.SystemParameters.PrimaryScreenHeight;*/
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 确保所有窗口关闭
            LogWindow.Instance.Close();
            System.Windows.Application.Current.Shutdown();
        }

        /// <summary>
        /// 处理鼠标按下事件，确保点击空白区域时清除焦点
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            // 获取当前的焦点控件
            var element = FocusManager.GetFocusedElement(this);

            // 如果当前有焦点控件，且点击的区域不在该控件上，则清除焦点
            if (element != null && !element.IsMouseOver)
            {
                // 将焦点设置到窗口本身或其他透明控件
                FocusManager.SetFocusedElement(this, this);
            }

            // 继续处理默认的鼠标按下事件
            base.OnPreviewMouseDown(e);
        }

    }
}
