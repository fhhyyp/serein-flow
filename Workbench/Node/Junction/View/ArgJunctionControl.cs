using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Serein.Library;

namespace Serein.Workbench.Node.View
{
    public class ArgJunctionControl : JunctionControlBase
    {
        public ArgJunctionControl()
        {
            base.JunctionType = JunctionType.ArgData;
            Render();
        }

        #region 控件属性，对应的参数
        public static readonly DependencyProperty ArgIndexProperty =
            DependencyProperty.Register("ArgIndex", typeof(int), typeof(ArgJunctionControl), new PropertyMetadata(default(int)));

        /// <summary>
        /// 所在的节点
        /// </summary>
        public int ArgIndex
        {
            get { return (int)GetValue(ArgIndexProperty); }
            set { SetValue(ArgIndexProperty, value); }
        }
        #endregion

        public override void Render()
        {
            if(double.IsNaN(base.Width))
            {
                base.Width = base._MyWidth;
            }
            if (double.IsNaN(base.Height))
            {
                base.Height = base._MyHeight;
            }


            var ellipse = new Ellipse
            {
                Width = base.Width,
                Height = base.Height,
                Fill = Brushes.Orange,
                ToolTip = "入参"
            };
            Content = ellipse;
        }
    }


}
