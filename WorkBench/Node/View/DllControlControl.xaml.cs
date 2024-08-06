using Serein.NodeFlow;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Serein.WorkBench.Node.View
{





    /// <summary>
    /// UserControl1.xaml 的交互逻辑
    /// </summary>
    public partial class DllControl : UserControl
    {

        public DllControl()
        {
            Header = "DLL文件"; // 设置初始值
            InitializeComponent();
        }


        /// <summary>
        /// Header 依赖属性，用于绑定标题
        /// </summary>
        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(string), typeof(DllControl), new PropertyMetadata(string.Empty));






        /// <summary>
        /// 向动作面板添加类型的文本块
        /// </summary>
        /// <param name="type">要添加的类型</param>
        public void AddAction(MethodDetails md)
        {
            AddTypeToListBox(md, ActionsListBox);
        }

        /// <summary>
        /// 向触发器面板添加类型的文本块
        /// </summary>
        /// <param name="type">要添加的类型</param>
        public void AddFlipflop(MethodDetails md)
        {
            AddTypeToListBox(md, FlipflopsListBox);
        }

        /// <summary>
        /// 向指定面板添加类型的文本块
        /// </summary>
        /// <param name="type">要添加的类型</param>
        /// <param name="panel">要添加到的面板</param>
        private void AddTypeToListBox(MethodDetails md, ListBox listBox)
        {
            // 创建一个新的 TextBlock 并设置其属性
            TextBlock typeText = new TextBlock
            {
                Text = $"{md.MethodTips}",
                Margin = new Thickness(10, 2, 0, 0),
                Tag = md
            };
            // 为 TextBlock 添加鼠标左键按下事件处理程序
            typeText.MouseLeftButtonDown += TypeText_MouseLeftButtonDown;
            // 为 TextBlock 添加鼠标移动事件处理程序
            typeText.MouseMove += TypeText_MouseMove;
            // 将 TextBlock 添加到指定的面板
            listBox.Items.Add(typeText);
        }

        /// <summary>
        /// 存储拖拽开始时的鼠标位置
        /// </summary>
        private Point _dragStartPoint;

        /// <summary>
        /// 处理 TextBlock 的鼠标左键按下事件
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void TypeText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 记录鼠标按下时的位置
            _dragStartPoint = e.GetPosition(null);
        }

        /// <summary>
        /// 处理 TextBlock 的鼠标移动事件
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void TypeText_MouseMove(object sender, MouseEventArgs e)
        {
            // 获取当前鼠标位置
            Point mousePos = e.GetPosition(null);
            // 计算鼠标移动的距离
            Vector diff = _dragStartPoint - mousePos;

            // 判断是否符合拖拽的最小距离要求
            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                // 获取触发事件的 TextBlock

                TextBlock typeText = sender as TextBlock;

                if (typeText != null)
                {
                    // 创建一个 DataObject 用于拖拽操作，并设置拖拽效果
                    DataObject dragData = new DataObject(MouseNodeType.DllNodeType, typeText.Tag);
                    DragDrop.DoDragDrop(typeText, dragData, DragDropEffects.Move);
                }
            }
        }





        


    }
}