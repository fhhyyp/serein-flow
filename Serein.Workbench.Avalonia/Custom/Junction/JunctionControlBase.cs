using Avalonia.Controls.Shapes;
using Avalonia.Controls;
using Avalonia.Media;
using Serein.Workbench.Avalonia.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.VisualTree;
using Serein.Library;

namespace Serein.Workbench.Avalonia.Custom.Junction
{
    public abstract class JunctionControlBase : Control 
    {
        protected JunctionControlBase()
        {
            this.Width = 25;
            this.Height = 20;
            this.PointerPressed += JunctionControlBase_PointerPressed;
            this.PointerMoved += JunctionControlBase_PointerMoved;
            this.PointerExited += JunctionControlBase_PointerExited;
        }

/*
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
        protected override Geometry DefiningGeometry => StreamGeometry;
*/
        protected readonly StreamGeometry StreamGeometry = new StreamGeometry();

        /// <summary>
        /// 重绘方法
        /// </summary>
        /// <param name="drawingContext"></param>
        public abstract void OnRender(DrawingContext drawingContext);
        /// <summary>
        /// 中心点
        /// </summary>
        public abstract Point MyCenterPoint { get; }



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
                if (_isMouseOver != value)
                {
                    //GlobalJunctionData.MyGlobalConnectingData.CurrentJunction = this;
                    _isMouseOver = value;
                    InvalidateVisual();
                }

            }
        }

        /// <summary>
        /// 控件重绘事件
        /// </summary>
        /// <param name="drawingContext"></param>
        public override void Render(DrawingContext drawingContext)
        {
            OnRender(drawingContext);
        }

        /// <summary>
        /// 获取背景颜色
        /// </summary>
        /// <returns></returns>
        protected Brush GetBackgrounp()
        {
            return (Brush)Brushes.Transparent;
            //var myData = GlobalJunctionData.MyGlobalConnectingData;
            //if (!myData.IsCreateing)
            //{
            //    return Brushes.Transparent;
            //}
            //if (IsMouseOver)
            //{
            //    if (myData.IsCanConnected)
            //    {
            //        if (myData.Type == JunctionOfConnectionType.Invoke)
            //        {
            //            return myData.ConnectionInvokeType.ToLineColor();
            //        }
            //        else
            //        {
            //            return myData.ConnectionArgSourceType.ToLineColor();
            //        }
            //    }
            //    else
            //    {
            //        return Brushes.Red;
            //    }
            //}
            //else
            //{
            //    return Brushes.Transparent;
            //}
        }

        private object lockObj = new object();


        /// <summary>
        ///  控件获得鼠标焦点事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void JunctionControlBase_PointerMoved(object? sender, global::Avalonia.Input.PointerEventArgs e)
        {
            //if (!GlobalJunctionData.MyGlobalConnectingData.IsCreateing) return;

            //if (IsMouseOver) return;
            IsMouseOver = true;

            this.InvalidateVisual();
        }

        /// <summary>
        /// 控件失去鼠标焦点事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void JunctionControlBase_PointerExited(object? sender, global::Avalonia.Input.PointerEventArgs e)
        {
            IsMouseOver = false;
            e.Handled = true;

        }

        /// <summary>
        /// 在碰撞点上按下鼠标控件开始进行移动
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void JunctionControlBase_PointerPressed(object? sender, global::Avalonia.Input.PointerPressedEventArgs e)
        {
            throw new NotImplementedException();
            //if (e.LeftButton == MouseButtonState.Pressed)
            //{
            //    var canvas = MainWindow.GetParentOfType<Canvas>(this);
            //    if (canvas != null)
            //    {
            //        var myData = GlobalJunctionData.MyGlobalConnectingData;
            //        myData.Reset();
            //        myData.IsCreateing = true; // 表示开始连接
            //        myData.StartJunction = this;
            //        myData.CurrentJunction = this;
            //        myData.StartPoint = this.TranslatePoint(new Point(this.Width / 2, this.Height / 2), canvas);

            //        var junctionOfConnectionType = this.JunctionType.ToConnectyionType();
            //        ConnectionLineShape bezierLine; // 类别
            //        Brush brushColor; // 临时线的颜色
            //        if (junctionOfConnectionType == JunctionOfConnectionType.Invoke)
            //        {
            //            brushColor = ConnectionInvokeType.IsSucceed.ToLineColor();
            //        }
            //        else if (junctionOfConnectionType == JunctionOfConnectionType.Arg)
            //        {
            //            brushColor = ConnectionArgSourceType.GetOtherNodeData.ToLineColor();
            //        }
            //        else
            //        {
            //            return;
            //        }
            //        bezierLine = new ConnectionLineShape(LineType.Bezier,
            //                                             myData.StartPoint,
            //                                             myData.StartPoint,
            //                                             brushColor,
            //                                             isTop: true); // 绘制临时的线

            //        Mouse.OverrideCursor = Cursors.Cross; // 设置鼠标为正在创建连线
            //        myData.MyLine = new MyLine(canvas, bezierLine);
            //    }
            //}
            //e.Handled = true;
        }

        /// <summary>
        /// 获取起始控制点
        /// </summary>
        /// <returns></returns>
        private Point GetStartPoint()
        {
            if (this.GetTransformedBounds() is TransformedBounds transformed)
            {
                var size = transformed.Bounds.Size;
                return new Point(size.Width / 2, size.Height / 2);  // 起始节点选择右侧边缘中心 
            }
            else
            {
                return new Point(0, 0);
            }
          
        }





    }


}
