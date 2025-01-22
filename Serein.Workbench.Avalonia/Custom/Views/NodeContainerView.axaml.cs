using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.PanAndZoom;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Newtonsoft.Json.Linq;
using Serein.Library;
using Serein.Library.Utils;
using Serein.Workbench.Avalonia.Api;
using Serein.Workbench.Avalonia.Custom.Node.Views;
using Serein.Workbench.Avalonia.Custom.ViewModels;
using Serein.Workbench.Avalonia.Extension;
using Serein.Workbench.Avalonia.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using Point = Avalonia.Point;

namespace Serein.Workbench.Avalonia.Custom.Views;

public partial class NodeContainerView : UserControl
{
    private readonly NodeContainerViewModel _vm;
    private readonly INodeOperationService nodeOperationService;
    private readonly IKeyEventService keyEventService;

    #region 与画布相关的字段
    /// <summary>
    /// 是否正在预览节点控件
    /// </summary>
    private bool IsPreviewNodeControl;
    /// <summary>
    /// 标记是否正在尝试选取控件
    /// </summary>
    private bool IsSelectControl;
    /// <summary>
    /// 标记是否正在拖动控件
    /// </summary>
    private bool IsControlDragging;
    /// <summary>
    /// 标记是否正在拖动画布
    /// </summary>
    private bool IsCanvasDragging;
    /// <summary>
    /// 标记是否正在选取节点
    /// </summary>
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


    public NodeContainerView()
    {
        InitializeComponent();
        _vm= App.GetService<NodeContainerViewModel>();
        DataContext = _vm;

        #region 获取UI相关的服务
        keyEventService = App.GetService<IKeyEventService>();
        nodeOperationService = App.GetService<INodeOperationService>();
        nodeOperationService.MainCanvas = PART_NodeContainer;
        nodeOperationService.OnNodeViewCreate += NodeOperationService_OnNodeViewCreate; // 处理事件
        keyEventService.KeyUp += (k) =>
        {
            if (k == Key.Escape)
            {
                IsCanvasDragging = false;
                IsControlDragging = false;
                nodeOperationService.ConnectingManage.Reset();
            }
        };
        #endregion

        #region 设置UI事件
        AddHandler(DragDrop.DropEvent, Drop); // 创建节点相关
        

        PointerPressed += NodeContainerView_PointerPressed; 
        PointerReleased += NodeContainerView_PointerReleased; 
        PointerMoved += NodeContainerView_PointerMoved;
        PointerWheelChanged += NodeContainerView_PointerWheelChanged;
        #endregion

        #region 初始化画布动画容器
        canvasTransformGroup = new TransformGroup();
        scaleTransform = new ScaleTransform();
        translateTransform = new TranslateTransform();
        canvasTransformGroup.Children.Add(scaleTransform);
        canvasTransformGroup.Children.Add(translateTransform);
        PART_NodeContainer.RenderTransform = canvasTransformGroup;
        #endregion 
    }

    #region 工具方法

    public Point GetPositionOfCanvas(PointerEventArgs e)
    {
        return e.GetPosition(PART_NodeContainer);
    }
    public Point GetPositionOfCanvas(DragEventArgs e)
    {
        return e.GetPosition(PART_NodeContainer);
    }

    #endregion

    #region 画布的移动、缩放、框选，以及拖拽事件

