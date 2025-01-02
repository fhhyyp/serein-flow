using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Serein.Library;
using Serein.Workbench.Avalonia.Views;
using System.Drawing;
using System;
using Color = Avalonia.Media.Color;
using Point = Avalonia.Point;
using System.Diagnostics;
using Avalonia.Threading;
using Serein.Workbench.Avalonia.Api;
using Serein.Workbench.Avalonia.Extension;

namespace Serein.Workbench.Avalonia.Custom.Views;

/// <summary>
/// ���ӿ��Ƶ�
/// </summary>
public class NodeJunctionView : TemplatedControl
{
    private readonly INodeOperationService nodeOperationService;

    /// <summary>
    /// Render�����п����Ի�����
    /// </summary>
    protected readonly StreamGeometry StreamGeometry = new StreamGeometry();

    /// <summary>
    /// ���ڲ鿴
    /// </summary>
    private bool IsPreviewing;

    public NodeJunctionView()
    {
        nodeOperationService = App.GetService<INodeOperationService>();
        this.PointerMoved += NodeJunctionView_PointerMoved;
        this.PointerExited += NodeJunctionView_PointerExited;

        this.PointerPressed += NodeJunctionView_PointerPressed;
        this.PointerReleased += NodeJunctionView_PointerReleased;
    }

    private void NodeJunctionView_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        nodeOperationService.ConnectingData.IsCreateing = false;
    }

    private void NodeJunctionView_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        nodeOperationService.TryCreateConnectionOnJunction(this); // ���Կ�ʼ����
        Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
    }



    /// <summary>
    /// ��ȡ���ؼ���Ϣ
    /// </summary>
    /// <param name="e"></param>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        //if (e.NameScope.Find("PART_FlipflopMethodInfos") is ListBox p_fm)
        //{
        //    //p_fm.SelectionChanged += ListBox_SelectionChanged;
        //    //p_fm.PointerExited += ListBox_PointerExited;
        //}
    }


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
        var pen = new Pen(Brushes.Black, 1);

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
        drawingContext.DrawGeometry(background, new Pen(Brushes.Black, 1), pathGeometry);

    }

    #region ��������¼�

    private void NodeJunctionView_PointerExited(object? sender, PointerEventArgs e)
    {
        if (IsPreviewing)
        {
            IsPreviewing = false;
            Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
        }
    }

    private void NodeJunctionView_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!IsPreviewing)
        {
            IsPreviewing = true;
            Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
        }
        
    }

    #endregion


    /// <summary>
    /// ��ȡ������ɫ
    /// </summary>
    /// <returns></returns>
    protected IBrush GetBackgrounp()
    {
        var myData = nodeOperationService.ConnectingData;
        if (!myData.IsCreateing)
        {
            //Debug.WriteLine($"return color is {Brushes.BurlyWood}");
            return new SolidColorBrush(Color.Parse("#76ABEE"));
        }

        if (myData.IsCanConnected)
        {
            if (myData.Type == JunctionOfConnectionType.Invoke)
            {
                return myData.ConnectionInvokeType.ToLineColor();
            }
            else
            {
                return myData.ConnectionArgSourceType.ToLineColor();
            }
        }
        else
        {
            return Brushes.Red;
        }

        if (IsPreviewing)
        {
            //return new SolidColorBrush(Color.Parse("#04FC10"));
           
        }
        else
        {
            //Debug.WriteLine($"return color is {Brushes.BurlyWood}");
            return new SolidColorBrush(Color.Parse("#76ABEE"));
        }
    }
    #endregion




}






