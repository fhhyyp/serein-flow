using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.VisualTree;

namespace Serein.Workbench.Avalonia.Controls
{
    /// <summary>
    /// 实现拖动的控件
    /// </summary>
    public partial class DragControls : UserControl
    {
        /// <summary>
        /// 记录上一次鼠标位置
        /// </summary>
        private Point lastMousePosition;

        /// <summary>
        /// 用于平滑更新坐标的计时器
        /// </summary>
        private DispatcherTimer _timer;

        /// <summary>
        /// 标记是否先启动了拖动
        /// </summary>
        private bool isDragging = false;

        /// <summary>
        /// 需要更新的坐标点
        /// </summary>
        private PixelPoint _targetPosition;

        public DragControls()
        {
            InitializeComponent();

            // 添加当前控件的事件监听
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;

            // 初始化计时器
            _timer = new DispatcherTimer
            {


                Interval = TimeSpan.FromMilliseconds(10)
            };
            _timer.Tick += OnTimerTick;
        }

        /// <summary>
        /// 计时器事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTimerTick(object sender, EventArgs e)
        {


            var window = this.FindAncestorOfType<Window>();
            if (window != null && window.Position != _targetPosition)
            {


                // 更新坐标
                window.Position = _targetPosition;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnPointerPressed(object sender, PointerPressedEventArgs e)
        {


            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            // 启动拖动
            isDragging = true;
            // 记录当前坐标
            lastMousePosition = e.GetPosition(this);
            e.Handled = true;
            // 启动计时器
            _timer.Start();
        }

        private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
        {


            if (!isDragging) return;
            // 停止拖动
            isDragging = false;
            e.Handled = true;
            // 停止计时器
            _timer.Stop();
        }

        private void OnPointerMoved(object sender, PointerEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

            // 如果没有启动拖动，则不执行
            if (!isDragging) return;

            var currentMousePosition = e.GetPosition(this);
            var offset = currentMousePosition - lastMousePosition;
            var window = this.FindAncestorOfType<Window>();
            if (window != null)
            {
                // 记录当前坐标
                _targetPosition = new PixelPoint(window.Position.X + (int)offset.X,
                    window.Position.Y + (int)offset.Y);
            }
        }
    }
}
