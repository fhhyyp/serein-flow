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
    }
}
