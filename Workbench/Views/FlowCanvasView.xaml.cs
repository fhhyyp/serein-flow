using Serein.Library;
using Serein.Library.Api;
using Serein.Workbench.Node.View;
using Serein.Workbench.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Serein.Workbench.Views
{
    /// <summary>
    /// FlowCanvasView.xaml 的交互逻辑
    /// </summary>
    public partial class FlowCanvasView : UserControl
    {
        private FlowCanvasViewModel ViewModel;
        /// <summary>
        /// 存储所有的连接。考虑集成在运行环境中。
        /// </summary>
        private List<ConnectionControl> Connections { get; } = [];

        #region 与画布相关的字段

        /// <summary>
        /// 标记是否正在尝试选取控件
        /// </summary>
        private bool IsSelectControl;
        /// <summary>
        /// 标记是否正在进行连接操作
        /// </summary>
        //private bool IsConnecting;
        /// <summary>
        /// 标记是否正在拖动控件
        /// </summary>
        private bool IsControlDragging;
        /// <summary>
        /// 标记是否正在拖动画布
        /// </summary>
        private bool IsCanvasDragging;
        private bool IsSelectDragging;

        /// <summary>
        /// 当前选取的控件
        /// </summary>
        private readonly List<NodeControlBase> selectNodeControls = [];

        /// <summary>
        /// 记录开始拖动节点控件时的鼠标位置
        /// </summary>
        private Point startControlDragPoint;
        /// <summary>
        /// 记录移动画布开始时的鼠标位置
        /// </summary>
        private Point startCanvasDragPoint;
        /// <summary>
        /// 记录开始选取节点控件时的鼠标位置
        /// </summary>
        private Point startSelectControolPoint;



        /// <summary>
        /// 组合变换容器
        /// </summary>
        private readonly TransformGroup canvasTransformGroup;
        /// <summary>
        /// 缩放画布
        /// </summary>
        private readonly ScaleTransform scaleTransform;
        /// <summary>
        /// 平移画布 
        /// </summary>
        private readonly TranslateTransform translateTransform;
        #endregion

        private IFlowEnvironment EnvDecorator;
        public FlowCanvasView()
        {
            ViewModel = App.GetService<Locator>().FlowCanvasViewModel;
            this.DataContext = ViewModel;
            EnvDecorator =  App.GetService<IFlowEnvironment>();
            InitializeComponent();

            #region 缩放平移容器
            canvasTransformGroup = new TransformGroup();
            scaleTransform = new ScaleTransform();
            translateTransform = new TranslateTransform();
            canvasTransformGroup.Children.Add(scaleTransform);
            canvasTransformGroup.Children.Add(translateTransform);
            FlowChartCanvas.RenderTransform = canvasTransformGroup;
            #endregion
        }

        /// <summary>
        /// 鼠标在画布移动。
        /// 选择控件状态下，调整选择框大小
        /// 连接状态下，实时更新连接线的终点位置。
        /// 移动画布状态下，移动画布。
        /// </summary>
        private void FlowChartCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var myData = GlobalJunctionData.MyGlobalConnectingData;
            if (myData.IsCreateing && e.LeftButton == MouseButtonState.Pressed)
            {

                if (myData.Type == JunctionOfConnectionType.Invoke)
                {
                    ViewModel.IsConnectionInvokeNode = true; // 正在连接节点的调用关系

                }
                else
                {
                    ViewModel.IsConnectionArgSourceNode = true; // 正在连接节点的调用关系
                }
                var currentPoint = e.GetPosition(FlowChartCanvas);
                currentPoint.X -= 2;
                currentPoint.Y -= 2;
                myData.UpdatePoint(currentPoint);
                return;
            }



            if (IsCanvasDragging && e.MiddleButton == MouseButtonState.Pressed) // 正在移动画布（按住中键） 
            {
                Point currentMousePosition = e.GetPosition(this);
                double deltaX = currentMousePosition.X - startCanvasDragPoint.X;
                double deltaY = currentMousePosition.Y - startCanvasDragPoint.Y;

                translateTransform.X += deltaX;
                translateTransform.Y += deltaY;

                startCanvasDragPoint = currentMousePosition;

                foreach (var line in Connections)
                {
                    line.RefreshLine(); // 画布移动时刷新所有连接线
                }
            }

            if (IsSelectControl) // 正在选取节点
            {
                IsSelectDragging = e.LeftButton == MouseButtonState.Pressed;
                // 获取当前鼠标位置
                Point currentPoint = e.GetPosition(FlowChartCanvas);

                // 更新选取矩形的位置和大小
                double x = Math.Min(currentPoint.X, startSelectControolPoint.X);
                double y = Math.Min(currentPoint.Y, startSelectControolPoint.Y);
                double width = Math.Abs(currentPoint.X - startSelectControolPoint.X);
                double height = Math.Abs(currentPoint.Y - startSelectControolPoint.Y);

                Canvas.SetLeft(SelectionRectangle, x);
                Canvas.SetTop(SelectionRectangle, y);
                SelectionRectangle.Width = width;
                SelectionRectangle.Height = height;

            }
        }

        /// <summary>
        /// 放置操作，根据拖放数据创建相应的控件，并处理相关操作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlowChartCanvas_Drop(object sender, DragEventArgs e)
        {
            try
            {
                var canvasDropPosition = e.GetPosition(FlowChartCanvas); // 更新画布落点
                PositionOfUI position = new PositionOfUI(canvasDropPosition.X, canvasDropPosition.Y);
                if (e.Data.GetDataPresent(MouseNodeType.CreateDllNodeInCanvas))
                {
                    if (e.Data.GetData(MouseNodeType.CreateDllNodeInCanvas) is MoveNodeData nodeData)
                    {
                        var canvasGuid = this.ViewModel.CanvasGuid;
                        Task.Run(async () =>
                        {
                            await EnvDecorator.CreateNodeAsync(canvasGuid, nodeData.NodeControlType, position, nodeData.MethodDetailsInfo); // 创建DLL文件的节点对象
                        });
                    }
                }
                else if (e.Data.GetDataPresent(MouseNodeType.CreateBaseNodeInCanvas))
                {
                    if (e.Data.GetData(MouseNodeType.CreateBaseNodeInCanvas) is Type droppedType)
                    {
                        NodeControlType nodeControlType = droppedType switch
                        {
                            Type when typeof(ConditionRegionControl).IsAssignableFrom(droppedType) => NodeControlType.ConditionRegion, // 条件区域
                            Type when typeof(ConditionNodeControl).IsAssignableFrom(droppedType) => NodeControlType.ExpCondition,
                            Type when typeof(ExpOpNodeControl).IsAssignableFrom(droppedType) => NodeControlType.ExpOp,
                            Type when typeof(GlobalDataControl).IsAssignableFrom(droppedType) => NodeControlType.GlobalData,
                            Type when typeof(ScriptNodeControl).IsAssignableFrom(droppedType) => NodeControlType.Script,
                            Type when typeof(NetScriptNodeControl).IsAssignableFrom(droppedType) => NodeControlType.NetScript,
                            _ => NodeControlType.None,
                        };
                        if (nodeControlType != NodeControlType.None)
                        {
                            var canvasGuid = this.ViewModel.CanvasGuid;
                            Task.Run(async () =>
                            {
                                await EnvDecorator.CreateNodeAsync(canvasGuid, nodeControlType, position); // 创建基础节点对象
                            });
                        }
                    }
                }
                e.Handled = true;
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(InfoType.ERROR, ex.ToString());
            }
        }

        /// <summary>
        /// 拖动效果，根据拖放数据是否为指定类型设置拖放效果
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlowChartCanvas_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(MouseNodeType.CreateDllNodeInCanvas)
                || e.Data.GetDataPresent(MouseNodeType.CreateBaseNodeInCanvas))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        /// <summary>
        /// 在画布中尝试选取控件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlowChartCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (GlobalJunctionData.MyGlobalConnectingData.IsCreateing)
            {
                return;
            }
            if (!IsSelectControl)
            {
                // 进入选取状态
                IsSelectControl = true;
                IsSelectDragging = false; // 初始化为非拖动状态

                // 记录鼠标起始点
                startSelectControolPoint = e.GetPosition(FlowChartCanvas);

                // 初始化选取矩形的位置和大小
                Canvas.SetLeft(SelectionRectangle, startSelectControolPoint.X);
                Canvas.SetTop(SelectionRectangle, startSelectControolPoint.Y);
                SelectionRectangle.Width = 0;
                SelectionRectangle.Height = 0;

                // 显示选取矩形
                SelectionRectangle.Visibility = Visibility.Visible;
                SelectionRectangle.ContextMenu ??= ConfiguerSelectionRectangle();

                // 捕获鼠标，以便在鼠标移动到Canvas外部时仍能处理事件
                FlowChartCanvas.CaptureMouse();
            }
            else
            {
                // 如果已经是选取状态，单击则认为结束框选
                CompleteSelection();
            }

            e.Handled = true; // 防止事件传播影响其他控件
        }

        /// <summary>
        /// 在画布中释放鼠标按下，结束选取状态 / 停止创建连线，尝试连接节点
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void FlowChartCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsSelectControl)
            {
                // 松开鼠标时判断是否为拖动操作
                if (IsSelectDragging)
                {
                    // 完成拖动框选
                    CompleteSelection();
                }

                // 释放鼠标捕获
                FlowChartCanvas.ReleaseMouseCapture();
            }

            // 创建连线
            if (GlobalJunctionData.MyGlobalConnectingData is ConnectingData myData && myData.IsCreateing)
            {

                if (myData.IsCanConnected)
                {
                    var canvas = this.FlowChartCanvas;
                    var currentendPoint = e.GetPosition(canvas); // 当前鼠标落点
                    var changingJunctionPosition = myData.CurrentJunction.TranslatePoint(new Point(0, 0), canvas);
                    var changingJunctionRect = new Rect(changingJunctionPosition, new Size(myData.CurrentJunction.Width, myData.CurrentJunction.Height));

                    if (changingJunctionRect.Contains(currentendPoint)) // 可以创建连接
                    {
                        #region 方法调用关系创建
                        if (myData.Type == JunctionOfConnectionType.Invoke)
                        {
                            var canvasGuid = this.ViewModel.CanvasGuid;

                            await EnvDecorator.ConnectInvokeNodeAsync(
                                        canvasGuid,
                                        myData.StartJunction.MyNode.Guid, 
                                        myData.CurrentJunction.MyNode.Guid,
                                        myData.StartJunction.JunctionType,
                                        myData.CurrentJunction.JunctionType,
                                        myData.ConnectionInvokeType);
                        }
                        #endregion

                        #region 参数来源关系创建
                        else if (myData.Type == JunctionOfConnectionType.Arg)
                        {
                            var argIndex = 0;
                            if (myData.StartJunction is ArgJunctionControl argJunction1)
                            {
                                argIndex = argJunction1.ArgIndex;
                            }
                            else if (myData.CurrentJunction is ArgJunctionControl argJunction2)
                            {
                                argIndex = argJunction2.ArgIndex;
                            }
                            var canvasGuid = this.ViewModel.CanvasGuid;

                            await EnvDecorator.ConnectArgSourceNodeAsync(
                                    canvasGuid,
                                    myData.StartJunction.MyNode.Guid, 
                                    myData.CurrentJunction.MyNode.Guid,
                                    myData.StartJunction.JunctionType,
                                    myData.CurrentJunction.JunctionType,
                                    myData.ConnectionArgSourceType,
                                    argIndex);
                        }
                        #endregion
                    }
                    EndConnection();
                }

            }
            e.Handled = true;

        }

        #region 拖动画布实现缩放平移效果
        private void FlowChartCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            IsCanvasDragging = true;
            startCanvasDragPoint = e.GetPosition(this);
            FlowChartCanvas.CaptureMouse();
            e.Handled = true; // 防止事件传播影响其他控件
        }

        private void FlowChartCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {



            if (IsCanvasDragging)
            {
                IsCanvasDragging = false;
                FlowChartCanvas.ReleaseMouseCapture();
            }
        }

        // 单纯缩放画布，不改变画布大小
        private void FlowChartCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (e.Delta < 0 && scaleTransform.ScaleX < 0.05) return;
                if (e.Delta > 0 && scaleTransform.ScaleY > 2.0) return;
                // 获取鼠标在 Canvas 内的相对位置
                var mousePosition = e.GetPosition(FlowChartCanvas);

                // 缩放因子，根据滚轮方向调整
                //double zoomFactor = e.Delta > 0 ? 0.1 : -0.1;
                double zoomFactor = e.Delta > 0 ? 1.1 : 0.9;

                // 当前缩放比例
                double oldScale = scaleTransform.ScaleX;
                double newScale = oldScale * zoomFactor;
                //double newScale = oldScale + zoomFactor;
                // 更新缩放比例
                scaleTransform.ScaleX = newScale;
                scaleTransform.ScaleY = newScale;

                // 计算缩放前后鼠标相对于 Canvas 的位置差异
                // double offsetX = mousePosition.X - (mousePosition.X * zoomFactor);
                // double offsetY = mousePosition.Y - (mousePosition.Y * zoomFactor);

                // 更新 TranslateTransform，确保以鼠标位置为中心进行缩放
                translateTransform.X -= (mousePosition.X * (newScale - oldScale));
                translateTransform.Y -= (mousePosition.Y * (newScale - oldScale));
            }
        }

        // 设置画布宽度高度
        private void InitializeCanvas(double width, double height)
        {
            FlowChartCanvas.Width = width;
            FlowChartCanvas.Height = height;
        }


        #region 动态调整区域大小
        //private void Thumb_DragDelta_TopLeft(object sender, DragDeltaEventArgs e)
        //{
        //    // 从左上角调整大小
        //    double newWidth = Math.Max(FlowChartCanvas.ActualWidth - e.HorizontalChange, 0);
        //    double newHeight = Math.Max(FlowChartCanvas.ActualHeight - e.VerticalChange, 0);

        //    FlowChartCanvas.Width = newWidth;
        //    FlowChartCanvas.Height = newHeight;

        //    Canvas.SetLeft(FlowChartCanvas, Canvas.GetLeft(FlowChartCanvas) + e.HorizontalChange);
        //    Canvas.SetTop(FlowChartCanvas, Canvas.GetTop(FlowChartCanvas) + e.VerticalChange);
        //}

        //private void Thumb_DragDelta_TopRight(object sender, DragDeltaEventArgs e)
        //{
        //    // 从右上角调整大小
        //    double newWidth = Math.Max(FlowChartCanvas.ActualWidth + e.HorizontalChange, 0);
        //    double newHeight = Math.Max(FlowChartCanvas.ActualHeight - e.VerticalChange, 0);

        //    FlowChartCanvas.Width = newWidth;
        //    FlowChartCanvas.Height = newHeight;

        //    Canvas.SetTop(FlowChartCanvas, Canvas.GetTop(FlowChartCanvas) + e.VerticalChange);
        //}

        //private void Thumb_DragDelta_BottomLeft(object sender, DragDeltaEventArgs e)
        //{
        //    // 从左下角调整大小
        //    double newWidth = Math.Max(FlowChartCanvas.ActualWidth - e.HorizontalChange, 0);
        //    double newHeight = Math.Max(FlowChartCanvas.ActualHeight + e.VerticalChange, 0);

        //    FlowChartCanvas.Width = newWidth;
        //    FlowChartCanvas.Height = newHeight;

        //    Canvas.SetLeft(FlowChartCanvas, Canvas.GetLeft(FlowChartCanvas) + e.HorizontalChange);
        //}

        private void Thumb_DragDelta_BottomRight(object sender, DragDeltaEventArgs e)
        {
            // 获取缩放后的水平和垂直变化
            double horizontalChange = e.HorizontalChange * scaleTransform.ScaleX;
            double verticalChange = e.VerticalChange * scaleTransform.ScaleY;

            // 计算新的宽度和高度，确保不会小于400
            double newWidth = Math.Max(FlowChartCanvas.ActualWidth + horizontalChange, 400);
            double newHeight = Math.Max(FlowChartCanvas.ActualHeight + verticalChange, 400);

            newHeight = newHeight < 400 ? 400 : newHeight;
            newWidth = newWidth < 400 ? 400 : newWidth;

            InitializeCanvas(newWidth, newHeight);

            //// 从右下角调整大小
            //double newWidth = Math.Max(FlowChartCanvas.ActualWidth + e.HorizontalChange * scaleTransform.ScaleX, 0);
            //double newHeight = Math.Max(FlowChartCanvas.ActualHeight + e.VerticalChange * scaleTransform.ScaleY, 0);

            //newWidth = newWidth < 400 ? 400 : newWidth;
            //newHeight = newHeight < 400 ? 400 : newHeight;

            //if (newWidth > 400 && newHeight > 400)
            //{
            //    FlowChartCanvas.Width = newWidth;
            //    FlowChartCanvas.Height = newHeight;

            //    double x = e.HorizontalChange > 0 ? -0.5 : 0.5;
            //    double y = e.VerticalChange > 0 ? -0.5 : 0.5;

            //    double deltaX = x * scaleTransform.ScaleX;
            //    double deltaY = y * scaleTransform.ScaleY;
            //    Test(deltaX, deltaY);
            //}
        }

        //private void Thumb_DragDelta_Left(object sender, DragDeltaEventArgs e)
        //{
        //    // 从左侧调整大小
        //    double newWidth = Math.Max(FlowChartCanvas.ActualWidth - e.HorizontalChange, 0);

        //    FlowChartCanvas.Width = newWidth;
        //    Canvas.SetLeft(FlowChartCanvas, Canvas.GetLeft(FlowChartCanvas) + e.HorizontalChange);
        //}

        private void Thumb_DragDelta_Right(object sender, DragDeltaEventArgs e)
        {
            //从右侧调整大小
            // 获取缩放后的水平变化
            double horizontalChange = e.HorizontalChange * scaleTransform.ScaleX;

            // 计算新的宽度，确保不会小于400
            double newWidth = Math.Max(FlowChartCanvas.ActualWidth + horizontalChange, 400);

            newWidth = newWidth < 400 ? 400 : newWidth;
            InitializeCanvas(newWidth, FlowChartCanvas.Height);

        }

        //private void Thumb_DragDelta_Top(object sender, DragDeltaEventArgs e)
        //{
        //    // 从顶部调整大小
        //    double newHeight = Math.Max(FlowChartCanvas.ActualHeight - e.VerticalChange, 0);

        //    FlowChartCanvas.Height = newHeight;
        //    Canvas.SetTop(FlowChartCanvas, Canvas.GetTop(FlowChartCanvas) + e.VerticalChange);
        //}

        private void Thumb_DragDelta_Bottom(object sender, DragDeltaEventArgs e)
        {
            // 获取缩放后的垂直变化
            double verticalChange = e.VerticalChange * scaleTransform.ScaleY;
            // 计算新的高度，确保不会小于400
            double newHeight = Math.Max(FlowChartCanvas.ActualHeight + verticalChange, 400);
            newHeight = newHeight < 400 ? 400 : newHeight;
            InitializeCanvas(FlowChartCanvas.Width, newHeight);
        }


        private void Test(double deltaX, double deltaY)
        {
            //Console.WriteLine((translateTransform.X, translateTransform.Y));
            //translateTransform.X += deltaX;
            //translateTransform.Y += deltaY;
        }

        #endregion
        #endregion


        /// 完成选取操作
        /// </summary>
        private void CompleteSelection()
        {
            IsSelectControl = false;

            // 隐藏选取矩形
            SelectionRectangle.Visibility = Visibility.Collapsed;

            // 获取选取范围
            Rect selectionArea = new Rect(Canvas.GetLeft(SelectionRectangle),
                                          Canvas.GetTop(SelectionRectangle),
                                          SelectionRectangle.Width,
                                          SelectionRectangle.Height);

            // 处理选取范围内的控件
            // selectNodeControls.Clear();
            foreach (UIElement element in FlowChartCanvas.Children)
            {
                Rect elementBounds = new Rect(Canvas.GetLeft(element), Canvas.GetTop(element),
                                              element.RenderSize.Width, element.RenderSize.Height);

                if (selectionArea.Contains(elementBounds))
                {
                    if (element is NodeControlBase control)
                    {
                        if (!selectNodeControls.Contains(control))
                        {
                            selectNodeControls.Add(control);
                        }
                    }
                }
            }

            // 选中后的操作
            SelectedNode();
        }

        /// <summary>
        /// 选择控件
        /// </summary>
        private void SelectedNode()
        {

            if (selectNodeControls.Count == 0)
            {
                //Console.WriteLine($"没有选择控件");
                SelectionRectangle.Visibility = Visibility.Collapsed;
                return;
            }
            if (selectNodeControls.Count == 1)
            {
                // ChangeViewerObjOfNode(selectNodeControls[0]);
            }

            //Console.WriteLine($"一共选取了{selectNodeControls.Count}个控件");
            foreach (var node in selectNodeControls)
            {
                //node.ViewModel.IsSelect =true;
                // node.ViewModel.CancelSelect();
                node.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC700"));
                node.BorderThickness = new Thickness(4);
            }
        }

        /// <summary>
        /// 结束连接操作，清理状态并移除虚线。
        /// </summary>
        private void EndConnection()
        {
            Mouse.OverrideCursor = null; // 恢复视觉效果
            ViewModel.IsConnectionArgSourceNode = false;
            ViewModel.IsConnectionInvokeNode = false;
            GlobalJunctionData.OK();
        }

        /// <summary>
        /// 创建菜单子项
        /// </summary>
        /// <param name="header"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
        public static MenuItem CreateMenuItem(string header, RoutedEventHandler handler)
        {
            var menuItem = new MenuItem { Header = header };
            menuItem.Click += handler;
            return menuItem;
        }


        /// <summary>
        /// 选择范围配置
        /// </summary>
        /// <returns></returns>
        private ContextMenu ConfiguerSelectionRectangle()
        {
            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(CreateMenuItem("删除", (s, e) =>
            {
                if (selectNodeControls.Count > 0)
                {
                    foreach (var node in selectNodeControls.ToArray())
                    {
                        var guid = node?.ViewModel?.NodeModel?.Guid;
                        if (!string.IsNullOrEmpty(guid))
                        {
                            var canvasGuid = this.ViewModel.CanvasGuid;
                            EnvDecorator.RemoveNodeAsync(canvasGuid, guid);
                        }
                    }
                }
                SelectionRectangle.Visibility = Visibility.Collapsed;
            }));
            return contextMenu;
            // nodeControl.ContextMenu = contextMenu;
        }



    }
}
