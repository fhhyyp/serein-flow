using Serein.Library;
using Serein.Library.Api;
using Serein.Workbench.Extension;
using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Point = System.Windows.Point;

namespace Serein.Workbench.Node.View
{
    #region 连接点相关代码



    public class ConnectionModelBase
    {
        /// <summary>
        /// 起始节点
        /// </summary>
        public NodeModelBase StartNode { get; set; }
        /// <summary>
        /// 目标节点
        /// </summary>
        public NodeModelBase EndNode { get; set; }

        /// <summary>
        /// 来源于起始节点的（控制点）类型
        /// </summary>
        public JunctionType JoinTypeOfStart { get; set; }

        /// <summary>
        /// 连接到目标节点的（控制点）类型
        /// </summary>
        public JunctionType JoinTypeOfEnd { get; set; }

        /// <summary>
        /// 连接类型
        /// </summary>
        public ConnectionInvokeType Type { get; set; }
    }


    public interface IJunctionNode
    {
        string BoundNodeGuid { get; }
    }

    /// <summary>
    /// 连接点
    /// </summary>
    public class JunctionNode : IJunctionNode
    {
        /// <summary>
        /// 连接点类型
        /// </summary>
        public JunctionType JunctionType { get; }
        /// <summary>
        /// 对应的视图对象
        /// </summary>
        public NodeModelBase NodeModel { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string BoundNodeGuid { get => NodeModel.Guid; }
    }


   
   
   
   
   
   
   
   
   
   
   
   
   
   
   
   
   
   
   
   






    #endregion




    


    /// <summary>
    /// 连接控件，表示控件的连接关系
    /// </summary>
    public class ConnectionControl
    {

        private readonly Action RemoteCallback;

        /// <summary>
        /// 关于调用
        /// </summary>
        /// <param name="Canvas"></param>
        /// <param name="Type"></param>
        public ConnectionControl(Canvas Canvas,
                                ConnectionInvokeType Type,
                                JunctionControlBase Start,
                                JunctionControlBase End,
                                Action remoteCallback)
        {
            this.LineType = LineType.Bezier;
            this.RemoteCallback = remoteCallback;
            this.Canvas = Canvas;
            this.Type = Type;
            this.Start = Start;
            this.End = End;
            //this.Start.Background = GetLineColor(Type);  // 线条颜色
            //this.End.Background = GetLineColor(Type);  // 线条颜色
            InitElementPoint();
        }

        /// <summary>
        /// 关于入参
        /// </summary>
        /// <param name="Canvas"></param>
        /// <param name="Type"></param>
        public ConnectionControl(LineType LineType,
                                Canvas Canvas,
                                int argIndex,
                                ConnectionArgSourceType connectionArgSourceType,
                                JunctionControlBase Start,
                                JunctionControlBase End,
                                Action remoteCallback)
        {
            this.LineType = LineType;
            this.RemoteCallback = remoteCallback;
            this.Canvas = Canvas;
            this.ArgIndex = ArgIndex;
            this.ConnectionArgSourceType = connectionArgSourceType;
            this.Start = Start;
            this.End = End;
            //this.Start.Background = GetLineColor(Type);  // 线条颜色
            //this.End.Background = GetLineColor(Type);  // 线条颜色
            InitElementPoint();
        }

        /// <summary>
        /// 所在的画布
        /// </summary>
        public Canvas Canvas { get; }

        /// <summary>
        /// 调用方法类型，连接类型
        /// </summary>
        public ConnectionInvokeType Type { get; }

        /// <summary>
        /// 获取参数类型，第几个参数
        /// </summary>
        public int ArgIndex { get; set; } = -1;

        /// <summary>
        /// 参数来源（决定了连接线的样式）
        /// </summary>
        public ConnectionArgSourceType ConnectionArgSourceType { get; set; }

        /// <summary>
        /// 起始控制点
        /// </summary>
        public JunctionControlBase Start { get; set; }

        /// <summary>
        /// 目标控制点
        /// </summary>
        public JunctionControlBase End { get; set; }

        /// <summary>
        /// 连接线
        /// </summary>
        private BezierLine BezierLine;

        private LineType LineType;

