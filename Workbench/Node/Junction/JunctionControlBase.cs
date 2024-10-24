using Serein.Library;
using Serein.Library.Utils;
using System;
using System.Net;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Serein.Workbench.Node.View
{

    

    public abstract class JunctionControlBase : Shape 
    {


        protected JunctionControlBase()
        {
            this.Width = 25;
            this.Height = 20;
            this.MouseDown += ControlPointBase_MouseDown;
            this.MouseMove += ControlPointBase_MouseMove;
        }
        #region 控件属性，所在的节点
        public static readonly DependencyProperty NodeProperty =
       DependencyProperty.Register(nameof(MyNode), typeof(NodeModelBase), typeof(JunctionControlBase), new PropertyMetadata(default(NodeModelBase)));

        /// <summary>
        /// 所在的节点
        /// </summary>
        public NodeModelBase MyNode
        {
            get { return (NodeModelBase)GetValue(NodeProperty); }
            set { SetValue(NodeProperty, value); }
        }
        #endregion

        #region 控件属性，连接器类型
        public static readonly DependencyProperty JunctionTypeProperty =
        DependencyProperty.Register(nameof(JunctionType), typeof(string), typeof(JunctionControlBase), new PropertyMetadata(default(string)));

        /// <summary>
        /// 控制点类型
        /// </summary>
        public JunctionType JunctionType
        {
            get { return EnumHelper.ConvertEnum<JunctionType>(GetValue(JunctionTypeProperty).ToString()); }
            set { SetValue(JunctionTypeProperty, value.ToString()); }
        }
        #endregion
        protected readonly StreamGeometry StreamGeometry = new StreamGeometry();
        protected override Geometry DefiningGeometry => StreamGeometry;
        
        /// <summary>
        /// 重绘方法
        /// </summary>
        /// <param name="drawingContext"></param>
        public abstract void Render(DrawingContext drawingContext);
        /// <summary>
        /// 中心点
        /// </summary>
        public abstract Point MyCenterPoint { get;  }

        // 处理鼠标悬停状态
        private bool _isMouseOver;
        public bool IsMouseOver
        {
            get => _isMouseOver;
            set
            {
                _isMouseOver = value;
                InvalidateVisual();
            }
        }

        /// <summary>
        /// 控件重绘事件
        /// </summary>
        /// <param name="drawingContext"></param>
        protected override void OnRender(DrawingContext drawingContext)
        {
            Render(drawingContext);
        }


        protected void ControlPointBase_MouseMove(object sender, MouseEventArgs e)
        {
            if (GlobalJunctionData.MyGlobalConnectingData is null) return;
            GlobalJunctionData.MyGlobalConnectingData.CurrentJunction = this;
        }



        /// <summary>
        /// 在碰撞点上按下鼠标控件开始进行移动
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void ControlPointBase_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                GlobalJunctionData.MyGlobalConnectingData = new ConnectingData();
                var myDataType = GlobalJunctionData.MyGlobalConnectingData;
                myDataType.StartJunction = this;
                var canvas = MainWindow.GetParentOfType<Canvas>(this);
                if (canvas != null)
                {
                    //myDataType.StartPoint = this.MyCenterPoint;
                    myDataType.StartPoint = this.TranslatePoint(new Point(this.Width / 2, this.Height / 2), canvas);
                    var bezierLine = new BezierLine(LineType.Bezier, myDataType.StartPoint, myDataType.StartPoint, Brushes.Green);
                    myDataType.VirtualLine = new MyLine(canvas, bezierLine);
                }
            }
            e.Handled = true;
        }

        private Point GetStartPoint()
        {
           return new Point(this.ActualWidth / 2, this.ActualHeight / 2);  // 起始节点选择右侧边缘中心 
        }
    }

   


   


}
