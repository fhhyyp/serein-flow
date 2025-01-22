using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Skia;
using Avalonia.Styling;
using Serein.Library.Utils;
using Serein.Workbench.Avalonia.Extension;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;



namespace Serein.Workbench.Avalonia.Custom.Views
{
    public class ConnectionLineShape : Control
    {
        private readonly double strokeThickness;

        /// <summary>
        /// 确定起始坐标和目标坐标、外观样式的曲线
        /// </summary>
        /// <param name="left">起始坐标</param>
        /// <param name="right">结束坐标</param>
        /// <param name="brush">颜色</param>
        /// <param name="isDotted">是否为虚线</param>
        public ConnectionLineShape(Point left,
                                   Point right,
                                   Brush brush,
                                   bool isDotted = false,
                                   bool isTop = false)
        {
            this.brush = brush;
            this.leftPoint = left;
            this.rightPoint = right;
            this.strokeThickness = 4;
            InitElementPoint(isDotted, isTop);
            InvalidateVisual(); // 触发重绘
        }


        public void InitElementPoint(bool isDotted, bool isTop = false)
        {
            //hitVisiblePen = new Pen(Brushes.Transparent, 1.0); // 初始化碰撞检测线
            //hitVisiblePen.Freeze(); // Freeze以提高性能
            
            visualPen = new Pen(brush, 3.0); // 默认可视化Pen
            opacity = 1.0d;
            //var dashStyle = new     DashStyle();

            if (isDotted)
            {
                opacity = 0.42d;
                visualPen.DashStyle = new DashStyle(); // DashStyles.Dash; // 选择虚线样式
            }
            //visualPen.Freeze(); // Freeze以提高性能

            linkSize = 4;  // 整线条粗细
            int zIndex = -999999;
           
            this.ZIndex = zIndex;
            //Panel.SetZIndex(this, zIndex); // 置底
        }

        /// <summary>
        /// 更新线条落点位置
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        public void UpdatePoint(Point left, Point right, Brush? brush = null)
        {
            if(brush is not null)
            {
                visualPen = new Pen(brush, 3.0); // 默认可视化Pen
            }
            this.leftPoint = left;
            this.rightPoint = right;
            InvalidateVisual(); // 触发重绘
        }

        /// <summary>
        /// 更新线条落点位置
        /// </summary>
        /// <param name="right"></param>
        public void UpdateRightPoint(Point right, Brush? brush = null)
        {
            if (brush is not null)
            {
                visualPen = new Pen(brush, 3.0); // 默认可视化Pen
            }
            this.rightPoint = right;
            InvalidateVisual(); // 触发重绘
        }

        /// <summary>
        /// 更新线条起点位置
        /// </summary>
        /// <param name="left"></param>
        public void UpdateLeftPoints(Point left, Brush? brush = null)
        {
            if (brush is not null)
            {
                visualPen = new Pen(brush, 3.0); // 默认可视化Pen
            }
            this.leftPoint = left;
            InvalidateVisual(); // 触发重绘
        }
        /// <summary>
        /// 刷新颜色
        /// </summary>
        /// <param name="brush"></param>
        public void UpdateColor(Brush brush )
        {
            visualPen = new Pen(brush, 3.0); // 默认可视化Pen
            InvalidateVisual(); // 触发重绘
        }

        /// <summary>
        /// 控件重绘事件
        /// </summary>
        /// <param name="drawingContext"></param>
        public override void Render(DrawingContext drawingContext)
        {
            // 刷新线条显示位置
            DrawBezierCurve(drawingContext, leftPoint, rightPoint);

        }
        #region 重绘

        private StreamGeometry  streamGeometry = new StreamGeometry();
        private Point rightCenterOfStartLocation;  // 目标节点选择左侧边缘中心
        private Point leftCenterOfEndLocation;  // 起始节点选择右侧边缘中心 
        //private Pen hitVisiblePen;  // 初始化碰撞检测线
        private Pen visualPen; // 默认可视化Pen
        private Point leftPoint; // 连接线的起始节点
        private Point rightPoint; // 连接线的终点
        private Brush brush; // 线条颜色
        private double opacity; // 透明度