        /// <summary>
        /// 配置连接曲线的右键菜单
        /// </summary>
        /// <param name="line"></param>
        private void ConfigureLineContextMenu()
        {
            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(MainWindow.CreateMenuItem("删除连线", (s, e) => this.DeleteConnection()));
            BezierLine.ContextMenu = contextMenu;
        }

        /// <summary>
        /// 删除该连线
        /// </summary>
        /// <param name="line"></param>
        public void DeleteConnection()
        {
            if(this.Start is JunctionControlBase startJunctionControlBase)
            {
                //startJunctionControlBase.Background = Brushes.Transparent;
            }
            if (this.End is JunctionControlBase endJunctionControlBase)
            {
                //endJunctionControlBase.Background = Brushes.Transparent;
            }
            Canvas.Children.Remove(BezierLine); // 移除线
            RemoteCallback?.Invoke();
        }

        /// <summary>
        /// 重新绘制
        /// </summary>
        public void RefreshLine()
        {
            (Point startPoint, Point endPoint) = RefreshPoint(Canvas, Start, End);
            BezierLine.UpdatePoints(startPoint, endPoint);
            //BezierLine.UpdatePoints(startPoint, endPoint);
        }
        /// <summary>
        /// 删除该连线
        /// </summary>
        public void Remote()
        {
            Canvas.Children.Remove(BezierLine);
        }
        /// <summary>
        /// 绘制
        /// </summary>
        public void InitElementPoint()
        {
            leftCenterOfEndLocation = Start.MyCenterPoint;
            rightCenterOfStartLocation = End.MyCenterPoint;
            //leftCenterOfEndLocation = new Point(0, End.ActualHeight / 2); // 目标节点选择左侧边缘中心
            //rightCenterOfStartLocation = new Point(Start.ActualWidth, Start.ActualHeight / 2);  // 起始节点选择右侧边缘中心 


            linkSize = 4;  // 整线条粗细
            (Point startPoint, Point endPoint) = RefreshPoint(Canvas, Start, End);
            BezierLine = new BezierLine(LineType, startPoint, endPoint, GetLineColor(Type), linkSize);
            Grid.SetZIndex(BezierLine, -9999999); // 置底
            Canvas.Children.Add(BezierLine);


            ConfigureLineContextMenu(); //配置右键菜单
        }


        double linkSize;  // 根据缩放比例调整线条粗细
        private Point rightCenterOfStartLocation;  // 目标节点选择左侧边缘中心
        private Point leftCenterOfEndLocation;  // 起始节点选择右侧边缘中心 

        #region 工具方法
        private (Point startPoint,Point endPoint) RefreshPoint(Canvas canvas, FrameworkElement startElement, FrameworkElement endElement)
        {
            var startPoint = startElement.TranslatePoint(rightCenterOfStartLocation, canvas); // 获取起始节点的中心位置
            var endPoint = endElement.TranslatePoint(leftCenterOfEndLocation, canvas); // 计算终点位置
            return (startPoint, endPoint);
        }

        /// <summary>
        /// 根据连接类型指定颜色
        /// </summary>
        /// <param name="currentConnectionType"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static SolidColorBrush GetLineColor(ConnectionInvokeType currentConnectionType)
        {
            return currentConnectionType switch
            {
                ConnectionInvokeType.IsSucceed => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#04FC10")),
                ConnectionInvokeType.IsFail => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F18905")),
                ConnectionInvokeType.IsError => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FE1343")),
                ConnectionInvokeType.Upstream => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A82E4")),
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#56CEF6")),
                //_ => throw new Exception(),
            };
        }

        #endregion
    }





