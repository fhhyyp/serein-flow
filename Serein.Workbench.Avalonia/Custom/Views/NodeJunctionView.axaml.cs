using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Serein.Library;
using Serein.Library.Api;
using Serein.Workbench.Avalonia.Api;
using Serein.Workbench.Avalonia.Extension;
using System;
using Color = Avalonia.Media.Color;
using Point = Avalonia.Point;

namespace Serein.Workbench.Avalonia.Custom.Views;

/// <summary>
/// ���ӿ��Ƶ�
/// </summary>
public class NodeJunctionView : TemplatedControl
{

    public static readonly DirectProperty<NodeJunctionView, JunctionType> JunctionTypeProperty =
        AvaloniaProperty.RegisterDirect<NodeJunctionView, JunctionType>(nameof(JunctionType), o => o.JunctionType, (o, v) => o.JunctionType = v);
    private JunctionType junctionType;
    public JunctionType JunctionType
    {
        get { return junctionType; }
        set { SetAndRaise(JunctionTypeProperty, ref junctionType, value); }
    }

    public static readonly DirectProperty<NodeJunctionView, NodeModelBase?> MyNodeProperty =
        AvaloniaProperty.RegisterDirect<NodeJunctionView, NodeModelBase?>(nameof(MyNode), o => o.MyNode, (o, v) => o.MyNode = v);
    private NodeModelBase? myNode;
    public NodeModelBase? MyNode
    {
        get { return myNode; }
        set { SetAndRaise(MyNodeProperty, ref myNode, value); }
    }
    
    public static readonly DirectProperty<NodeJunctionView, int> ArgIndexProperty =
        AvaloniaProperty.RegisterDirect<NodeJunctionView, int>(nameof(ArgIndex), o => o.ArgIndex, (o, v) => o.ArgIndex = v);
    private int argIndex;
    public int ArgIndex
    {
        get { return argIndex; }
        set { SetAndRaise(ArgIndexProperty, ref argIndex, value); }
    }



    private readonly INodeOperationService nodeOperationService;
    private readonly IFlowEnvironment flowEnvironment;

    /// <summary>
    /// Render�����п����Ի�����
    /// </summary>
    protected readonly StreamGeometry StreamGeometry = new StreamGeometry();



    #region ��������¼�


    public NodeJunctionView()
    {
        nodeOperationService = App.GetService<INodeOperationService>();
        flowEnvironment = App.GetService<IFlowEnvironment>();
        //this.PointerExited += NodeJunctionView_PointerExited;
        this.PointerMoved += NodeJunctionView_PointerMoved; 
        this.PointerPressed += NodeJunctionView_PointerPressed;
        this.PointerReleased += NodeJunctionView_PointerReleased;
    }
    

    public bool IsPreviewing { get; set; }
    private Guid Guid = Guid.NewGuid();

