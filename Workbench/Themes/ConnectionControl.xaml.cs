using Serein.Library;
using Serein.Workbench.Node.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Serein.Workbench.Themes
{
    /// <summary>
    /// ConnectionControl.xaml 的交互逻辑
    /// </summary>
    public partial class ConnectionControl : UserControl
    {
        public static readonly DependencyProperty StartProperty =
        DependencyProperty.Register("Start", typeof(NodeControlBase), typeof(ConnectionControl),
            new PropertyMetadata(null, OnConnectionChanged));

        public static readonly DependencyProperty EndProperty =
            DependencyProperty.Register("End", typeof(NodeControlBase), typeof(ConnectionControl),
                new PropertyMetadata(null, OnConnectionChanged));

        public static readonly DependencyProperty TypeProperty =
            DependencyProperty.Register("Type", typeof(ConnectionType), typeof(ConnectionControl),
                new PropertyMetadata(ConnectionType.IsSucceed, OnConnectionChanged));

        public NodeControlBase Start
        {
            get { return (NodeControlBase)GetValue(StartProperty); }
            set { SetValue(StartProperty, value); }
        }

        public NodeControlBase End
        {
            get { return (NodeControlBase)GetValue(EndProperty); }
            set { SetValue(EndProperty, value); }
        }

        public ConnectionType Type
        {
            get { return (ConnectionType)GetValue(TypeProperty); }
            set { SetValue(TypeProperty, value); }
        }

        private static void OnConnectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as ConnectionControl;
            control?.Refresh();
        }
        public ConnectionControl()
        {
            InitializeComponent();
        }
        public void Refresh()
        {
            if (Start == null || End == null || PART_Canvas == null)
                return;
            InitializePath();
            //BezierLineDrawer.UpdateBezierLine(PART_Canvas, Start, End, _bezierPath, _arrowPath);
        }

        private System.Windows.Shapes.Path _bezierPath;
        private System.Windows.Shapes.Path _arrowPath;

        private void InitializePath()
        {
            if (_bezierPath == null)
            {
                _bezierPath = new System.Windows.Shapes.Path { Stroke = BezierLineDrawer.GetLineColor(Type), StrokeThickness = 1 };
                PART_Canvas.Children.Add(_bezierPath);
            }
            if (_arrowPath == null)
            {
                _arrowPath = new System.Windows.Shapes.Path { Stroke = BezierLineDrawer.GetLineColor(Type), Fill = BezierLineDrawer.GetLineColor(Type), StrokeThickness = 1 };
                PART_Canvas.Children.Add(_arrowPath);
            }
        }
    }
}