    /*
         /// <summary>
        /// 连接控件，表示控件的连接关系
        /// </summary>
        public class ConnectionControl : Shape
        {
            private readonly Action RemoteCallback;

            /// <summary>
            /// 关于调用
            /// </summary>
            /// <param name="Canvas"></param>
            /// <param name="Type"></param>
            public ConnectionControl(Canvas Canvas,
                                    ConnectionInvokeType Type,
                                    JunctionControlBase Start,
                                    JunctionControlBase End,
                                    Action remoteCallback)
            {
                this.RemoteCallback = remoteCallback;
                this.Canvas = Canvas;
                this.Type = Type;
                this.Start = Start;
                this.End = End;
                this.Start.Background = GetLineColor(Type);  // 线条颜色
                this.End.Background = GetLineColor(Type);  // 线条颜色
                InitElementPoint();
            }

            /// <summary>
            /// 关于入参
            /// </summary>
            /// <param name="Canvas"></param>
            /// <param name="Type"></param>
            public ConnectionControl(Canvas Canvas,
                                    int argIndex,
                                    ConnectionArgSourceType connectionArgSourceType,
                                    JunctionControlBase Start,
                                    JunctionControlBase End,
                                    Action remoteCallback)
            {
                this.RemoteCallback = remoteCallback;
                this.Canvas = Canvas;
                this.ArgIndex = ArgIndex;
                this.ConnectionArgSourceType = connectionArgSourceType;
                this.Start = Start;
                this.End = End;
                this.Start.Background = GetLineColor(Type);  // 线条颜色
                this.End.Background = GetLineColor(Type);  // 线条颜色
                InitElementPoint();
            }

            /// <summary>
            /// 所在的画布
            /// </summary>
            public Canvas Canvas { get; }

            /// <summary>
            /// 调用方法类型，连接类型
            /// </summary>
            public ConnectionInvokeType Type { get; }

            /// <summary>
            /// 获取参数类型，第几个参数
            /// </summary>
            public int ArgIndex { get; set; } = -1;

            /// <summary>
            /// 参数来源（决定了连接线的样式）
            /// </summary>
            public ConnectionArgSourceType ConnectionArgSourceType { get; set; }

            /// <summary>
            /// 起始控制点
            /// </summary>
            public JunctionControlBase Start { get; set; }

            /// <summary>
            /// 目标控制点
            /// </summary>
            public JunctionControlBase End { get; set; }


            /// <summary>
            /// 配置连接曲线的右键菜单
            /// </summary>
            /// <param name="line"></param>
            private void ConfigureLineContextMenu(ConnectionControl connection)
            {
                var contextMenu = new ContextMenu();
                contextMenu.Items.Add(MainWindow.CreateMenuItem("删除连线", (s, e) => DeleteConnection(connection)));
                connection.ContextMenu = contextMenu;

            }

            /// <summary>
            /// 删除该连线
            /// </summary>
            /// <param name="line"></param>
            private void DeleteConnection(ConnectionControl connection)
            {
                var connectionToRemove = connection;
                if (connectionToRemove is null)
                {
                    return;
                }
                if(this.Start is JunctionControlBase startJunctionControlBase)
                {
                    startJunctionControlBase.Background = Brushes.Transparent;
                }
                if (this.End is JunctionControlBase endJunctionControlBase)
                {
                    endJunctionControlBase.Background = Brushes.Transparent;
                }
                this.Canvas.g
                RemoteCallback?.Invoke();
            }

            /// <summary>
            /// 移除
            /// </summary>
            public void RemoveFromCanvas()
            {
                Canvas.Children.Remove(this); // 移除线
            }

            /// <summary>
            /// 重新绘制
            /// </summary>
            public void AddOrRefreshLine()
            {
                this.InvalidateVisual();
            }

            public void InitElementPoint()
            {
                leftCenterOfEndLocation = new Point(0, End.ActualHeight / 2); // 目标节点选择左侧边缘中心
                rightCenterOfStartLocation = new Point(Start.ActualWidth, Start.ActualHeight / 2);  // 起始节点选择右侧边缘中心 
                brush = GetLineColor(Type);  // 线条颜色
                hitVisiblePen = new Pen(Brushes.Transparent, 1.0); // 初始化碰撞检测线
                hitVisiblePen.Freeze(); // Freeze以提高性能
                visualPen = new Pen(brush, 2.0); // 默认可视化Pen
                visualPen.Freeze(); // Freeze以提高性能
                ConfigureLineContextMenu(this); // 设置连接右键事件
                linkSize = 4;  // 整线条粗细
                Canvas.Children.Add(this); // 添加线
                Grid.SetZIndex(this, -9999999); // 置底
            }

            /// <summary>
            /// 控件重绘事件
            /// </summary>
            /// <param name="drawingContext"></param>
            protected override void OnRender(DrawingContext drawingContext)
            {
                RefreshPoint(Canvas, this.Start, this.End); // 刷新坐标
                DrawBezierCurve(drawingContext, startPoint, endPoint, linkSize, brush); // 刷新线条显示位置
            }

            private readonly StreamGeometry streamGeometry = new StreamGeometry();
            private Point rightCenterOfStartLocation;  // 目标节点选择左侧边缘中心
            private Point leftCenterOfEndLocation;  // 起始节点选择右侧边缘中心 
            private Pen hitVisiblePen;  // 初始化碰撞检测线
            private Pen visualPen; // 默认可视化Pen
            private Point startPoint; // 连接线的起始节点
            private Point endPoint; // 连接线的终点
            private Brush brush; // 线条颜色
            double linkSize;  // 根据缩放比例调整线条粗细
            protected override Geometry DefiningGeometry => streamGeometry;

            #region 工具方法

            public void RefreshPoint(Canvas canvas, FrameworkElement startElement, FrameworkElement endElement)
            {
                endPoint = endElement.TranslatePoint(leftCenterOfEndLocation, canvas); // 计算终点位置
                startPoint = startElement.TranslatePoint(rightCenterOfStartLocation, canvas); // 获取起始节点的中心位置
            }

            /// <summary>
            /// 根据连接类型指定颜色
            /// </summary>
            /// <param name="currentConnectionType"></param>
            /// <returns></returns>
            /// <exception cref="Exception"></exception>
            public static SolidColorBrush GetLineColor(ConnectionInvokeType currentConnectionType)
            {
                return currentConnectionType switch
                {
                    ConnectionInvokeType.IsSucceed => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#04FC10")),
                    ConnectionInvokeType.IsFail => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F18905")),
                    ConnectionInvokeType.IsError => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FE1343")),
                    ConnectionInvokeType.Upstream => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A82E4")),
                    _ => throw new Exception(),
                };
            }
            private Point c0, c1; // 用于计算贝塞尔曲线控制点逻辑
            private Vector axis = new Vector(1, 0);
            private Vector startToEnd;
            private void DrawBezierCurve(DrawingContext drawingContext,
                                       Point start,
                                       Point end,
                                       double linkSize,
                                       Brush brush,
                                       bool isHitTestVisible = false,
                                       double strokeThickness = 1.0,
                                       bool isMouseOver = false,
                                       double dashOffset = 0.0)
            {
                // 控制点的计算逻辑
                double power = 8 * 8;  // 控制贝塞尔曲线的“拉伸”强度

                // 计算轴向向量与起点到终点的向量
                //var axis = new Vector(1, 0);
                startToEnd = (end.ToVector() - start.ToVector()).NormalizeTo();

                // 计算拉伸程度k，拉伸与水平夹角正相关
                var k = 1 - Math.Pow(Math.Max(0, axis.DotProduct(startToEnd)), 10.0);

                // 如果起点x大于终点x，增加额外的偏移量，避免重叠
                var bias = start.X > end.X ? Math.Abs(start.X - end.X) * 0.25 : 0;

                // 控制点的实际计算
                c0 = new Point(+(power + bias) * k + start.X, start.Y);
                c1 = new Point(-(power + bias) * k + end.X, end.Y);

                // 准备StreamGeometry以用于绘制曲线
                streamGeometry.Clear();
                using (var context = streamGeometry.Open())
                {
                    context.BeginFigure(start, true, false);   // 曲线起点
                    context.BezierTo(c0, c1, end, true, false); // 画贝塞尔曲线
                }

                drawingContext.DrawGeometry(null, visualPen, streamGeometry);

                // 绘制碰撞检测线
                //if (true)
                //{
                //    //hitVisiblePen = new Pen(Brushes.Transparent, linkSize + strokeThickness);
                //    //hitVisiblePen.Freeze();
                //    drawingContext.DrawGeometry(null, hitVisiblePen, streamGeometry);
                //}
                //else
                //{


                //}

            }
            #endregion
        }
     */




}
