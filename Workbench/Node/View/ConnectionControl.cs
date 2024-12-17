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
        /// <summary>
        /// 所在的画布
        /// </summary>
        public Canvas Canvas { get; }

        /// <summary>
        /// 调用方法类型，连接类型
        /// </summary>
        public ConnectionInvokeType InvokeType { get; }

        /// <summary>
        /// 目标节点控制点
        /// </summary>
        private INodeJunction EndNode;

        /// <summary>
        /// 获取参数类型，第几个参数
        /// </summary>
        public int ArgIndex { get; set; } = -1;

        /// <summary>
        /// 参数来源（决定了连接线的样式）
        /// </summary>
        public ConnectionArgSourceType ArgSourceType { get; set; }

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
        private ConnectionLineShape BezierLine;



        private LineType LineType;

        /// <summary>
        /// 关于调用
        /// </summary>
        /// <param name="Canvas"></param>
        /// <param name="invokeType"></param>
        public ConnectionControl(Canvas Canvas,
                                ConnectionInvokeType invokeType,
                                JunctionControlBase Start,
                                JunctionControlBase End)
        {
            this.LineType = LineType.Bezier;
            this.Canvas = Canvas;
            this.InvokeType = invokeType;
            this.Start = Start;
            this.End = End;
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
                                ConnectionArgSourceType argSourceType,
                                JunctionControlBase Start,
                                JunctionControlBase End,
                                INodeJunction nodeJunction)
        {
            this.LineType = LineType;
            this.Canvas = Canvas;
            this.ArgIndex = argIndex;
            this.ArgSourceType = argSourceType;
            this.Start = Start;
            this.End = End;
            this.EndNode = nodeJunction;
            InitElementPoint();
        }

        /// <summary>
        /// 绘制
        /// </summary>
        public void InitElementPoint()
        {
            leftCenterOfEndLocation = Start.MyCenterPoint;
            rightCenterOfStartLocation = End.MyCenterPoint;

            (Point startPoint, Point endPoint) = RefreshPoint(Canvas, Start, End);
            var connectionType = Start.JunctionType.ToConnectyionType();
            bool isDotted;
            Brush brush;
            if(connectionType == JunctionOfConnectionType.Invoke)
            {
                brush = InvokeType.ToLineColor();
                isDotted = false;
            }
            else
            {
                brush = ArgSourceType.ToLineColor();
                isDotted = true; // 如果为参数，则绘制虚线
            }
            BezierLine = new ConnectionLineShape(LineType, startPoint, endPoint, brush, isDotted); 
            Grid.SetZIndex(BezierLine, -9999999); // 置底
            Canvas.Children.Add(BezierLine);

            ConfigureLineContextMenu(); //配置右键菜单
        }


        /// <summary>
        /// 配置连接曲线的右键菜单
        /// </summary>
        private void ConfigureLineContextMenu()
        {
            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(MainWindow.CreateMenuItem("删除连线", (s, e) => this.Remote()));
            BezierLine.ContextMenu = contextMenu;
        }

       
        /// <summary>
        /// 删除该连线
        /// </summary>
        public void Remote()
        {
            Canvas.Children.Remove(BezierLine);
            var env = Start.MyNode.Env;
            if (Start.JunctionType.ToConnectyionType() == JunctionOfConnectionType.Invoke)
            {
                env.RemoveConnectInvokeAsync(Start.MyNode.Guid, End.MyNode.Guid, InvokeType);
            }
            else if (Start.JunctionType.ToConnectyionType() == JunctionOfConnectionType.Arg)
            {
                env.RemoveConnectArgSourceAsync(Start.MyNode.Guid, End.MyNode.Guid, ArgIndex) ;
            }
        }

        /// <summary>
        /// 重新绘制
        /// </summary>
        public void RefreshLine()
        {
            if(ArgIndex > -1)
            {
                End = EndNode.GetJunctionOfArgData(ArgIndex);
            }
            (Point startPoint, Point endPoint) = RefreshPoint(Canvas, Start, End);
            BezierLine.UpdatePoints(startPoint, endPoint);
        }


        private Point rightCenterOfStartLocation;  // 目标节点选择左侧边缘中心
        private Point leftCenterOfEndLocation;  // 起始节点选择右侧边缘中心 
        /// <summary>
        /// 刷新坐标
        /// </summary>

        private (Point startPoint, Point endPoint) RefreshPoint(Canvas canvas, FrameworkElement startElement, FrameworkElement endElement)
        {
            var startPoint = startElement.TranslatePoint(rightCenterOfStartLocation, canvas); // 获取起始节点的中心位置
            var endPoint = endElement.TranslatePoint(leftCenterOfEndLocation, canvas); // 计算终点位置
            return (startPoint, endPoint);
        }
    }








}
