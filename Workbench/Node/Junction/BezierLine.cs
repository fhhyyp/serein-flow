using Serein.Library;
using Serein.Workbench.Extension;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Serein.Workbench.Node.View
{
    /// <summary>
    /// 连接线的类型
    /// </summary>
    public enum LineType
    {
        /// <summary>
        /// 贝塞尔曲线
        /// </summary>
        Bezier,
        /// <summary>
        /// 半圆线
        /// </summary>
        Semicircle,
    }

    /// <summary>
    /// 贝塞尔曲线
    /// </summary>
    public class BezierLine : Shape
    {
        private readonly double strokeThickness;

        private readonly LineType lineType;

        /// <summary>
        /// 确定起始坐标和目标坐标、外光样式的曲线
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="brush"></param>
        /// <param name="strokeThickness"></param>
        public BezierLine(LineType lineType,  Point start, Point end, Brush brush, double strokeThickness = 4)
        {
            this.lineType = lineType;
            this.brush = brush;
            startPoint = start;
            endPoint = end;
            this.strokeThickness = strokeThickness;
            InitElementPoint();
            InvalidateVisual(); // 触发重绘
        }
        public void InitElementPoint()
        {
            hitVisiblePen = new Pen(Brushes.Transparent, 1.0); // 初始化碰撞检测线
            hitVisiblePen.Freeze(); // Freeze以提高性能
            visualPen = new Pen(brush, 3.0); // 默认可视化Pen
            visualPen.Freeze(); // Freeze以提高性能
            linkSize = 4;  // 整线条粗细
            Panel.SetZIndex(this, -9999999); // 置底
        }

        /// <summary>
        /// 更新线条落点位置
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        public void UpdatePoints(Point start, Point end)
        {
            startPoint = start;
            endPoint = end;
            InvalidateVisual(); // 触发重绘
        }

        /// <summary>
        /// 更新线条落点位置
        /// </summary>
        /// <param name="point"></param>
        public void UpdateEndPoints(Point point)
        {
            endPoint = point;
            InvalidateVisual(); // 触发重绘
        }
        /// <summary>
        /// 更新线条落点位置
        /// </summary>
        /// <param name="point"></param>
        public void UpdateStartPoints(Point point)
        {
            startPoint = point;
            InvalidateVisual(); // 触发重绘
        }
        /// <summary>
        /// 控件重绘事件
        /// </summary>
        /// <param name="drawingContext"></param>
        protected override void OnRender(DrawingContext drawingContext)
        {
            // 刷新线条显示位置
            switch (this.lineType)
            {
                case LineType.Bezier:
                    DrawBezierCurve(drawingContext, startPoint, endPoint, linkSize); 
                    break;
                case LineType.Semicircle:
                    DrawBezierCurve(drawingContext, startPoint, endPoint);
                    break;
                default:
                    break;
            }

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


        private Point c0, c1; // 用于计算贝塞尔曲线控制点逻辑
        private Vector axis = new Vector(1, 0);
        private Vector startToEnd;
        private void DrawBezierCurve(DrawingContext drawingContext,
                                   Point start,
                                   Point end,
                                   double linkSize,
                                   bool isHitTestVisible = false,
                                   double strokeThickness = 1.0,
                                   bool isMouseOver = false,
                                   double dashOffset = 0.0)
        {
            // 控制点的计算逻辑
            double power = 100;  // 控制贝塞尔曲线的“拉伸”强度

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


        private void DrawBezierCurve(DrawingContext drawingContext, Point start, Point end)
        {
            // 计算中心点和半径
            // 计算圆心和半径
            double x = 35 ;
            // 创建一个弧线路径
            streamGeometry.Clear();
            using (var context = streamGeometry.Open())
            {
                // 开始绘制
                context.BeginFigure(start, false, false);

                // 生成弧线
                context.ArcTo(
                    end,               // 结束点
                    new Size(x, x),   // 椭圆的半径
                    0,                 // 椭圆的旋转角度
                    false,            // 是否大弧
                    SweepDirection.Counterclockwise, // 方向
                    true,             // 是否连接到起始点
                    true              // 是否使用高质量渲染
                );

                // 结束绘制
                context.LineTo(start, false, false); // 连接到起始点（可选）
            }

            // 绘制弧线
            drawingContext.DrawGeometry(null, visualPen, streamGeometry);

        }
    }


}
