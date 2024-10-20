using Serein.Library;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Serein.Workbench.Node.View
{
    /// <summary>
    /// ActionRegion.xaml 的交互逻辑
    /// </summary>
    public partial class ActionRegionControl : NodeControlBase
    {
        private Point _dragStartPoint;

        //private new readonly CompositeActionNode Node;

        //public override NodeControlViewModel ViewModel { get ; set ; }

        public ActionRegionControl() : base(null)
        {
            InitializeComponent();
        }
        //public ActionRegionControl(CompositeActionNode node)
        //{
        //    InitializeComponent();
        //    //ViewModel = new NodeControlViewModel(node);
        //    DataContext = ViewModel;
        //    base.Name = "动作组合节点";
        //}

        public void AddAction(NodeControlBase node, bool isTask = false)
        {
            /*TextBlock actionText = new TextBlock
            {
                Text = node.MethodDetails.MethodName + (isTask ? " (Task)" : ""),
                Margin = new Thickness(10, 2, 0, 0),
                Tag = node.MethodDetails,
            };*/
            /// Node?.AddNode((SingleActionNode)node.ViewModel.Node);
            // ActionsListBox.Items.Add(node);
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
                    MoveNodeData moveNodeData = new MoveNodeData
                    {
                        NodeControlType = Library.NodeControlType.ConditionRegion
                    };

                    // 创建一个 DataObject 用于拖拽操作，并设置拖拽效果
                    DataObject dragData = new DataObject(MouseNodeType.CreateDllNodeInCanvas, moveNodeData);

                    DragDrop.DoDragDrop(typeText, dragData, DragDropEffects.Move);


                    //var dragData = new DataObject(MouseNodeType.CreateNodeInCanvas, typeText.Tag);
                    //DragDrop.DoDragDrop(typeText, dragData, DragDropEffects.Move);
                }
            }
        }


    }
}
