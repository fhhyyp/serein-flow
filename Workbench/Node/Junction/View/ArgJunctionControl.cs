using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Serein.Library;

namespace Serein.Workbench.Node.View
{
    public class ArgJunctionControl : JunctionControlBase
    {
        public ArgJunctionControl()
        {
            base.JunctionType = JunctionType.ArgData;
            this.InvalidateVisual();
        }

        #region 控件属性，对应的参数
        public static readonly DependencyProperty ArgIndexProperty =
            DependencyProperty.Register("ArgIndex", typeof(int), typeof(ArgJunctionControl), new PropertyMetadata(default(int)));

        /// <summary>
        /// 所在的节点
        /// </summary>
        public int ArgIndex
        {
            get { return (int)GetValue(ArgIndexProperty); }
            set { SetValue(ArgIndexProperty, value); }
        }


        #endregion
        private Point _myCenterPoint;
        public override Point MyCenterPoint { get => _myCenterPoint; }


        public override void Render(DrawingContext drawingContext)
        {
            double width = ActualWidth;
            double height = ActualHeight;
            var background = GetBackgrounp();
            // 输入连接器的背景
            var connectorRect = new Rect(0, 0, width, height);
            drawingContext.DrawRectangle(Brushes.Transparent, null, connectorRect);

            // 定义圆形的大小和位置
            double connectorSize = 10; // 连接器的大小
            double circleCenterX = 8; // 圆心 X 坐标
            double circleCenterY = height / 2; // 圆心 Y 坐标
            var circlePoint = new Point(circleCenterX, circleCenterY);
            _myCenterPoint = new Point(circleCenterX - connectorSize / 2, circleCenterY); // 中心坐标

            // 绘制连接器的圆形部分
            var ellipse = new EllipseGeometry(circlePoint, connectorSize / 2, connectorSize / 2);
            drawingContext.DrawGeometry(background, MyUIFunc.CreateAndFreezePen(), ellipse);

            // 定义三角形的间距
            double triangleOffsetX = 4; // 三角形与圆形的间距
            double triangleCenterX = circleCenterX + connectorSize / 2 + triangleOffsetX; // 三角形中心 X 坐标
            double triangleCenterY = circleCenterY; // 三角形中心 Y 坐标

            // 绘制三角形
            var pathGeometry = new StreamGeometry();
            using (var context = pathGeometry.Open())
            {
                context.BeginFigure(new Point(triangleCenterX, triangleCenterY - 4.5), true, true);
                context.LineTo(new Point(triangleCenterX + 5, triangleCenterY), true, false);
                context.LineTo(new Point(triangleCenterX, triangleCenterY + 4.5), true, false);
                context.LineTo(new Point(triangleCenterX, triangleCenterY - 4.5), true, false);
            }
            drawingContext.DrawGeometry(background, MyUIFunc.CreateAndFreezePen(), pathGeometry);
        }
    }


}
