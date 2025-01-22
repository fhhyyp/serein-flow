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

    #region �뻭����ص��ֶ�
    /// <summary>
    /// �Ƿ�����Ԥ���ڵ�ؼ�
    /// </summary>
    private bool IsPreviewNodeControl;
    /// <summary>
    /// ����Ƿ����ڳ���ѡȡ�ؼ�
    /// </summary>
    private bool IsSelectControl;
    /// <summary>
    /// ����Ƿ������϶��ؼ�
    /// </summary>
    private bool IsControlDragging;
    /// <summary>
    /// ����Ƿ������϶�����
    /// </summary>
    private bool IsCanvasDragging;
    /// <summary>
    /// ����Ƿ�����ѡȡ�ڵ�
    /// </summary>
    private bool IsSelectDragging;
    /// <summary>
    /// ��ǰѡȡ�Ŀؼ�
    /// </summary>
    private readonly List<NodeControlBase> selectNodeControls = [];

    /// <summary>
    /// ��¼��ʼ�϶��ڵ�ؼ�ʱ�����λ��
    /// </summary>
    private Point startControlDragPoint;
    /// <summary>
    /// ��¼�ƶ�������ʼʱ�����λ��
    /// </summary>
    private Point startCanvasDragPoint;
    /// <summary>
    /// ��¼��ʼѡȡ�ڵ�ؼ�ʱ�����λ��
    /// </summary>
    private Point startSelectControolPoint;

    /// <summary>
    /// ��ϱ任����
    /// </summary>
    private readonly TransformGroup canvasTransformGroup;
    /// <summary>
    /// ���Ż���
    /// </summary>
    private readonly ScaleTransform scaleTransform;
    /// <summary>
    /// ƽ�ƻ��� 
    /// </summary>
    private readonly TranslateTransform translateTransform;
    #endregion


    public NodeContainerView()
    {
        InitializeComponent();
        _vm= App.GetService<NodeContainerViewModel>();
        DataContext = _vm;

        #region ��ȡUI��صķ���
        keyEventService = App.GetService<IKeyEventService>();
        nodeOperationService = App.GetService<INodeOperationService>();
        nodeOperationService.MainCanvas = PART_NodeContainer;
        nodeOperationService.OnNodeViewCreate += NodeOperationService_OnNodeViewCreate; // �����¼�
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

        #region ����UI�¼�
        AddHandler(DragDrop.DropEvent, Drop); // �����ڵ����
        

        PointerPressed += NodeContainerView_PointerPressed; 
        PointerReleased += NodeContainerView_PointerReleased; 
        PointerMoved += NodeContainerView_PointerMoved;
        PointerWheelChanged += NodeContainerView_PointerWheelChanged;
        #endregion

        #region ��ʼ��������������
        canvasTransformGroup = new TransformGroup();
        scaleTransform = new ScaleTransform();
        translateTransform = new TranslateTransform();
        canvasTransformGroup.Children.Add(scaleTransform);
        canvasTransformGroup.Children.Add(translateTransform);
        PART_NodeContainer.RenderTransform = canvasTransformGroup;
        #endregion 
    }

    #region ���߷���

    public Point GetPositionOfCanvas(PointerEventArgs e)
    {
        return e.GetPosition(PART_NodeContainer);
    }
    public Point GetPositionOfCanvas(DragEventArgs e)
    {
        return e.GetPosition(PART_NodeContainer);
    }

    #endregion

    #region �������ƶ������š���ѡ���Լ���ק�¼�

    #region ��Ӧ��ק�¼�
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
                var canvasDropPosition = GetPositionOfCanvas(e); // ���»������
                PositionOfUI position = new PositionOfUI(canvasDropPosition.X, canvasDropPosition.Y);
                nodeOperationService.CreateNodeView(mdInfo, position); // �ύ�����ڵ������
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

    #region �϶�����
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
        IsCanvasDragging = false; // �����϶�
    }

    private void NodeContainerView_PointerMoved(object? sender, PointerEventArgs e)
    {
        // �Ƿ���������
        var myData = nodeOperationService.ConnectingManage;
        if (myData.IsCreateing)
        {
            var isPass = e.JudgePointer(sender, PointerType.Mouse, p => p.IsLeftButtonPressed);
            if (isPass)
            {
                if (myData.Type == JunctionOfConnectionType.Invoke)
                {
                    _vm.IsConnectionInvokeNode = true; // �������ӽڵ�ĵ��ù�ϵ

                }
                else
                {
                    _vm.IsConnectionArgSourceNode = true; // �������ӽڵ�ĵ��ù�ϵ
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
            // �϶�����
            Point currentMousePosition = e.GetPosition(this);
            double deltaX = currentMousePosition.X - startCanvasDragPoint.X;
            double deltaY = currentMousePosition.Y - startCanvasDragPoint.Y;
            translateTransform.X += deltaX;
            translateTransform.Y += deltaY;
            startCanvasDragPoint = currentMousePosition;
        }
    }

    // ����
    private void NodeContainerView_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var delta = e.Delta.Y;
        if (delta < 0 && scaleTransform.ScaleX < 0.02) return;
        if (delta > 0 && scaleTransform.ScaleY > 4.0) return;

        // �������ӣ����ݹ��ַ������
        double zoomFactor = delta > 0 ? 1.23 : 0.78;

        // ��ǰ���ű���
        double oldScale = scaleTransform.ScaleX;
        double newScale = oldScale * zoomFactor;

        // ��¼����ǰ�����λ��
        var mousePosition = GetPositionOfCanvas(e);

        // �������ű���
        scaleTransform.ScaleX = newScale;
        scaleTransform.ScaleY = newScale;

        // ��¼���ź�����λ��
        var newMousePosition = GetPositionOfCanvas(e);

        // ���� TranslateTransform��ȷ�������λ��Ϊ���Ľ�������
        var s_position = newMousePosition - mousePosition; // ����ƫ����
        translateTransform.X += s_position.X * newScale; // �������ű�������ƫ��
        translateTransform.Y += s_position.Y * newScale; // �������ű�������ƫ��

    }

    #endregion

    #endregion

    #region �ڵ��¼�������ط���
    /// <summary>
    /// ��ק�����ؼ�
    /// </summary>
    /// <param name="eventArgs"></param>
    /// <returns></returns>
    private bool NodeOperationService_OnNodeViewCreate(NodeViewCreateEventArgs eventArgs)
    {
        if (eventArgs.NodeControl is not Control control)
        {
            return false;
        }
        var position = eventArgs.Position;// ����
        SetNodeEvent(control); // ���øÿؼ��뻭������������¼�

        DragControl(control, position.X, position.Y);
        PART_NodeContainer.Children.Add(control);
        return true;
    }

    /// <summary>
    /// ���ýڵ��뻭��������صĲ����¼�
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

    #region �ؼ���������ط���

    /// <summary>
    /// �ƶ��ؼ�
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
    /// �ؼ�������Ҽ������¼��������϶�������
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
            startControlDragPoint = GetPositionOfCanvas(e); // ��¼��갴��ʱ��λ��
            
            e.Handled = true; // ��ֹ�¼�����Ӱ�������ؼ�
        }

    }

    /// <summary>
    /// �ؼ�������ƶ��¼�����������϶����¿ؼ���λ�á������ƶ������ƶ��߼���
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

        if (IsControlDragging) // ��������϶��ؼ�
        {
            Point currentPosition = GetPositionOfCanvas(e); // ��ȡ��ǰ���λ�� 

            // �����ƶ�
            if (selectNodeControls.Count == 0 || !selectNodeControls.Contains(nodeControl))
            {
                double deltaX = currentPosition.X - startControlDragPoint.X; // ����X�᷽���ƫ����
                double deltaY = currentPosition.Y - startControlDragPoint.Y; // ����Y�᷽���ƫ����
                double newLeft = Canvas.GetLeft(nodeControl) + deltaX; // �µ���߾�
                double newTop = Canvas.GetTop(nodeControl) + deltaY; // �µ��ϱ߾�
                DragControl(nodeControl, newLeft, newTop);
                nodeControl.UpdateLocationConnections();
            }
            // �����ƶ�
            else
            {
                // ���������ƶ�
                // ��ȡ��λ��
                var oldLeft = Canvas.GetLeft(nodeControl);
                var oldTop = Canvas.GetTop(nodeControl);

                // ���㱻ѡ��ؼ���ƫ����
                var deltaX = /*(int)*/(currentPosition.X - startControlDragPoint.X);
                var deltaY = /*(int)*/(currentPosition.Y - startControlDragPoint.Y);

                // �ƶ���ѡ��Ŀؼ�
                var newLeft = oldLeft + deltaX;
                var newTop = oldTop + deltaY;

                //this.EnvDecorator.MoveNode(nodeControlMain.ViewModel.NodeModel.Guid, newLeft, newTop); // �ƶ��ڵ�
                DragControl(nodeControl, newLeft, newTop);
                // ����ؼ�ʵ���ƶ��ľ���
                var actualDeltaX = newLeft - oldLeft;
                var actualDeltaY = newTop - oldTop;

                // �ƶ�����ѡ�еĿؼ�
                foreach (var selectItemNode in selectNodeControls)
                {
                    if (selectItemNode != nodeControl) // �����Ѿ��ƶ��Ŀؼ�
                    {
                        var otherNewLeft = Canvas.GetLeft(selectItemNode) + actualDeltaX;
                        var otherNewTop = Canvas.GetTop(selectItemNode) + actualDeltaY;
                        DragControl(selectItemNode, otherNewLeft, otherNewTop);
                        //this.EnvDecorator.MoveNode(nodeControl.ViewModel.NodeModel.Guid, otherNewLeft, otherNewTop); // �ƶ��ڵ�
                    }
                }

                // ���½ڵ�֮���ߵ�����λ��
               foreach (var item in selectNodeControls)
               {
                    item.UpdateLocationConnections();
               }
            }
            startControlDragPoint = currentPosition; // ������ʼ��λ��
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