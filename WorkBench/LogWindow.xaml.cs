using System.Windows;

namespace Serein.WorkBench
{
    /// <summary>
    /// DebugWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LogWindow : Window
    {
        public LogWindow()
        {
            InitializeComponent();
        }
        public void AppendText(string text)
        {
            Dispatcher.BeginInvoke(() =>
            {
                LogTextBox.AppendText(text);
                LogTextBox.ScrollToEnd();
            });
        }
        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;  // 取消关闭操作
            this.Hide();      // 隐藏窗体而不是关闭
        }
    }
}