    private void NodeJunctionView_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!nodeOperationService.ConnectingData.IsCreateing)
            return;
        if (nodeOperationService.MainCanvas is not InputElement inputElement)
            return;
        var currentPoint = e.GetPosition(nodeOperationService.MainCanvas);
        if (inputElement.InputHitTest(currentPoint) is NodeJunctionView junctionView)
        {
            RefreshDisplay(junctionView);
        }
        else
        {
            var oldNj = nodeOperationService.ConnectingData.CurrentJunction;
            if (oldNj is not null)
            {
                oldNj.IsPreviewing = false;
                oldNj.InvalidateVisual();
            }
        }
    }

    private void RefreshDisplay(NodeJunctionView junctionView)
    {
        var oldNj = nodeOperationService.ConnectingData.CurrentJunction;
        if (oldNj is not null )
        {
            if (junctionView.Equals(oldNj))
            {
                return;
            }
            oldNj.IsPreviewing = false;
            oldNj.InvalidateVisual();
        }
        nodeOperationService.ConnectingData.CurrentJunction = junctionView;
        if (!this.Equals(junctionView))
        {

            nodeOperationService.ConnectingData.TempLine?.ToEnd(junctionView);
        }
        junctionView.IsPreviewing = true;
        junctionView.InvalidateVisual();
    }



    /// <summary>
    /// ���Կ�ʼ����������
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void NodeJunctionView_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        nodeOperationService.TryCreateConnectionOnJunction(this); // ���Կ�ʼ����
    }
    private void NodeJunctionView_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        CheckJunvtion();
        nodeOperationService.ConnectingData.Reset();
    }

    private void CheckJunvtion()
    {
        var myData = nodeOperationService.ConnectingData;
        if(myData.StartJunction is null || myData.CurrentJunction is null)
        {
            return;
        }
        if(myData.StartJunction.MyNode is null || myData.CurrentJunction.MyNode is null)
        {
            return;
        }
        if (!myData.IsCanConnected())
        {
            return;
        }

        var canvas = nodeOperationService.MainCanvas;

        #region �������ù�ϵ����
        if (myData.Type == JunctionOfConnectionType.Invoke)
        {
            flowEnvironment.ConnectInvokeNodeAsync(myData.StartJunction.MyNode.Guid, myData.CurrentJunction.MyNode.Guid,
                        myData.StartJunction.JunctionType,
                        myData.CurrentJunction.JunctionType,
                        myData.ConnectionInvokeType);
        }
        #endregion

        #region ������Դ��ϵ����
        else if (myData.Type == JunctionOfConnectionType.Arg)
        {
            var argIndex = 0;
            if (myData.StartJunction.JunctionType == JunctionType.ArgData)
            {
                argIndex = myData.StartJunction.ArgIndex;
            }
            else if (myData.CurrentJunction.JunctionType == JunctionType.ArgData)
            {
                argIndex = myData.CurrentJunction.ArgIndex;
            }

            flowEnvironment.ConnectArgSourceNodeAsync(myData.StartJunction.MyNode.Guid, myData.CurrentJunction.MyNode.Guid,
                    myData.StartJunction.JunctionType,
                    myData.CurrentJunction.JunctionType,
                    myData.ConnectionArgSourceType,
                    argIndex);
        }
        #endregion

       
    }

    #endregion


    #region �ػ�UI�Ӿ�

    /// <summary>
    /// �ؼ��ػ��¼�
    /// </summary>
    /// <param name="drawingContext"></param>

    public override void Render(DrawingContext drawingContext)
    {
        double width = 44;
        double height = 26;
        var background = GetBackgrounp();
        var pen = new Pen(Brushes.Transparent, 1);
        //var pen = nodeOperationService.ConnectingData.IsCreateing ? new Pen(background, 1) : new Pen(Brushes.Black, 1);

        // �����������ı���
        var connectorRect = new Rect(0, 0, width, height);
        drawingContext.DrawRectangle(Brushes.Transparent, new Pen(), connectorRect);


        double circleCenterX = width / 2 ; // ���� X ����
        double circleCenterY = height / 2 ; // ���� Y ����
        //_myCenterPoint = new Point(circleCenterX - Width / 2, circleCenterY); // ��������

        // ����Բ�εĴ�С��λ��
        var diameterCircle = width - 20;
        Rect rect = new(4, 2, diameterCircle / 2, diameterCircle / 2);
        var ellipse = new EllipseGeometry(rect);
        drawingContext.DrawGeometry(background, pen, ellipse);

        // ���������εļ��
        double triangleCenterX = width / 2  - 2; // ���������� X ����
        double triangleCenterY = height / 2  -5; // ���������� Y ����

        // ����������
        var pathGeometry = new StreamGeometry();
        using (var context = pathGeometry.Open())
        {
            int t = 6;
            context.BeginFigure(new Point(triangleCenterX, triangleCenterY - t), true);
            context.LineTo(new Point(triangleCenterX + 8, triangleCenterY), true);
            context.LineTo(new Point(triangleCenterX, triangleCenterY + t), true);
            context.LineTo(new Point(triangleCenterX, triangleCenterY - t), true);
        }
        drawingContext.DrawGeometry(background, pen, pathGeometry);

    }



    /// <summary>
    /// ��ȡ������ɫ
    /// </summary>
    /// <returns></returns>
    protected IBrush GetBackgrounp()
    {
        var myData = nodeOperationService.ConnectingData;
        if (IsPreviewing == false || !myData.IsCreateing )
        {
            return new SolidColorBrush(Color.Parse("#76ABEE"));
        }

        if (!myData.IsCanConnected())
        {
            return new SolidColorBrush(Color.Parse("#FF0000"));
        }

        if (myData.Type == JunctionOfConnectionType.Invoke)
        {
            return myData.ConnectionInvokeType.ToLineColor(); // ����
        }
        else
        {
            return myData.ConnectionArgSourceType.ToLineColor(); // ����
        }

    }
    #endregion




}