    #region 响应拖拽事件
    private void Drop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Text))
        {
            var json = e.Data.GetText();
            if (string.IsNullOrEmpty(json))
            {
                return;
            }
            var mdInfo = json.ToJsonObject<MethodDetailsInfo>();
            if (mdInfo is not null)
            {
                var canvasDropPosition = GetPositionOfCanvas(e); // 更新画布落点
                PositionOfUI position = new PositionOfUI(canvasDropPosition.X, canvasDropPosition.Y);
                nodeOperationService.CreateNodeView(mdInfo, position); // 提交创建节点的请求
            }

        }
        else // if (e.Data.Contains(DataFormats.FileNames))
        {
            var files = e.Data.GetFiles();
            var str = files?.Select(f => f.Path);
            if (str is not null)
            {
            }
        }
    } 
    #endregion

    #region 拖动画布
    private void NodeContainerView_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsPreviewNodeControl)
        {
            IsCanvasDragging = false;
            e.Handled = true;
            return;
        }
        if (!IsCanvasDragging)
        {
            IsCanvasDragging = true;
            startCanvasDragPoint = e.GetPosition(this);
            e.Handled = true;
        }
    }
    private void NodeContainerView_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        IsCanvasDragging = false; // 不再拖动
    }

    private void NodeContainerView_PointerMoved(object? sender, PointerEventArgs e)
    {
        // 是否正在连接
        var myData = nodeOperationService.ConnectingManage;
        if (myData.IsCreateing)
        {
            var isPass = e.JudgePointer(sender, PointerType.Mouse, p => p.IsLeftButtonPressed);
            if (isPass)
            {
                if (myData.Type == JunctionOfConnectionType.Invoke)
                {
                    _vm.IsConnectionInvokeNode = true; // 正在连接节点的调用关系

                }
                else
                {
                    _vm.IsConnectionArgSourceNode = true; // 正在连接节点的调用关系
                }
                var currentPoint = e.GetPosition(PART_NodeContainer);
                //myData.CurrentJunction?.InvalidateVisual();
                myData.UpdatePoint(new Point(currentPoint.X - 5, currentPoint.Y - 5));
                e.Handled = true;
                return;

            }
            
           
        }


     

        if (IsCanvasDragging)
        {
            // 拖动画布
            Point currentMousePosition = e.GetPosition(this);
            double deltaX = currentMousePosition.X - startCanvasDragPoint.X;
            double deltaY = currentMousePosition.Y - startCanvasDragPoint.Y;
            translateTransform.X += deltaX;
            translateTransform.Y += deltaY;
            startCanvasDragPoint = currentMousePosition;
        }
    }

    // 缩放
    private void NodeContainerView_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var delta = e.Delta.Y;
        if (delta < 0 && scaleTransform.ScaleX < 0.02) return;
        if (delta > 0 && scaleTransform.ScaleY > 4.0) return;

        // 缩放因子，根据滚轮方向调整
        double zoomFactor = delta > 0 ? 1.23 : 0.78;

        // 当前缩放比例
        double oldScale = scaleTransform.ScaleX;
        double newScale = oldScale * zoomFactor;

        // 记录缩放前的鼠标位置
        var mousePosition = GetPositionOfCanvas(e);

        // 更新缩放比例
        scaleTransform.ScaleX = newScale;
        scaleTransform.ScaleY = newScale;

        // 记录缩放后的鼠标位置
        var newMousePosition = GetPositionOfCanvas(e);

        // 更新 TranslateTransform，确保以鼠标位置为中心进行缩放
        var s_position = newMousePosition - mousePosition; // 计算偏移量
        translateTransform.X += s_position.X * newScale; // 根据缩放比例进行偏移
        translateTransform.Y += s_position.Y * newScale; // 根据缩放比例进行偏移

    }

    #endregion

    #endregion

    #region 节点事件处理相关方法
    /// <summary>
    /// 拖拽创建控件
    /// </summary>
    /// <param name="eventArgs"></param>
    /// <returns></returns>
    private bool NodeOperationService_OnNodeViewCreate(NodeViewCreateEventArgs eventArgs)
    {
        if (eventArgs.NodeControl is not Control control)
        {
            return false;
        }
        var position = eventArgs.Position;// 坐标
        SetNodeEvent(control); // 设置该控件与画布交互的相关事件

        DragControl(control, position.X, position.Y);
        PART_NodeContainer.Children.Add(control);
        return true;
    }

    /// <summary>
    /// 设置节点与画布容器相关的操作事件
    /// </summary>
    /// <param name="nodeControl"></param>
    private void SetNodeEvent(Control nodeControl)
    {
        nodeControl.PointerMoved += NodeControl_PointerMoved; ;
        nodeControl.PointerExited += NodeControl_PointerExited;
        nodeControl.PointerPressed += Block_MouseLeftButtonDown;
        nodeControl.PointerMoved += Block_MouseMove;
        nodeControl.PointerReleased += (s, e) => IsControlDragging = false;
    }

    #endregion

    #region 控件交互的相关方法

    /// <summary>
    /// 移动控件
    /// </summary>
    /// <param name="nodeControl"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    private void DragControl(Control nodeControl, double x, double y)
    {
        Canvas.SetLeft(nodeControl, x);
        Canvas.SetTop(nodeControl, y);
    }

    /// <summary>
    /// 控件的鼠标右键按下事件，启动拖动操作。
    /// </summary>
    private void Block_MouseLeftButtonDown(object? sender, PointerPressedEventArgs e)
    {
        var isPass =  e.JudgePointer(sender, PointerType.Mouse, p => p.IsRightButtonPressed);
        if (!isPass)
        {
            return;
        }

        if (sender is NodeControlBase nodeControl)
        {
            IsControlDragging = true;
            startControlDragPoint = GetPositionOfCanvas(e); // 记录鼠标按下时的位置
            
            e.Handled = true; // 防止事件传播影响其他控件
        }

    }

    /// <summary>
    /// 控件的鼠标移动事件，根据鼠标拖动更新控件的位置。批量移动计算移动逻辑。
    /// </summary>
    private void Block_MouseMove(object? sender, PointerEventArgs e)
    {

        if (sender is not NodeControlBase nodeControl)
        {
            return;
        }

        if (IsCanvasDragging)
            return;
        if (IsSelectControl)
            return;

        if (IsControlDragging) // 如果正在拖动控件
        {
            Point currentPosition = GetPositionOfCanvas(e); // 获取当前鼠标位置 

            // 单个移动
            if (selectNodeControls.Count == 0 || !selectNodeControls.Contains(nodeControl))
            {
                double deltaX = currentPosition.X - startControlDragPoint.X; // 计算X轴方向的偏移量
                double deltaY = currentPosition.Y - startControlDragPoint.Y; // 计算Y轴方向的偏移量
                double newLeft = Canvas.GetLeft(nodeControl) + deltaX; // 新的左边距
                double newTop = Canvas.GetTop(nodeControl) + deltaY; // 新的上边距
                DragControl(nodeControl, newLeft, newTop);
                nodeControl.UpdateLocationConnections();
            }
            // 批量移动
            else
            {
                // 进行批量移动
                // 获取旧位置
                var oldLeft = Canvas.GetLeft(nodeControl);
                var oldTop = Canvas.GetTop(nodeControl);

                // 计算被选择控件的偏移量
                var deltaX = /*(int)*/(currentPosition.X - startControlDragPoint.X);
                var deltaY = /*(int)*/(currentPosition.Y - startControlDragPoint.Y);

                // 移动被选择的控件
                var newLeft = oldLeft + deltaX;
                var newTop = oldTop + deltaY;

                //this.EnvDecorator.MoveNode(nodeControlMain.ViewModel.NodeModel.Guid, newLeft, newTop); // 移动节点
                DragControl(nodeControl, newLeft, newTop);
                // 计算控件实际移动的距离
                var actualDeltaX = newLeft - oldLeft;
                var actualDeltaY = newTop - oldTop;

                // 移动其它选中的控件
                foreach (var selectItemNode in selectNodeControls)
                {
                    if (selectItemNode != nodeControl) // 跳过已经移动的控件
                    {
                        var otherNewLeft = Canvas.GetLeft(selectItemNode) + actualDeltaX;
                        var otherNewTop = Canvas.GetTop(selectItemNode) + actualDeltaY;
                        DragControl(selectItemNode, otherNewLeft, otherNewTop);
                        //this.EnvDecorator.MoveNode(nodeControl.ViewModel.NodeModel.Guid, otherNewLeft, otherNewTop); // 移动节点
                    }
                }

                // 更新节点之间线的连接位置
               foreach (var item in selectNodeControls)
               {
                    item.UpdateLocationConnections();
               }
            }
            startControlDragPoint = currentPosition; // 更新起始点位置
        } 
    }


    private void NodeControl_PointerExited(object? sender, PointerEventArgs e)
    {
        IsPreviewNodeControl = false;
    }

    private void NodeControl_PointerMoved(object? sender, PointerEventArgs e)
    {
        IsPreviewNodeControl = true;
    }
    #endregion





}