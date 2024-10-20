using System.Windows;

namespace Serein.Workbench
{
    /// <summary>
    /// DebugWindow.xaml 的交互逻辑
    /// </summary>
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using System.Timers;
    using System.Windows;

    /// <summary>
    /// LogWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LogWindow : Window
    {
        private StringBuilder logBuffer = new StringBuilder();
        private int logUpdateInterval = 500; // 批量更新的时间间隔（毫秒）
        private Timer logUpdateTimer;
        private const int MaxLines = 1000; // 最大显示的行数
        private bool autoScroll = true; // 自动滚动标识
        private int flushThreshold = 1000; // 设置日志刷新阈值
        private const int maxFlushSize = 1000; // 每次最大刷新字符数

        public LogWindow()
        {
            InitializeComponent();

            // 初始化定时器，用于批量更新日志
            logUpdateTimer = new Timer(logUpdateInterval);
            logUpdateTimer.Elapsed += (s, e) => FlushLog(); // 定时刷新日志
            logUpdateTimer.Start();

            // 添加滚动事件处理，判断用户是否手动滚动
            // LogTextBox.ScrollChanged += LogTextBox_ScrollChanged;
        }

        /// <summary>
        /// 添加日志到缓冲区
        /// </summary>
        public void AppendText(string text)
        {
            lock (logBuffer)
            {
                logBuffer.Append(text);

                // 异步写入日志到文件
                // Task.Run(() => File.AppendAllText("log.txt", text));
                FlushLog();
                // 如果日志达到阈值，立即刷新
                //if (logBuffer.Length > flushThreshold)
                //{
                //    FlushLog();
                //}
            }
        }

        /// <summary>
        /// 清空日志缓冲区并更新到 TextBox 中
        /// </summary>
        private void FlushLog()
        {
            if (logBuffer.Length == 0) return;

            Dispatcher.InvokeAsync(() =>
            {
                lock (logBuffer)
                {
                    // 仅追加部分日志，避免一次更新过多内容
                    string logContent = logBuffer.Length > maxFlushSize
                        ? logBuffer.ToString(0, maxFlushSize)
                        : logBuffer.ToString();
                    logBuffer.Remove(0, logContent.Length); // 清空已更新的部分

                    LogTextBox.Dispatcher.Invoke(() =>
                    {
                        LogTextBox.AppendText(logContent);
                    });
                    
                }

                // 不必每次都修剪日志，当行数超过限制20%时再修剪
                if (LogTextBox.LineCount > MaxLines * 1.2)
                {
                    TrimLog();
                }

                ScrollToEndIfNeeded(); // 根据是否需要自动滚动来决定
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// 限制日志输出的最大行数，超出时删除旧日志
        /// </summary>
        private void TrimLog()
        {
            if (LogTextBox.LineCount > MaxLines)
            {
                // 删除最早的多余行
                LogTextBox.Text = LogTextBox.Text.Substring(
                    LogTextBox.GetCharacterIndexFromLineIndex(LogTextBox.LineCount - MaxLines));
            }
        }

        /// <summary>
        /// 检测用户是否手动滚动了文本框
        /// </summary>
        private void LogTextBox_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
        {
            if (e.ExtentHeightChange == 0) // 用户手动滚动时
            {
                // 判断是否滚动到底部
                //autoScroll = LogTextBox.VerticalOffset == LogTextBox.ScrollableHeight;
            }
        }

        /// <summary>
        /// 根据 autoScroll 标志决定是否滚动到末尾
        /// </summary>
        private void ScrollToEndIfNeeded()
        {
            if (autoScroll)
            {
                LogTextBox.ScrollToEnd(); // 仅在需要时滚动到末尾
            }
        }

        /// <summary>
        /// 清空日志
        /// </summary>
        public void Clear()
        {
            Dispatcher.BeginInvoke(() =>
            {
                LogTextBox.Clear();
            });
        }

        /// <summary>
        /// 点击清空日志按钮时触发
        /// </summary>
        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
        }

        /// <summary>
        /// 窗口关闭事件，隐藏窗体而不是关闭
        /// </summary>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            logBuffer?.Clear();
            Clear();
            e.Cancel = true;  // 取消关闭操作
            this.Hide();      // 隐藏窗体而不是关闭
        }
    }

}
