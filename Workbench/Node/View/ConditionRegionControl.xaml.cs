using Serein.NodeFlow.Model;
using Serein.Workbench.Node.ViewModel;
using System.Windows;

namespace Serein.Workbench.Node.View
{
    /// <summary>
    /// ConditionRegion.xaml 的交互逻辑
    /// </summary>
    public partial class ConditionRegionControl : NodeControlBase
    {
        public new CompositeConditionNode ViewModel => ViewModel;

        public ConditionRegionControl() : base()
        {
            base.ViewModel.IsEnabledOnView = false;
            InitializeComponent();
        }

        public ConditionRegionControl(ConditionRegionNodeControlViewModel viewModel) : base(viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }



        /// <summary>
        /// 添加条件控件
        /// </summary>
        /// <param name="node"></param>
        public void AddCondition(NodeControlBase node)
        {
            
            //((CompositeConditionNode)ViewModel.NodeModel).AddNode((SingleConditionNode)node.ViewModel.NodeModel);
            ViewModel.AddNode((SingleConditionNode)node.ViewModel.NodeModel);

            this.Width += node.Width;
            this.Height += node.Height;
            ConditionsListBox.Items.Add(node);
        }

        private void ConditionsListBox_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        // Mouse event handlers for dragging
        //private void TypeText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        //{
        //    _dragStartPoint = e.GetPosition(null);
        //}

        //private void TypeText_MouseMove(object sender, MouseEventArgs e)
        //{
        //    Point mousePos = e.GetPosition(null);
        //    Vector diff = _dragStartPoint - mousePos;

        //    if (e.LeftButton == MouseButtonState.Pressed &&
        //        (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
        //         Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
        //    {
        //        if (sender is TextBlock typeText)
        //        {
        //            var dragData = new DataObject(MouseNodeType.RegionType, typeText.Tag);
        //            DragDrop.DoDragDrop(typeText, dragData, DragDropEffects.Move);
        //        }
        //    }
        //}

        

        /*private void TypeText_MouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = _dragStartPoint - mousePos;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                TextBlock typeText = sender as TextBlock;
                if (typeText != null)
                {
                    DataObject dragData = new DataObject("Type", typeText.Tag);
                    DragDrop.DoDragDrop(typeText, dragData, DragDropEffects.Move);
                }
            }
        }*/

    }
}
