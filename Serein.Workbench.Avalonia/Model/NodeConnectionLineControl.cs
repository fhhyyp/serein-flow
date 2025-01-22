using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.VisualTree;
using Serein.Library;
using Serein.Script.Node;
using Serein.Workbench.Avalonia.Custom.Views;
using Serein.Workbench.Avalonia.Extension;
using Serein.Workbench.Avalonia.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Color = Avalonia.Media.Color;
using Point = Avalonia.Point;

namespace Serein.Workbench.Avalonia.Model
{



    public class NodeConnectionLineControl
    {
        /// <summary>
        /// 线条类别（方法调用）
        /// </summary>
        public ConnectionInvokeType ConnectionInvokeType { get; set; } = ConnectionInvokeType.IsSucceed;
        /// <summary>
        /// 线条类别（参数传递）
        /// </summary>
        public ConnectionArgSourceType ConnectionArgSourceType { get; set; } = ConnectionArgSourceType.GetOtherNodeData;


        /// <summary>
        /// 画布
        /// </summary>
        private Canvas Canvas;
        /// <summary>
        /// 连接线的起点
        /// </summary>
        private NodeJunctionView? LeftNodeJunctionView;
        /// <summary>
        /// 连接线的终点
        /// </summary>
        private NodeJunctionView? RightNodeJunctionView;

        /// <summary>
        /// 连接时显示的线
        /// </summary>
        public ConnectionLineShape? ConnectionLineShape { get; private set; }

        private NodeJunctionView StartNodeJunctionView;

        public NodeConnectionLineControl(Canvas canvas,
                NodeJunctionView? leftNodeJunctionView,
                NodeJunctionView? rightNodeJunctionView)
        {
            if (leftNodeJunctionView is null && rightNodeJunctionView is null)
            {
                throw new Exception("不能都为空");
            }

            Canvas = canvas;
            LeftNodeJunctionView = leftNodeJunctionView;
            RightNodeJunctionView = rightNodeJunctionView;

            if (leftNodeJunctionView is null && rightNodeJunctionView is not null)
            {
                StartNodeJunctionView = rightNodeJunctionView;
            }
            else if(leftNodeJunctionView is not null && rightNodeJunctionView is null)
            {
                StartNodeJunctionView = leftNodeJunctionView;
            }
            else if (leftNodeJunctionView is not null && rightNodeJunctionView is not null)
            {
                LeftNodeJunctionView = leftNodeJunctionView;
                RightNodeJunctionView = rightNodeJunctionView;
                RefreshLineDsiplay();
            }

          
        }

        /// <summary>
        /// 连接到终点
        /// </summary>
        /// <param name="endNodeJunctionView"></param>
        public void ToEnd(NodeJunctionView endNodeJunctionView)
        {
            var @bool = endNodeJunctionView.JunctionType == JunctionType.Execute || endNodeJunctionView.JunctionType == JunctionType.ArgData;
            (LeftNodeJunctionView, RightNodeJunctionView) = @bool? (StartNodeJunctionView, endNodeJunctionView) : (endNodeJunctionView, StartNodeJunctionView);
            RefreshLineDsiplay();
            return;

            /*if(StartNodeJunctionView.JunctionType == JunctionType.NextStep
                 && endNodeJunctionView.JunctionType == JunctionType.Execute 
                 && StartNodeJunctionView.MyNode?.Equals(endNodeJunctionView.MyNode) == false)
            {
                LeftNodeJunctionView = StartNodeJunctionView;
                RightNodeJunctionView = endNodeJunctionView;
                RefreshLineDsiplay();
                return;
            }

            if (StartNodeJunctionView.JunctionType == JunctionType.ReturnData
                 && endNodeJunctionView.JunctionType == JunctionType.ArgData
                 && StartNodeJunctionView.MyNode?.Equals(endNodeJunctionView.MyNode) == false)
            {
                LeftNodeJunctionView = StartNodeJunctionView;
                RightNodeJunctionView = endNodeJunctionView;
                RefreshLineDsiplay();
                return;
            }


            if (StartNodeJunctionView.JunctionType == JunctionType.Execute
                && endNodeJunctionView.JunctionType == JunctionType.NextStep
                && StartNodeJunctionView.MyNode?.Equals(endNodeJunctionView.MyNode) == false)
            {
                LeftNodeJunctionView = endNodeJunctionView;
                RightNodeJunctionView = StartNodeJunctionView;
                RefreshLineDsiplay();
                return;
            }

            if (StartNodeJunctionView.JunctionType == JunctionType.ArgData
                && endNodeJunctionView.JunctionType == JunctionType.ReturnData
                && StartNodeJunctionView.MyNode?.Equals(endNodeJunctionView.MyNode) == false)
            {
                LeftNodeJunctionView = endNodeJunctionView;
                RightNodeJunctionView = StartNodeJunctionView;
                RefreshLineDsiplay();
                return;
            }*/

        }

