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
using System.Windows.Media.Media3D;
using System.Windows.Documents;
using System.Threading;
using Serein.Workbench.Services;
using Serein.Workbench.Tool;
using System.ComponentModel;
using System.Diagnostics;
using Serein.Library.Api;
using Serein.Workbench.Node.ViewModel;
using Serein.Workbench.Themes;

namespace Serein.Workbench.Node.View
{

    /// <summary>
    /// 控制带的拓展方法
    /// </summary>
    internal static class MyUIFunc
    {
        public static Pen CreateAndFreezePen()
        {
            // 创建Pen
            Pen pen = new Pen(Brushes.Black, 1);

            // 冻结Pen
            if (pen.CanFreeze)
            {
                pen.Freeze();
            }
            return pen;
        }
    }

    
    /// <summary>
    /// 入参控件
    /// </summary>
    internal class ParamsArgControl: Shape
    {
        public ParamsArgControl()
        {
            this.MouseDown += ParamsArg_OnMouseDown; // 增加或删除
            this.MouseMove += ParamsArgControl_MouseMove;
            this.MouseLeave += ParamsArgControl_MouseLeave;
            AddOrRemoveParamsAction = AddParamAsync;
        }

        

        protected readonly StreamGeometry StreamGeometry = new StreamGeometry();
        protected override Geometry DefiningGeometry => StreamGeometry;


        #region 控件属性，所在的节点
        public static readonly DependencyProperty NodeProperty =
            DependencyProperty.Register(nameof(MyNode), typeof(IFlowNode), typeof(ParamsArgControl), new PropertyMetadata(default(IFlowNode)));
        //public NodeModelBase NodeModel;

        /// <summary>
        /// 所在的节点
        /// </summary>
        public IFlowNode MyNode
        {
            get { return (IFlowNode)GetValue(NodeProperty); }
            set { SetValue(NodeProperty, value); }
        }
        #endregion

        #region 控件属性，连接器类型
        public static readonly DependencyProperty ArgIndexProperty =
            DependencyProperty.Register(nameof(ArgIndex), typeof(int), typeof(ParamsArgControl), new PropertyMetadata(default(int)));

        /// <summary>
        /// 参数的索引
        /// </summary>
        public int ArgIndex
        {
            get { return (int)GetValue(ArgIndexProperty); }
            set { SetValue(ArgIndexProperty, value.ToString()); }
        }
        #endregion


        /// <summary>
        /// 控件重绘事件
        /// </summary>
        /// <param name="drawingContext"></param>
        protected override void OnRender(DrawingContext drawingContext)
        {
            Brush brush = isMouseOver ? Brushes.Red : Brushes.Green;
            double height = ActualHeight;
            // 定义圆形的大小和位置
            double connectorSize = 10; // 连接器的大小
            double circleCenterX = 8; // 圆心 X 坐标
            double circleCenterY = height / 2; // 圆心 Y 坐标
            var circlePoint = new Point(circleCenterX, circleCenterY);

            // 圆形部分
            var ellipse = new EllipseGeometry(circlePoint, connectorSize / 2, connectorSize / 2);

            drawingContext.DrawGeometry(brush, MyUIFunc.CreateAndFreezePen(), ellipse);
        }
        

        private bool isMouseOver; // 鼠标悬停状态

        private Action AddOrRemoveParamsAction; // 增加或删除参数

        public void ParamsArg_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
           AddOrRemoveParamsAction.Invoke();
        }

        private void ParamsArgControl_MouseMove(object sender, MouseEventArgs e)
        {
            isMouseOver = true;
            if (cts.IsCancellationRequested) {
                cts = new CancellationTokenSource();
                Task.Run(async () =>
                {
                    await Task.Delay(380);

                }, cts.Token).ContinueWith((t) =>
                {
                    // 如果焦点仍在控件上时，则改变点击事件
                    if (isMouseOver)
                    {
                        AddOrRemoveParamsAction = RemoveParamAsync;
                        this.Dispatcher.Invoke(InvalidateVisual);// 触发一次重绘
                    }
                });
            }
            
        }


        private CancellationTokenSource cts = new CancellationTokenSource();


        private void ParamsArgControl_MouseLeave(object sender, MouseEventArgs e)
        {
            isMouseOver = false;
            AddOrRemoveParamsAction = AddParamAsync; // 鼠标焦点离开时恢复点击事件
            cts?.Cancel();
            this.Dispatcher.Invoke(InvalidateVisual);// 触发一次重绘

        }



