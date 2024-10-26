using Serein.Library;
using Serein.Library.Utils;
using System;
using System.Net;
using System.Reflection;
using System.Windows;
using Serein.Workbench.Extension;
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
            this.MouseDown += JunctionControlBase_MouseDown;
            this.MouseMove += JunctionControlBase_MouseMove;
            this.MouseLeave += JunctionControlBase_MouseLeave; ;
        }

       
        #region 控件属性，所在的节点
        public static readonly DependencyProperty NodeProperty =
            DependencyProperty.Register(nameof(MyNode), typeof(NodeModelBase), typeof(JunctionControlBase), new PropertyMetadata(default(NodeModelBase)));
        //public NodeModelBase NodeModel;

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



        /// <summary>
        /// 禁止连接
        /// </summary>
        private bool IsConnectionDisable;

        /// <summary>
        /// 处理鼠标悬停状态
        /// </summary>
        private bool _isMouseOver;
        public bool IsMouseOver
        {
            get => _isMouseOver;
            set
            {
                if(_isMouseOver != value)
                {
                    GlobalJunctionData.MyGlobalConnectingData.CurrentJunction = this;
                    _isMouseOver = value;
                    InvalidateVisual();
                }
               
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

        /// <summary>
        /// 获取背景颜色
        /// </summary>
        /// <returns></returns>
        protected Brush GetBackgrounp()
        {
            var myData = GlobalJunctionData.MyGlobalConnectingData;
            if(!myData.IsCreateing)
            {
                return Brushes.Transparent;
            }
            if (IsMouseOver)
            {
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
            }
            else
            {
                return Brushes.Transparent;
            }
        }

        private object lockObj = new object();

        // 控件获得鼠标焦点事件
        private void JunctionControlBase_MouseMove(object sender, MouseEventArgs e)
        {
            //if (!GlobalJunctionData.MyGlobalConnectingData.IsCreateing) return;

            //if (IsMouseOver) return;
            IsMouseOver = true;
            
            //this.InvalidateVisual();
            
            
        }
        // 控件失去鼠标焦点事件
        private void JunctionControlBase_MouseLeave(object sender, MouseEventArgs e)
        {
            IsMouseOver = false;
            e.Handled = true;
            //Console.WriteLine("控件失去鼠标焦点");

        }


        /// <summary>
        /// 在碰撞点上按下鼠标控件开始进行移动
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void JunctionControlBase_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var canvas = MainWindow.GetParentOfType<Canvas>(this);
                if (canvas != null)
                {
                    var myData = GlobalJunctionData.MyGlobalConnectingData;
                    myData.Reset();
                    myData.IsCreateing = true; // 表示开始连接
                    myData.StartJunction = this;
                    myData.CurrentJunction = this;
                    myData.StartPoint = this.TranslatePoint(new Point(this.Width / 2, this.Height / 2), canvas);

                    var junctionOfConnectionType = this.JunctionType.ToConnectyionType();
                    ConnectionLineShape bezierLine; // 类别
                    Brush brushColor; // 临时线的颜色
                    if (junctionOfConnectionType == JunctionOfConnectionType.Invoke)
                    {
                        brushColor = ConnectionInvokeType.IsSucceed.ToLineColor();
                    }
                    else if(junctionOfConnectionType == JunctionOfConnectionType.Arg)
                    {
                        brushColor = ConnectionArgSourceType.GetOtherNodeData.ToLineColor();
                    }
                    else
                    {
                        return;
                    }
                    bezierLine = new ConnectionLineShape(LineType.Bezier,
                                                         myData.StartPoint,
                                                         myData.StartPoint,
                                                         brushColor,
                                                         isTop: true); // 绘制临时的线

                    Mouse.OverrideCursor = Cursors.Cross; // 设置鼠标为正在创建连线
                    myData.MyLine = new MyLine(canvas, bezierLine);
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
