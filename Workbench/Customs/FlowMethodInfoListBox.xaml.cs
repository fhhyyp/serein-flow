using Serein.Library;
using Serein.Library.Utils;
using Serein.Workbench.Models;
using Serein.Workbench.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace Serein.Workbench.Customs
{

    /// <summary>
    /// 拖拽创建节点类型
    /// </summary>
    public static class MouseNodeType
    {
        /// <summary>
        /// 创建来自DLL的节点
        /// </summary>
        public static string CreateDllNodeInCanvas { get; } = nameof(CreateDllNodeInCanvas);
        /// <summary>
        /// 创建基础节点
        /// </summary>
        public static string CreateBaseNodeInCanvas { get; } = nameof(CreateBaseNodeInCanvas);
    }

    /// <summary>
    /// FlowMethodInfoListBox.xaml 的交互逻辑
    /// </summary>
    public partial class FlowMethodInfoListBox :  UserControl, System.ComponentModel.INotifyPropertyChanged
    {
        private object? viewMethodInfo;

        /// <summary>
        /// 当前选中的方法信息
        /// </summary>
        public object? ViewMethodInfo
        {
            get => viewMethodInfo;
            set
            {
                if (viewMethodInfo != value)
                {
                    viewMethodInfo = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ViewMethodInfo)));
                }
            }
        }

        /// <summary>
        /// 属性改变事件，用于通知绑定的UI更新
        /// </summary>
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// FlowMethodInfoListBox 的构造函数
        /// </summary>
        public FlowMethodInfoListBox()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 依赖属性，用于绑定方法信息列表
        /// </summary>
        public static readonly DependencyProperty ItemsSourceProperty =
          DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(FlowMethodInfoListBox), new PropertyMetadata(null));

        /// <summary>
        /// 获取或设置方法信息列表
        /// </summary>
        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        /// <summary>
        /// 依赖属性，用于设置背景颜色
        /// </summary>
        public new static readonly DependencyProperty BackgroundProperty =
            DependencyProperty.Register(nameof(Background), typeof(Brush), typeof(FlowMethodInfoListBox), new PropertyMetadata(Brushes.Transparent));

        /// <summary>
        /// 获取或设置背景颜色
        /// </summary>
        public new Brush Background
        {
            get => (Brush)GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }


        /// <summary>
        /// 存储拖拽开始时的鼠标位置
        /// </summary>
        private Point _dragStartPoint;

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

            // 记录鼠标按下时的位置
            _dragStartPoint = e.GetPosition(null);
        }

        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            // 获取当前鼠标位置
            Point mousePos = e.GetPosition(null);
            // 计算鼠标移动的距离
            Vector diff = _dragStartPoint - mousePos;

            // 判断是否符合拖拽的最小距离要求
            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                // 获取触发事件的 TextBlock

                if (sender is Grid grid && grid.DataContext is MethodDetailsInfo mdInfo)
                {
                    if (!EnumHelper.TryConvertEnum<Library.NodeType>(mdInfo.NodeType, out var nodeType))
                    {
                        return;
                    }

                    MoveNodeModel moveNodeModel = new MoveNodeModel()
                    {
                        NodeControlType = nodeType switch
                        {
                            NodeType.Action => NodeControlType.Action,
                            NodeType.Flipflop => NodeControlType.Flipflop,
                            NodeType.UI => NodeControlType.UI,
                            _ => NodeControlType.None,
                        },
                        MethodDetailsInfo = mdInfo
                    };
                    //MoveNodeData moveNodeData = new MoveNodeData
                    //{

                       
                    //    MethodDetailsInfo = mdInfo,
                    //};
                    if (moveNodeModel.NodeControlType == NodeControlType.None)
                    {
                        return;
                    }

                    // 创建一个 DataObject 用于拖拽操作，并设置拖拽效果
                    DataObject dragData = new DataObject(MouseNodeType.CreateDllNodeInCanvas, moveNodeModel);
                    DragDrop.DoDragDrop(grid, dragData, DragDropEffects.Move);
                }
            }
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(sender is ListBox listBox)
            {
                if (listBox.SelectedIndex != -1)
                {
                    var item = listBox.SelectedItem;
                    if (item is MethodDetailsInfo mdInfo)
                    {
                        App.GetService<FlowNodeService>().CurrentMethodDetailsInfo = mdInfo;
                    }
                }
                // Serein.Workbench.Models.FlowLibraryInfo
            }
        }
    }
}
