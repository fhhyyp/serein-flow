using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;
using Serein.Workbench.Node.View;
using System.Windows.Controls;
using Serein.Library;
using System.Windows.Data;

namespace Serein.Workbench.Node.View
{


    public abstract class NodeJunctionViewBase : ContentControl, IDisposable
    {
        public NodeJunctionViewBase()
        {
            var transfromGroup = new TransformGroup();
            transfromGroup.Children.Add(_Translate);
            RenderTransform = transfromGroup;
        }

        /// <summary>
        /// 每个连接器都有一个唯一标识符（Guid），用于标识连接器。
        /// </summary>
        public Guid Guid
        {
            get => (Guid)GetValue(GuidProperty);
            set => SetValue(GuidProperty, value);
        }
        public static readonly DependencyProperty GuidProperty = DependencyProperty.Register(
            nameof(Guid),
            typeof(Guid),
            typeof(NodeJunctionViewBase), // NodeConnectorContent
            new PropertyMetadata(Guid.Empty));

        /// <summary>
        /// 连接器当前的连接数，表示有多少条 NodeLink 连接到此连接器。该属性为只读。
        /// </summary>
        public int ConnectedCount
        {
            get => (int)GetValue(ConnectedCountProperty);
            private set => SetValue(ConnectedCountPropertyKey, value);
        }
        public static readonly DependencyPropertyKey ConnectedCountPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(ConnectedCount),
            typeof(int),
            typeof(NodeJunctionViewBase), // NodeConnectorContent
            new PropertyMetadata(0));

        public static readonly DependencyProperty ConnectedCountProperty = ConnectedCountPropertyKey.DependencyProperty;

        /// <summary>
        /// 布尔值，指示此连接器是否有任何连接。
        /// </summary>
        public bool IsConnected
        {
            get => (bool)GetValue(IsConnectedProperty);
            private set => SetValue(IsConnectedPropertyKey, value);
        }
        public static readonly DependencyPropertyKey IsConnectedPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(IsConnected),
            typeof(bool),
            typeof(NodeJunctionViewBase), // NodeConnectorContent
            new PropertyMetadata(false));

        public static readonly DependencyProperty IsConnectedProperty = IsConnectedPropertyKey.DependencyProperty;

        /// <summary>
        /// 这些属性控制连接器的外观（颜色、边框厚度、填充颜色）。
        /// </summary>
        public Brush Stroke
        {
            get => (Brush)GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }
        public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
            nameof(Stroke),
            typeof(Brush),
            typeof(NodeJunctionViewBase),  // NodeConnectorContent
            new FrameworkPropertyMetadata(Brushes.Blue));

        /// <summary>
        /// 这些属性控制连接器的外观（颜色、边框厚度、填充颜色）。
        /// </summary>
        public double StrokeThickness
        {
            get => (double)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }
        public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
            nameof(StrokeThickness),
            typeof(double),
            typeof(NodeJunctionViewBase),  // NodeConnectorContent
            new FrameworkPropertyMetadata(1.0));

        /// <summary>
        /// 这些属性控制连接器的外观（颜色、边框厚度、填充颜色）。
        /// </summary>
        public Brush Fill
        {
            get => (Brush)GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }
        public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
            nameof(Fill),
            typeof(Brush),
            typeof(NodeJunctionViewBase),// NodeConnectorContent
            new FrameworkPropertyMetadata(Brushes.Gray));

        /// <summary>
        /// 指示该连接器是否可以与其他连接器进行连接。
        /// </summary>
        public bool CanConnect
        {
            get => (bool)GetValue(CanConnectProperty);
            set => SetValue(CanConnectProperty, value);
        }
        public static readonly DependencyProperty CanConnectProperty = DependencyProperty.Register(
            nameof(CanConnect),
            typeof(bool),
            typeof(NodeJunctionViewBase),// NodeConnectorContent
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));


        private Point _Position = new Point();
        /// <summary>
        ///  该连接器的当前坐标（位置）。
        /// </summary>
        public Point Position
        {
            get => _Position;
            set => UpdatePosition(value);
        }

        /// <summary>
        /// (重要数据)表示连接器所属的节点。
        /// </summary>
        public NodeModelBase NodeModel { get; private set; } = null;

        /// <summary>
        ///  该连接器所连接的所有 NodeLink 的集合。
        /// </summary>
        public IEnumerable<ConnectionControl> NodeLinks => _NodeLinks;
        List<ConnectionControl> _NodeLinks = new List<ConnectionControl>();

        protected abstract FrameworkElement ConnectorControl { get; }
        TranslateTransform _Translate = new TranslateTransform();
        void UpdatePosition(Point pos)
        {
            _Position = pos;
            _Translate.X = _Position.X;
            _Translate.Y = _Position.Y;

            InvalidateVisual();
        }

        /// <summary>
        /// 将 NodeLink 添加到连接器，并更新 ConnectedCount 和 IsConnected。
        /// </summary>
        /// <param name="nodeLink"></param>
        public void Connect(ConnectionControl nodeLink)
        {
            _NodeLinks.Add(nodeLink);
            ConnectedCount = _NodeLinks.Count;
            IsConnected = ConnectedCount > 0;
        }

        /// <summary>
        /// 断开与某个 NodeLink 的连接，更新连接状态。
        /// </summary>
        /// <param name="nodeLink"></param>
        public void Disconnect(ConnectionControl nodeLink)
        {
            _NodeLinks.Remove(nodeLink);
            ConnectedCount = _NodeLinks.Count;
            IsConnected = ConnectedCount > 0;
        }

        /// <summary>
        /// 获取连接器相对于指定 Canvas 的位置。
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="xScaleOffset"></param>
        /// <param name="yScaleOffset"></param>
        /// <returns></returns>
        public Point GetContentPosition(Canvas canvas, double xScaleOffset = 0.5, double yScaleOffset = 0.5)
        {
            // it will be shifted Control position if not called UpdateLayout().
            ConnectorControl.UpdateLayout();
            var transformer = ConnectorControl.TransformToVisual(canvas);

            var x = ConnectorControl.ActualWidth * xScaleOffset;
            var y = ConnectorControl.ActualHeight * yScaleOffset;
            return transformer.Transform(new Point(x, y));
        }

        /// <summary>
        /// 更新与此连接器相连的所有 NodeLink 的位置。这个方法是抽象的，要求子类实现。
        /// </summary>
        /// <param name="canvas"></param>
        public abstract void UpdateLinkPosition(Canvas canvas);

        /// <summary>
        /// 用于检查此连接器是否可以与另一个连接器相连接，要求子类实现。
        /// </summary>
        /// <param name="connector"></param>
        /// <returns></returns>
        public abstract bool CanConnectTo(NodeJunctionViewBase connector);

        /// <summary>
        /// 释放连接器相关的资源，包括样式、绑定和已连接的 NodeLink
        /// </summary>
        public void Dispose()
        {
            // You need to clear Style.
            // Because implemented on style for binding.
            Style = null;

            // Clear binding for subscribing source changed event from old control.
            // throw exception about visual tree ancestor different if you not clear binding.
            BindingOperations.ClearAllBindings(this);

            var nodeLinks = _NodeLinks.ToArray();

            // it must instance to nodeLinks because change node link collection in NodeLink Dispose.
            foreach (var nodeLink in nodeLinks)
            {
                // nodeLink.Dispose();
            }
        }

    }
}