        double linkSize;  // 根据缩放比例调整线条粗细

        //public void UpdateLineColor()
        //{
        //    visualPen = new Pen(brush, 3.0); // 默认可视化Pen
        //    InvalidateVisual(); // 触发重绘
        //}


        private Point c0, c1; // 用于计算贝塞尔曲线控制点逻辑
        private Vector axis = new Vector(1, 0);
        private Vector startToEnd;
        private int i = 0;
        private void DrawBezierCurve(DrawingContext drawingContext,
                                   Point left,
                                   Point right)
        {
            // 控制点的计算逻辑
            double power = 140;  // 控制贝塞尔曲线的“拉伸”强度
            drawingContext.PushOpacity(opacity);

            // 计算轴向向量与起点到终点的向量
            //var axis = new Vector(1, 0);
            startToEnd = (right.ToVector() - left.ToVector()).NormalizeTo();



            //var dp = axis.DotProduct(startToEnd);
            //dp = dp < 50 ? 50 : dp;
            //var pow = Math.Max(0, dp) ;
            //var k = 1 - Math.Pow(pow, 10.0);
            //
            //Debug.WriteLine("pow : " + pow);
            //Debug.WriteLine("k   : " + k);
            //Debug.WriteLine("");

            // 计算拉伸程度k，拉伸与水平夹角正相关
            var dp = axis.DotProduct(startToEnd) ;
            var pow = Math.Pow(dp, 10.0);
            pow = pow > 0 ? 0 : pow;
            var k = 1 - pow;
            // 如果起点x大于终点x，增加额外的偏移量，避免重叠
            var bias = left.X > right.X ? Math.Abs(left.X - right.X) * 0.25 : 0;
            // 控制点的实际计算
            c0 = new Point(+(power + bias) * k + left.X, left.Y);
            c1 = new Point(-(power + bias) * k + right.X, right.Y);

            // 准备StreamGeometry以用于绘制曲线
            // why can't clearValue()?
            //streamGeometry.ClearValue(ThemeProperty);
            //var streamGeometry = new StreamGeometry();
            //if( i++ > 100 && streamGeometry is AvaloniaObject avaloniaObject)
            //{
            //    var platformImplInfo = streamGeometry.GetType().GetProperty("PlatformImpl", BindingFlags.NonPublic | BindingFlags.Instance);
            //    var platformImpl = platformImplInfo?.GetValue(streamGeometry);

            //     var pathCache = platformImpl?.GetType().GetField("_bounds", BindingFlags.NonPublic | BindingFlags.Instance);
            //    if(pathCache is IDisposable disposable)
            //    {
            //        disposable.Dispose();
            //    }
            //    //pathCache?.Invoke(platformImpl, []);
            //    Debug.WriteLine("invoke => InvalidateCaches");
            //    i = 0;
            //     //public class AvaloniaObject : IAvaloniaObjectDebug, INotifyPropertyChanged
            //     //private readonly ValueStore _values;
            //}

            // this is override "Render()" method
            // display a bezier-line on canvas and follow the mouse in real time
            // I don't want to re-instantiate StreamGeometry()
            // because I want to reduce the number of GC
            // but , it seems impossible to do so in avalonia
            streamGeometry = new StreamGeometry();
            // in wpf , this method allows display content to be cleared
            // streamGeometry.Clear();  // this is wpf method
            // in avalonia, why does this method need value of "AvaloniaProperty" data type ? 
            // but I don't know use what "AvaloniaProperty" to clear the displayed content
            // if I don't clear the cache or re-instantiate it
            // the canvas will display repeated lines , because exits cache inside streamGeometry
            // streamGeometry.ClearValue("AvaloniaProperty");  
            using (var context = streamGeometry.Open())
            {
                context.BeginFigure(left, true);   // start point of the bezier-line 
                context.CubicBezierTo(c0, c1, right, true); // drawing bezier-line
            }
            drawingContext.DrawGeometry(null, visualPen, streamGeometry);

        }

       
        #endregion
    }
}
