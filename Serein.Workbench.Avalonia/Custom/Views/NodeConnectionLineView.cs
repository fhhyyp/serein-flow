using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.VisualTree;
using Serein.Library;
using Serein.Script.Node;
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

namespace Serein.Workbench.Avalonia.Custom.Views
{
    


    public class NodeConnectionLineView
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

        public NodeConnectionLineView(Canvas canvas, 
                NodeJunctionView? leftNodeJunctionView,
                NodeJunctionView? rightNodeJunctionView)
        {
            this.Canvas = canvas;
            this.LeftNodeJunctionView = leftNodeJunctionView;
            this.RightNodeJunctionView = rightNodeJunctionView;
        }

        /// <summary>
        /// 连接到终点
        /// </summary>
        /// <param name="endNodeJunctionView"></param>
        public void ToEnd(NodeJunctionView endNodeJunctionView)
        {
            if((endNodeJunctionView.JunctionType == JunctionType.NextStep
                || endNodeJunctionView.JunctionType == JunctionType.ReturnData) 
                && RightNodeJunctionView is not null
                /*&& LeftNodeJunctionView is null*/
                /*&& !LeftNodeJunctionView.Equals(endNodeJunctionView)*/)
            {
                LeftNodeJunctionView = endNodeJunctionView;
                RefreshLineDsiplay();
                return;
            }
            else if ((endNodeJunctionView.JunctionType == JunctionType.Execute
                 || endNodeJunctionView.JunctionType == JunctionType.ArgData)
                 && LeftNodeJunctionView is not null
                 /*&& RightNodeJunctionView is null*/
                 /*&& !RightNodeJunctionView.Equals(endNodeJunctionView)*/)
            {
                RightNodeJunctionView = endNodeJunctionView;
                RefreshLineDsiplay();
                return;
            }


            //

            //var leftPoint = GetPoint(LeftNodeJunctionView);
            //var rightPoint = GetPoint(RightNodeJunctionView);
            //var brush = GetBackgrounp();
            //ConnectionLineShape.UpdatePoint(leftPoint, rightPoint);
            //CreateLineShape(startPoint, endPoint, brush);
        }

        /// <summary>
        /// 刷新线的显示
        /// </summary>
        public void RefreshLineDsiplay()
        {
            if(LeftNodeJunctionView is null || RightNodeJunctionView is null)
            {
                return;
            }
            var leftPoint = GetPoint(LeftNodeJunctionView);
            var rightPoint = GetPoint(RightNodeJunctionView);
            if (ConnectionLineShape is null)
            {
                Debug.WriteLine("创建");
                CreateLineShape(leftPoint, rightPoint, GetBackgrounp());
            }
            else
            {
                Debug.WriteLine("刷新");
                var brush = GetBackgrounp();
                ConnectionLineShape.UpdatePoint( leftPoint, rightPoint, brush);
            }
        }


        /// <summary>
        /// 刷新临时线的显示
        /// </summary>
        public void RefreshRightPointOfTempLineDsiplay(Point rightPoint)
        {
            if(ConnectionLineShape is not null)
            {
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
            if(ConnectionLineShape is not null)
            {
                var brush = GetBackgrounp();
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

            //var point = nodeJunctionView.TranslatePoint(defaultPoint ,  Canvas);
            //if(point is null)
            //{
            //    return defaultPoint;
            //}
            //else
            //{
            //    return point.Value;
           // }
        }

        private void CreateLineShape(Point leftPoint, Point rightPoint, Brush brush)
        {
            ConnectionLineShape = new ConnectionLineShape(leftPoint, rightPoint, brush);
            Canvas.Children.Add(ConnectionLineShape);
        }

        private JunctionOfConnectionType GetConnectionType()
        {
            return LeftNodeJunctionView.JunctionType.ToConnectyionType();
        }


        /// <summary>
        /// 获取背景颜色
        /// </summary>
        /// <returns></returns>
        public Brush GetBackgrounp()
        {

            if(LeftNodeJunctionView is null || RightNodeJunctionView is null)
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
            else       
            {          
                return ConnectionArgSourceType.ToLineColor(); // 参数
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
            if(ConnectionLineShape is null)
            {
                return;
            }
            Canvas.Children.Remove(ConnectionLineShape);
        }
    }
}