        /// <summary>
        /// 刷新线的显示
        /// </summary>
        public void RefreshLineDsiplay()
        {
            if (LeftNodeJunctionView is null || RightNodeJunctionView is null)
            {
                return;
            }
            var leftPoint = GetPoint(LeftNodeJunctionView);
            var rightPoint = GetPoint(RightNodeJunctionView);
            if (ConnectionLineShape is null)
            {
                CreateLineShape(leftPoint, rightPoint, GetBackgrounp());
            }
            else
            {
                var brush = GetBackgrounp();
                ConnectionLineShape.UpdatePoint(leftPoint, rightPoint, brush);
            }
        }

        //public void UpdateColor()
        //{
        //    var brush = GetBackgrounp();
        //    ConnectionLineShape?.UpdateColor(brush);
        //}

        /// <summary>
        /// 刷新临时线的显示
        /// </summary>
        public void RefreshRightPointOfTempLineDsiplay(Point rightPoint)
        {
            if (ConnectionLineShape is not null)
            {

                RightNodeJunctionView = null;
                var brush = GetBackgrounp();
                ConnectionLineShape.UpdateRightPoint(rightPoint, brush);
                return;
            }

            if (LeftNodeJunctionView is not null)
            {
                var leftPoint = GetPoint(LeftNodeJunctionView);
                var brush = GetBackgrounp();
                CreateLineShape(leftPoint, rightPoint, brush);
            }
        }

        /// <summary>
        /// 刷新临时线的显示
        /// </summary>
        public void RefreshLeftPointOfTempLineDsiplay(Point leftPoint)
        {
            if (ConnectionLineShape is not null)
            {
                var brush = GetBackgrounp();
                LeftNodeJunctionView = null;
                ConnectionLineShape.UpdateLeftPoints(leftPoint, brush);
                return;
            }

            if (RightNodeJunctionView is not null)
            {
                var rightPoint = GetPoint(RightNodeJunctionView);
                var brush = GetBackgrounp();
                CreateLineShape(leftPoint, rightPoint, brush);
            }
        }

        /// <summary>
        /// 获取背景颜色
        /// </summary>
        /// <returns></returns>
        public Brush GetBackgrounp()
        {

            if (LeftNodeJunctionView is null || RightNodeJunctionView is null)
            {
                return new SolidColorBrush(Color.Parse("#FF0000")); // 没有终点
            }

            // 判断连接控制点是否匹配
            if (!IsCanConnected())
            {
                return new SolidColorBrush(Color.Parse("#FF0000"));
            }


            if (GetConnectionType() == JunctionOfConnectionType.Invoke)
            {
                return ConnectionInvokeType.ToLineColor(); // 调用
            }
            else if (GetConnectionType() == JunctionOfConnectionType.Arg)
            {
                return ConnectionArgSourceType.ToLineColor(); // 参数
            }
            else
            {
                return new SolidColorBrush(Color.Parse("#FF0000"));
            }
        }

        public bool IsCanConnected()
        {
            if (LeftNodeJunctionView is null
                || RightNodeJunctionView is null)
            {
                return false;
            }
            if (LeftNodeJunctionView?.MyNode is null
                || LeftNodeJunctionView.MyNode.Equals(RightNodeJunctionView.MyNode))
                return false;

            if (LeftNodeJunctionView.JunctionType.IsCanConnection(RightNodeJunctionView.JunctionType))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 移除线
        /// </summary>
        public void Remove()
        {
            if (ConnectionLineShape is null)
            {
                return;
            }
            Canvas.Children.Remove(ConnectionLineShape);
        }


        private static Point defaultPoint = new Point(0, 0);
        int count;
        private Point GetPoint(NodeJunctionView nodeJunctionView)
        {

            var junctionSize = nodeJunctionView.GetTransformedBounds()!.Value.Bounds.Size;
            Point junctionPoint;
            if (nodeJunctionView.JunctionType == JunctionType.ArgData || nodeJunctionView.JunctionType == JunctionType.Execute)
            {
                junctionPoint = new Point(junctionSize.Width / 2 - 11, junctionSize.Height / 2); // 选择左侧
            }
            else
            {
                junctionPoint = new Point(junctionSize.Width / 2 + 11, junctionSize.Height / 2); // 选择右侧
            }
            if (nodeJunctionView.TranslatePoint(junctionPoint, Canvas) is Point point)
            {
                //myData.StartPoint = point;
                return point;
            }
            else
            {
                return defaultPoint;
            }
        }

        private void CreateLineShape(Point leftPoint, Point rightPoint, Brush brush)
        {
            ConnectionLineShape = new ConnectionLineShape(leftPoint, rightPoint, brush);
            Canvas.Children.Add(ConnectionLineShape);
        }

        private JunctionOfConnectionType GetConnectionType()
        {
            if(LeftNodeJunctionView is null)
            {
                if(RightNodeJunctionView is null)
                {

                    return JunctionOfConnectionType.None;
                }
                else
                {

                    return RightNodeJunctionView.JunctionType.ToConnectyionType();
                }
            }
            else
            {

                return LeftNodeJunctionView.JunctionType.ToConnectyionType();
            }
        }



    }
}
