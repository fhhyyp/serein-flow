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
using System.Windows.Threading;

namespace Serein.Workbench.Node.View
{
    /// <summary>
    /// ScriptNodeControl.xaml 的交互逻辑
    /// </summary>
    public partial class ScriptNodeControl : NodeControlBase
    {
        private ScriptNodeControlViewModel viewModel => (ScriptNodeControlViewModel)ViewModel;
        private DispatcherTimer _debounceTimer; // 用于延迟更新
        private bool _isUpdating = false;    // 防止重复更新

        public ScriptNodeControl()
        {
            InitializeComponent();
        }
        public ScriptNodeControl(ScriptNodeControlViewModel viewModel) : base(viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();

#if false
            // 初始化定时器
            _debounceTimer = new DispatcherTimer();
            _debounceTimer.Interval = TimeSpan.FromMilliseconds(500); // 停止输入 500ms 后更新
            _debounceTimer.Tick += DebounceTimer_Tick; 
#endif
        }


#if false
        // 每次输入时重置定时器
        private void RichTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        // 定时器事件，用户停止输入后触发
        private async void DebounceTimer_Tick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();

            if (_isUpdating)
                return;

            // 开始后台处理语法分析和高亮
            _isUpdating = true;
            await Task.Run(() => HighlightKeywordsAsync(viewModel.Script));
        }

        // 异步执行语法高亮操作
        private async Task HighlightKeywordsAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            // 模拟语法分析和高亮（可以替换为实际逻辑）
            var highlightedText = text;

            // 在 UI 线程中更新 RichTextBox 的内容
            await Dispatcher.BeginInvoke(() =>
            {
                var range = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
                range.Text = highlightedText;
            });

            _isUpdating = false;
        }

#endif




    }
}