        private void AddParamAsync() // 以当前选定入参连接器增加新的入参
        {
           this.MyNode.Env.FlowEdit.ChangeParameter(MyNode.Guid, true, ArgIndex);
        }
        private void RemoveParamAsync() // 移除当前选定的入参连接器
        {
           this.MyNode.Env.FlowEdit.ChangeParameter(MyNode.Guid, false, ArgIndex);
        }

    }



    internal abstract class JunctionControlBase : Shape 
    {
        private readonly FlowNodeService flowNodeService;
        protected JunctionControlBase()
        {
            
          
            this.Width = 25;
            this.Height = 20;
            this.MouseDown += JunctionControlBase_MouseDown;
            this.MouseMove += JunctionControlBase_MouseMove;
            this.MouseLeave += JunctionControlBase_MouseLeave;
#if DEBUG

            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                return;

#endif
            flowNodeService = App.GetService<FlowNodeService>();

        }


        #region 控件属性，所在的节点
       /* public static readonly DependencyProperty NodeProperty =
            DependencyProperty.Register(nameof(MyNode), typeof(IFlowNode), typeof(JunctionControlBase), new PropertyMetadata(default(IFlowNode)));

        public IFlowNode MyNode
        {
            get { return (IFlowNode)GetValue(NodeProperty); }
            set { SetValue(NodeProperty, value); }
        }
*/

        /// <summary>
        /// 对应的节点Control View Model
        /// </summary>
        public NodeControlViewModelBase NodeViewModel
        {
            get { return (NodeControlViewModelBase)GetValue(NodeViewModelProperty); }
            set { SetValue(NodeViewModelProperty, value); }
        }

        public static readonly DependencyProperty NodeViewModelProperty =
            DependencyProperty.Register(nameof(NodeViewModel), typeof(NodeControlViewModelBase), typeof(JunctionControlBase), new PropertyMetadata(default(IFlowNode)));


        /// <summary>
        /// 对应的节点Guid
        /// </summary>
        public string NodeGuid
        {
            get
            {
                return NodeViewModel?.NodeModel.Guid ?? string.Empty;
            }
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
        /// 处理鼠标悬停状态
        /// </summary>
        private bool _isMouseOver;
        public new bool IsMouseOver
        {
            get => _isMouseOver;
            set
            {
                if(_isMouseOver != value)
                {
                   
                    _isMouseOver = value;
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
            if(flowNodeService is null)
            {
                return Brushes.Transparent;
            }
            var cd = flowNodeService.ConnectingData;
            if(!cd.IsCreateing)
            {
                return Brushes.Transparent;
            }
            if (IsMouseOver)
            {
                if (cd.IsCanConnected)
                {
                    if (cd.Type == JunctionOfConnectionType.Invoke)
                    {
                        return cd.ConnectionInvokeType.ToLineColor();
                    }
                    else
                    {
                        return cd.ConnectionArgSourceType.ToLineColor();
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

        /// <summary>
        ///  控件获得鼠标焦点事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void JunctionControlBase_MouseMove(object sender, MouseEventArgs e)
        {
            //if (!GlobalJunctionData.MyGlobalConnectingData.IsCreateing) return;

            //if (IsMouseOver) return;
            IsMouseOver = true;

            flowNodeService.ConnectingData.CurrentJunction = this;
            InvalidateVisual();

            //Debug.WriteLine(NodeGuid);
            //this.InvalidateVisual();
        }

        /// <summary>
        /// 控件失去鼠标焦点事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void JunctionControlBase_MouseLeave(object sender, MouseEventArgs e)
        {
            IsMouseOver = false;
            e.Handled = true;
            flowNodeService.ConnectingData.CurrentJunction = this;
            InvalidateVisual();
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
                var canvas = WpfFuncTool.GetParentOfType<Canvas>(this); 
                if (canvas != null)
                {
                    var cd = flowNodeService.ConnectingData;
                    cd.Reset();
                    cd.IsCreateing = true; // 表示开始连接
                    cd.StartJunction = this;
                    cd.CurrentJunction = this;
                    cd.StartPoint = this.TranslatePoint(new Point(this.Width / 2, this.Height / 2), canvas);

                    var junctionOfConnectionType = this.JunctionType.ToConnectyionType();
                    //Debug.WriteLine(this.JunctionType);
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
                                                         cd.StartPoint,
                                                         cd.StartPoint,
                                                         brushColor,
                                                         isTop: true); // 绘制临时的线

                    Mouse.OverrideCursor = Cursors.Cross; // 设置鼠标为正在创建连线
                    cd.MyLine = new MyLine(canvas, bezierLine);
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
