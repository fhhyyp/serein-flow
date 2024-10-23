using Serein.Library;
using Serein.Library.Utils;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Serein.Workbench.Node.View
{



    public abstract class JunctionControlBase : UserControl
    {

        public double _MyWidth = 20;
        public double _MyHeight = 20;

        protected JunctionControlBase()
        {
            //this.Width = 20;
            //this.Height = 20;
            this.MouseDown += ControlPointBase_MouseDown;
            this.MouseMove += ControlPointBase_MouseMove; ;
        }
        #region 控件属性，所在的节点
        public static readonly DependencyProperty NodeGuidProperty =
       DependencyProperty.Register("NodeGuid", typeof(string), typeof(JunctionControlBase), new PropertyMetadata(default(string)));

        /// <summary>
        /// 所在的节点
        /// </summary>
        public string NodeGuid
        {
            get { return (string)GetValue(NodeGuidProperty); }
            set { SetValue(NodeGuidProperty, value.ToString()); }
        }
        #endregion

        #region 控件属性，连接器类型
        public static readonly DependencyProperty JunctionTypeProperty =
        DependencyProperty.Register("JunctionType", typeof(string), typeof(JunctionControlBase), new PropertyMetadata(default(string)));

        public JunctionType JunctionType
        {
            get { return EnumHelper.ConvertEnum<JunctionType>(GetValue(JunctionTypeProperty).ToString()); }
            set { SetValue(JunctionTypeProperty, value.ToString()); }
        } 
        #endregion

        

        public abstract void Render();
        private void ControlPointBase_MouseMove(object sender, MouseEventArgs e)
        {
            if (GlobalJunctionData.MyGlobalData is null) return;
            GlobalJunctionData.MyGlobalData.ChangingJunction = this;
        }

        /// <summary>
        /// 在碰撞点上按下鼠标控件开始进行移动
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ControlPointBase_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                GlobalJunctionData.MyGlobalData = new ConnectingData();
                var myDataType = GlobalJunctionData.MyGlobalData;
                myDataType.StartJunction = this;
                var canvas = MainWindow.GetParentOfType<Canvas>(this);
                myDataType.StartPoint = this.TranslatePoint(new Point(this.Width /2 , this.Height /2 ), canvas);
                myDataType.VirtualLine = new MyLine(canvas, new Line // 虚拟线
                {
                    Stroke = Brushes.OldLace,
                    StrokeThickness = 2
                });
               
            }
            e.Handled = true;
        }
    }

   


   


}
