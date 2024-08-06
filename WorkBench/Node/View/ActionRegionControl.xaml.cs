using Serein.Flow.NodeModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using static Serein.WorkBench.MainWindow;

namespace Serein.WorkBench.Node.View
{
    /// <summary>
    /// ActionRegion.xaml 的交互逻辑
    /// </summary>
    public partial class ActionRegionControl : NodeControlBase
    {
        private Point _dragStartPoint;

        private new readonly CompositeActionNode Node;

        public ActionRegionControl() : base()

        {
            InitializeComponent();
        }
        public ActionRegionControl(CompositeActionNode node) : base(node)
        {
            InitializeComponent();
            Node = node;
            base.Name = "动作组合节点";
        }

        #region 动态调整区域大小
        private void ResizeTop_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double oldHeight = Height;
            double newHeight = Math.Max(MinHeight, oldHeight - e.VerticalChange);
            Height = newHeight;
            Canvas.SetTop(this, Canvas.GetTop(this) + (oldHeight - newHeight));
        }

        private void ResizeBottom_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newHeight = Math.Max(MinHeight, Height + e.VerticalChange);
            Height = newHeight;
        }

        private void ResizeLeft_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double oldWidth = Width;
            double newWidth = Math.Max(MinWidth, oldWidth - e.HorizontalChange);
            Width = newWidth;
            Canvas.SetLeft(this, Canvas.GetLeft(this) + (oldWidth - newWidth));
        }

        private void ResizeRight_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newWidth = Math.Max(MinWidth, Width + e.HorizontalChange);
            Width = newWidth;
        }

#endregion


        public void AddAction(NodeControlBase node, bool isTask = false)
        {
            /*TextBlock actionText = new TextBlock
            {
                Text = node.MethodDetails.MethodName + (isTask ? " (Task)" : ""),
                Margin = new Thickness(10, 2, 0, 0),
                Tag = node.MethodDetails,
            };*/
            Node?.AddNode((SingleActionNode)node.Node);
            ActionsListBox.Items.Add(node);
        }

       /* public async Task ExecuteActions(DynamicContext context)
        {
            foreach (TextBlock item in ActionsListBox.Items)
            {
                dynamic tag = item.Tag;
                IAction action = tag.Action;
                bool isTask = tag.IsTask;

                if (isTask)
                {
                    await Task.Run(() => action.Execute(Node.MethodDetails, context));
                }
                else
                {
                    action.Execute(Node.MethodDetails, context);
                }
            }
        }*/



        private void ActionsListBox_Drop(object sender, DragEventArgs e)
        {
            /*if (e.Data.GetDataPresent("Type"))
            {
                Type droppedType = e.Data.GetData("Type") as Type;

                if (droppedType != null && typeof(ICondition).IsAssignableFrom(droppedType) && droppedType.IsClass)
                {
                    // 创建一个新的 TextBlock 并设置其属性
                    TextBlock conditionText = new TextBlock
                    {
                        Text = droppedType.Name,
                        Margin = new Thickness(10, 2, 0, 0),
                        Tag = droppedType
                    };

                    // 为 TextBlock 添加鼠标左键按下事件处理程序
                    // conditionText.MouseLeftButtonDown += TypeText_MouseLeftButtonDown;
                    // 为 TextBlock 添加鼠标移动事件处理程序
                    // conditionText.MouseMove += TypeText_MouseMove;

                    // 将 TextBlock 添加到 ActionsListBox 中
                    ActionsListBox.Items.Add(conditionText);
                }
            }*/
            e.Handled = true;
        }

        // 用于拖动的鼠标事件处理程序
        private void TypeText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void TypeText_MouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = _dragStartPoint - mousePos;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                if (sender is TextBlock typeText)
                {
                    var dragData = new DataObject(MouseNodeType.RegionType, typeText.Tag);
                    DragDrop.DoDragDrop(typeText, dragData, DragDropEffects.Move);
                }
            }
        }


    }
}
