using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Serein.Library;
using Serein.Library.Api;
using Serein.NodeFlow.Env;
using Serein.Workbench.Api;
using Serein.Workbench.Customs;
using Serein.Workbench.Extension;
using Serein.Workbench.Models;
using Serein.Workbench.Node;
using Serein.Workbench.Node.View;
using Serein.Workbench.Services;
using Serein.Workbench.Themes;
using Serein.Workbench.Tool;
using Serein.Workbench.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Binding = System.Windows.Data.Binding;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using UserControl = System.Windows.Controls.UserControl;
using Clipboard = System.Windows.Clipboard;
using TextDataFormat = System.Windows.TextDataFormat;
using System.Windows.Media.Animation;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Services;

namespace Serein.Workbench.Views
{
    /// <summary>
    /// FlowCanvasView.xaml 的交互逻辑
    /// </summary>
    public partial class FlowCanvasView : UserControl, IFlowCanvas
    {
        
        private readonly IFlowEnvironment flowEnvironment;
        private readonly IKeyEventService keyEventService;
        private readonly FlowNodeService flowNodeService;
        private readonly IFlowEEForwardingService flowEEForwardingService;

        /// <summary>
        /// 存储所有的连接。考虑集成在运行环境中。
        /// </summary>
        private List<ConnectionControl> Connections { get; } = [];
        /// <summary>
        /// 画布模型
        /// </summary>
        public FlowCanvasViewModel ViewModel => this.DataContext as FlowCanvasViewModel ?? throw new ArgumentNullException();


        #region 与画布相关的字段

        /// <summary>
        /// 标记是否正在尝试选取控件
        /// </summary>
        private bool IsSelectControl;
        /// <summary>
        /// 标记是否正在拖动控件
        /// </summary>
        private bool IsControlDragging;
        /// <summary>
        /// 标记是否正在拖动画布
        /// </summary>
        private bool IsCanvasDragging;
        /// <summary>
        /// 是否正在选取控件
        /// </summary>
        private bool IsSelectDragging;

        /// <summary>
        /// 当前选取的控件
        /// </summary>
        private readonly List<NodeControlBase> selectNodeControls = [];

        /// <summary>
        /// 记录开始拖动节点控件时的鼠标位置
        /// </summary>
        private Point startControlDragPoint;
        /// <summary>
        /// 记录移动画布开始时的鼠标位置
        /// </summary>
        private Point startCanvasDragPoint;
        /// <summary>
        /// 记录开始选取节点控件时的鼠标位置
        /// </summary>
        private Point startSelectControolPoint;

        /// <summary>
        /// 组合变换容器
        /// </summary>
        private readonly TransformGroup canvasTransformGroup;
        /// <summary>
        /// 缩放画布
        /// </summary>
        private readonly ScaleTransform scaleTransform;
        /// <summary>
        /// 平移画布 
        /// </summary>
        private readonly TranslateTransform translateTransform;
        #endregion

        #region 初始化画布与相关事件

        /// <summary>
        /// FlowCanvasView 构造函数
        /// </summary>
        /// <param name="model"></param>
        public FlowCanvasView(FlowCanvasDetails model)
        {
            var vm = App.GetService<Locator>().FlowCanvasViewModel;
            vm.Model = model;
            this.DataContext = vm;
            InitializeComponent();


            flowEnvironment = App.GetService<IFlowEnvironment>();
            flowNodeService = App.GetService<FlowNodeService>();
            keyEventService = App.GetService<IKeyEventService>();
            flowEEForwardingService = App.GetService<IFlowEEForwardingService>();
           

            //flowEEForwardingService.OnProjectLoaded += FlowEEForwardingService_OnProjectLoaded;

            // 缩放平移容器
            canvasTransformGroup = new TransformGroup();
            scaleTransform = new ScaleTransform();
            translateTransform = new TranslateTransform();
            canvasTransformGroup.Children.Add(scaleTransform);
            canvasTransformGroup.Children.Add(translateTransform);
            FlowChartCanvas.RenderTransform = canvasTransformGroup;
            SetBinding(model);
            InitEvent();


        }

        /// <summary>
        /// 设置绑定
        /// </summary>
        /// <param name="canvasModel"></param>
        private void SetBinding(FlowCanvasDetails canvasModel)
        {
            Binding bindingScaleX = new(nameof(canvasModel.ScaleX)) { Source = canvasModel, Mode = BindingMode.TwoWay };
            BindingOperations.SetBinding(scaleTransform, ScaleTransform.ScaleXProperty, bindingScaleX);

            Binding bindingScaleY = new(nameof(canvasModel.ScaleY)) { Source = canvasModel, Mode = BindingMode.TwoWay };
            BindingOperations.SetBinding(scaleTransform, ScaleTransform.ScaleYProperty, bindingScaleY);

            Binding bindingX = new(nameof(canvasModel.ViewX)) { Source = canvasModel, Mode = BindingMode.TwoWay };
            BindingOperations.SetBinding(translateTransform, TranslateTransform.XProperty, bindingX);

            Binding bindingY = new(nameof(canvasModel.ViewY)) { Source = canvasModel, Mode = BindingMode.TwoWay };
            BindingOperations.SetBinding(translateTransform, TranslateTransform.YProperty, bindingY);

        }

        private void InitEvent()
        {
            keyEventService.OnKeyDown += KeyEventService_OnKeyDown;
            flowEEForwardingService.NodeLocated += FlowEEForwardingService_OnNodeLocated;
        }

        private void FlowNodeService_OnRemoveConnectionLine(NodeConnectChangeEventArgs e)
        {
            if(e.ChangeType == NodeConnectChangeEventArgs.ConnectChangeType.Create || e.CanvasGuid != this.Guid)
            {
                return;
            }

            var connectionControl = Connections.FirstOrDefault(c =>
            {
                if (c.Start.NodeGuid != e.FromNodeGuid
                || c.End.NodeGuid != e.ToNodeGuid)
                {
                    return false; // 不是当前连接
                }
                var jct1 = c.Start.JunctionType.ToConnectyionType();
                var jct2 = c.End.JunctionType.ToConnectyionType();
                if (e.JunctionOfConnectionType == JunctionOfConnectionType.Invoke)
                {
                    if (jct1 == JunctionOfConnectionType.Invoke 
                        && jct2 == JunctionOfConnectionType.Invoke)
                    {
                        return true; // 是当前连接
                    }
                }
                else
                {
                    if (c.ArgIndex == e.ArgIndex 
                        && jct1 == JunctionOfConnectionType.Arg
                        && jct2 == JunctionOfConnectionType.Arg)
                    {
                        return true; // 是当前连接
                    }
                }
                return true;
            });
            if(connectionControl is null)
            {
                return;
            }
            connectionControl.RemoveOnCanvas(); // 移除连接线
        }

        /// <summary>
        /// 节点需要定位
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEEForwardingService_OnNodeLocated(NodeLocatedEventArgs eventArgs)
        {
            if(!ViewModel.NodeControls.TryGetValue(eventArgs.NodeGuid,out var nodeControl))
            {
                return;
            }
            /*if (nodeControl.FlowCanvas.Guid.Equals(Guid)) // 防止事件传播到其它画布
            {
                return;
            }*/

            // 获取控件在 FlowChartCanvas 上的相对位置
#if false
            Rect controlBounds = VisualTreeHelper.GetDescendantBounds(nodeControl);
            Point controlPosition = nodeControl.TransformToAncestor(FlowChartCanvas).Transform(new Point(0, 0));

            // 获取控件在画布上的中心点
            double controlCenterX = controlPosition.X + controlBounds.Width / 2;
            double controlCenterY = controlPosition.Y + controlBounds.Height / 2;

            // 考虑缩放因素计算目标位置的中心点
            double scaledCenterX = controlCenterX * scaleTransform.ScaleX;
            double scaledCenterY = controlCenterY * scaleTransform.ScaleY;

            // 计算平移偏移量，使得控件在可视区域的中心
            double translateX = scaledCenterX - this.FlowChartStackPanel.ActualWidth / 2;
            double translateY = scaledCenterY - FlowChartStackPanel.ActualHeight / 2;

            var translate = this.translateTransform;
            // 应用平移变换
            translate.X = 0;
            translate.Y = 0;
            translate.X -= translateX;
            translate.Y -= translateY; 
#endif

            // 设置RenderTransform以实现移动效果
            TranslateTransform translateTransform = new TranslateTransform();
            nodeControl.RenderTransform = translateTransform;
            ElasticAnimation(nodeControl, translateTransform, 6, 0.5, 0.5);
        }

        /// <summary>
        /// 控件抖动
        /// 来源：https://www.cnblogs.com/RedSky/p/17705411.html
        /// 作者：HotSky
        /// </summary>
        /// <param name="translate"></param>
        /// <param name="nodeControl">需要抖动的控件</param>
        /// <param name="power">抖动第一下偏移量</param>
        /// <param name="range">减弱幅度（小于等于power，大于0）</param>
        /// <param name="speed">持续系数(大于0)，越大时间越长，</param>
        private static void ElasticAnimation(NodeControlBase nodeControl, TranslateTransform translate, double power, double range = 1, double speed = 1)
        {
            DoubleAnimationUsingKeyFrames animation1 = new DoubleAnimationUsingKeyFrames();
            for (double i = power, j = 1; i >= 0; i -= range)
            {
                animation1.KeyFrames.Add(new LinearDoubleKeyFrame(-i, TimeSpan.FromMilliseconds(j++ * 100 * speed)));
                animation1.KeyFrames.Add(new LinearDoubleKeyFrame(i, TimeSpan.FromMilliseconds(j++ * 100 * speed)));
            }
            translate.BeginAnimation(TranslateTransform.YProperty, animation1);
            DoubleAnimationUsingKeyFrames animation2 = new DoubleAnimationUsingKeyFrames();
            for (double i = power, j = 1; i >= 0; i -= range)
            {
                animation2.KeyFrames.Add(new LinearDoubleKeyFrame(-i, TimeSpan.FromMilliseconds(j++ * 100 * speed)));
                animation2.KeyFrames.Add(new LinearDoubleKeyFrame(i, TimeSpan.FromMilliseconds(j++ * 100 * speed)));
            }
            translate.BeginAnimation(TranslateTransform.XProperty, animation2);

            animation2.Completed += (s, e) =>
            {
                nodeControl.RenderTransform = null; // 或者重新设置为默认值
            };
        }

        /// <summary>
        /// 加载完成后刷新显示
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FlowEEForwardingService_OnProjectLoaded(ProjectLoadedEventArgs eventArgs)
        {
            RefreshAllLine();
        }


        /// <summary>
        ///  尝试判断是否为区域，如果是，将节点放置在区域中
        /// </summary>
        /// <param name="nodeControl"></param>
        /// <param name="position"></param>
        /// <param name="targetNodeControl">目标节点控件</param>
        /// <returns></returns>
        private bool TryPlaceNodeInRegion(NodeControlBase nodeControl,
                                          PositionOfUI position,
                                          out NodeControlBase targetNodeControl)
        {
            var point = new Point(position.X, position.Y);
            HitTestResult hitTestResult = VisualTreeHelper.HitTest(FlowChartCanvas, point);
            if (hitTestResult != null && hitTestResult.VisualHit is UIElement hitElement)
            {
                // 准备放置条件表达式控件
                if (nodeControl.ViewModel.NodeModel.ControlType == NodeControlType.ExpCondition)
                {
                    /* ConditionRegionControl? conditionRegion =  WpfFuncTool.GetParentOfType<ConditionRegionControl>(hitElement);
                     if (conditionRegion is not null)
                     {
                         targetNodeControl = conditionRegion;
                         //// 如果存在条件区域容器
                         //conditionRegion.AddCondition(nodeControl);
                         return true;
                     }*/
                }

                else
                {
                    // 准备放置全局数据控件
                    GlobalDataControl? globalDataControl = WpfFuncTool.GetParentOfType<GlobalDataControl>(hitElement);
                    if (globalDataControl is not null)
                    {
                        targetNodeControl = globalDataControl;
                        return true;
                    }
                }
            }
            targetNodeControl = null;
            return false;
        } 
        #endregion

        #region 画布节点操作接口实现
        private IFlowCanvas Api => this;

        /// <inheritdoc/>
        public string Guid => ViewModel.Model.Guid;

        /// <inheritdoc/>
        public new string Name => ViewModel.Model.Name;
        FlowCanvasDetails IFlowCanvas.Model => ViewModel.Model;
        void IFlowCanvas.Remove(NodeControlBase nodeControl)
        {
            ViewModel.NodeControls.Remove(nodeControl.ViewModel.NodeModel.Guid);
            FlowChartCanvas.Dispatcher.Invoke(() =>
            {
                FlowChartCanvas.Children.Remove(nodeControl);
            });
        }
        void IFlowCanvas.Add(NodeControlBase nodeControl)
        {
            var p = nodeControl.ViewModel.NodeModel.Position;
            PositionOfUI position = new PositionOfUI(p.X, p.Y);
            if (TryPlaceNodeInRegion(nodeControl, position, out var regionControl)) // 判断添加到区域容器
            {
                // 通知运行环境调用加载节点子项的方法
               flowEnvironment.FlowEdit.PlaceNodeToContainer(Guid,
                                                  nodeControl.ViewModel.NodeModel.Guid, // 待移动的节点
                                                  regionControl.ViewModel.NodeModel.Guid); // 目标的容器节点
                return;
            }

            // 并非添加在容器中，直接放置节点

            ViewModel.NodeControls.TryAdd(nodeControl.ViewModel.NodeModel.Guid, nodeControl);

            FlowChartCanvas.Dispatcher.Invoke(() =>
            {

                FlowChartCanvas.Children.Add(nodeControl);

                if (nodeControl.ViewModel.NodeModel.ControlType == NodeControlType.UI)
                {
                    // 需要切换到对应画布，尽可能让UI线程获取到适配器
                    var edit = App.GetService<Locator>().FlowEditViewModel;
                    var tab = edit.CanvasTabs.First(tab => tab.Content == this);
                    App.GetService<Locator>().FlowEditViewModel.SelectedTab = tab;

                }
            });
            ConfigureNodeEvents(nodeControl); // 配置相关事件
            ConfigureContextMenu(nodeControl); // 添加右键菜单

        }

        void IFlowCanvas.CreateInvokeConnection(NodeControlBase fromNodeControl, NodeControlBase toNodeControl, ConnectionInvokeType type)
        {
            if (fromNodeControl is not INodeJunction IFormJunction || toNodeControl is not INodeJunction IToJunction)
            {
                SereinEnv.WriteLine(InfoType.INFO, "非预期的连接");
                return;
            }
            JunctionControlBase startJunction = IFormJunction.NextStepJunction;
            JunctionControlBase endJunction = IToJunction.ExecuteJunction;


            var connection = new ConnectionControl(
                       FlowChartCanvas,
                       type,
                       startJunction,
                       endJunction
                   );

            //if (toNodeControl is FlipflopNodeControl flipflopControl
            //    && flipflopControl?.ViewModel?.NodeModel is NodeModelBase nodeModel) // 某个节点连接到了触发器，尝试从全局触发器视图中移除该触发器
            //{
            //    NodeTreeViewer.RemoveGlobalFlipFlop(nodeModel); // 从全局触发器树树视图中移除
            //}

            Connections.Add(connection);
            fromNodeControl.AddCnnection(connection);
            toNodeControl.AddCnnection(connection);
            EndConnection(); // 环境触发了创建节点连接事件


        }
        void IFlowCanvas.RemoveInvokeConnection(NodeControlBase fromNodeControl, NodeControlBase toNodeControl)
        {
            if (fromNodeControl is not INodeJunction IFormJunction || toNodeControl is not INodeJunction IToJunction)
            {
                SereinEnv.WriteLine(InfoType.INFO, "非预期的连接");
                return;
            }
            JunctionControlBase startJunction = IFormJunction.NextStepJunction;
            JunctionControlBase endJunction = IToJunction.ExecuteJunction;

            var removeConnections = Connections.Where(c =>
                                               c.Start.Equals(startJunction)
                                            && c.End.Equals(endJunction)
                                            && (c.Start.JunctionType.ToConnectyionType() == JunctionOfConnectionType.Invoke
                                            || c.End.JunctionType.ToConnectyionType() == JunctionOfConnectionType.Invoke))
                                            .ToList();


            foreach (var connection in removeConnections)
            {
                Connections.Remove(connection);
                fromNodeControl.RemoveConnection(connection); // 移除连接
                toNodeControl.RemoveConnection(connection); // 移除连接

                //if (NodeControls.TryGetValue(connection.End.MyNode.Guid, out var control))
                //{
                //    JudgmentFlipFlopNode(control); // 连接关系变更时判断
                //}
            }
        }
        void IFlowCanvas.CreateArgConnection(NodeControlBase fromNodeControl, NodeControlBase toNodeControl, ConnectionArgSourceType type, int index)
        {
            if (fromNodeControl is not INodeJunction IFormJunction || toNodeControl is not INodeJunction IToJunction)
            {
                SereinEnv.WriteLine(InfoType.INFO, "非预期的情况");
                return;
            }

            JunctionControlBase startJunction = type switch
            {
                ConnectionArgSourceType.GetPreviousNodeData => IFormJunction.ReturnDataJunction, // 自身节点
                ConnectionArgSourceType.GetOtherNodeData => IFormJunction.ReturnDataJunction, // 其它节点的返回值控制点
                ConnectionArgSourceType.GetOtherNodeDataOfInvoke => IFormJunction.ReturnDataJunction, // 其它节点的返回值控制点
                _ => throw new Exception("窗体事件 FlowEnvironment_NodeConnectChangeEvemt 创建/删除节点之间的参数传递关系 JunctionControlBase 枚举值错误 。非预期的枚举值。") // 应该不会触发
            };

            if (IToJunction.ArgDataJunction.Length <= index)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100);
                    await App.UIContextOperation.InvokeAsync(() =>
                    {
                        Api.CreateArgConnection(fromNodeControl, toNodeControl, type, index);
                    });
                });
                return; // // 尝试重新连接
            }
            JunctionControlBase endJunction = IToJunction.ArgDataJunction[index];
            LineType lineType = LineType.Bezier;
            // 添加连接
            var connection = new ConnectionControl(
                lineType,
                FlowChartCanvas,
                index,
                type,
                startJunction,
                endJunction,
                IToJunction
            );
            Connections.Add(connection);
            fromNodeControl.AddCnnection(connection);
            toNodeControl.AddCnnection(connection);
            EndConnection(); // 环境触发了创建节点连接事件
        }
        void IFlowCanvas.RemoveArgConnection(NodeControlBase fromNodeControl, NodeControlBase toNodeControl, int index)
        {
            if (fromNodeControl is not INodeJunction IFormJunction || toNodeControl is not INodeJunction IToJunction)
            {
                SereinEnv.WriteLine(InfoType.INFO, "非预期的连接");
                return;
            }
            JunctionControlBase startJunction = IFormJunction.NextStepJunction;
            JunctionControlBase endJunction = IToJunction.ExecuteJunction;

            var removeConnections = Connections.Where(c =>
                                               c.Start.NodeGuid == startJunction.NodeGuid
                                            && c.End.NodeGuid == endJunction.NodeGuid
                                            && (c.Start.JunctionType.ToConnectyionType() == JunctionOfConnectionType.Arg
                                            || c.End.JunctionType.ToConnectyionType() == JunctionOfConnectionType.Arg))
                                            .ToList();


            foreach (var connection in removeConnections)
            {
                Connections.Remove(connection);
                fromNodeControl.RemoveConnection(connection); // 移除连接
                toNodeControl.RemoveConnection(connection); // 移除连接
                //if (NodeControls.TryGetValue(connection.End.MyNode.Guid, out var control))
                //{
                //    JudgmentFlipFlopNode(control); // 连接关系变更时判断
                //}
            }
        }

        #endregion

        #region 画布键盘操作
        /// <summary>
        /// 监听按键事件
        /// </summary>
        /// <param name="key"></param>
        private void KeyEventService_OnKeyDown(Key key)
        {
            
            if (flowNodeService.CurrentSelectCanvas is null || !flowNodeService.CurrentSelectCanvas.Guid.Equals(Guid))
            {
                return;
            }


            if (key == Key.F5)
            {
                if (keyEventService.GetKeyState(Key.LeftCtrl) || keyEventService.GetKeyState(Key.RightCtrl))
                {
                    // Ctrl + F5 调试当前流程
                    Task.Run(() =>
                    {
                        flowEnvironment.FlowControl.StartFlowAsync([flowNodeService.CurrentSelectCanvas.Guid]);
                    });
                }
                else if (selectNodeControls.Count == 1 )
                {
                    // F5 调试当前选定节点
                    var nodeModel = selectNodeControls[0].ViewModel.NodeModel;
                    SereinEnv.WriteLine(InfoType.INFO, $"调试运行当前节点:{nodeModel.Guid}");
                    Task.Run(() =>
                    {
                        flowEnvironment.FlowControl.StartFlowAsync<FlowResult>(nodeModel.Guid);
                    });
                    //_ = nodeModel.StartFlowAsync(new DynamicContext(flowEnvironment), new CancellationToken());
                }

            }


            if (key == Key.Escape)
            {
                // 退出连线、选取状态
                IsControlDragging = false;
                IsCanvasDragging = false;
                SelectionRectangle.Visibility = Visibility.Collapsed;
                CancelSelectNode();
                EndConnection(); // Esc 按键 退出连线状态
                return;
            }

            // 复制节点
            if (selectNodeControls.Count > 0 && key == Key.C && (keyEventService.GetKeyState(Key.LeftCtrl) || keyEventService.GetKeyState(Key.RightCtrl))) 
            {
                var text = flowNodeService.CpoyNodeInfo([.. selectNodeControls.Select(c => c.ViewModel.NodeModel)]);
                //Clipboard.SetText(text); // 复制，持久性设置
                try
                {
                    Clipboard.SetDataObject(text, true); // 复制，持久性设置
                }
                catch 
                {
                    return;
                }
            }
            
            // 粘贴节点
            if (key == Key.V && (keyEventService.GetKeyState(Key.LeftCtrl) || keyEventService.GetKeyState(Key.RightCtrl))) 
            {
                string clipboardText = Clipboard.GetText(TextDataFormat.Text); // 获取复制的文本
                string? nodesText = "";
                try
                {
                    var jobject = JObject.Parse(clipboardText);
                    nodesText = jobject["nodes"]?.ToString();
                }
                catch (Exception ex)
                {
                    SereinEnv.WriteLine(InfoType.ERROR, $"粘贴节点信息失败: {ex.Message}");
                    return;
                }
                
                if (!string.IsNullOrWhiteSpace(nodesText))
                {
                    
                    if (Clipboard.ContainsText())
                    {
                        Point mousePosition = Mouse.GetPosition(FlowChartCanvas);
                        PositionOfUI positionOfUI = new PositionOfUI(mousePosition.X, mousePosition.Y); // 坐标数据
                        flowNodeService.PasteNodeInfo(Guid, nodesText, positionOfUI); // 粘贴节点信息
                    }
                    else if (Clipboard.ContainsImage())
                    {
                        // var image = Clipboard.GetImage();
                    }
                    else
                    {
                        SereinEnv.WriteLine(InfoType.INFO, "剪贴板中没有可识别的数据。");
                    }
                }
                return;
            }

            var cd = flowNodeService.ConnectingData;
            if (cd.IsCreateing)
            {
                if (cd.Type == JunctionOfConnectionType.Invoke)
                {
                    ConnectionInvokeType connectionInvokeType = key switch
                    {
                        Key.D1 => ConnectionInvokeType.Upstream,
                        Key.D2 => ConnectionInvokeType.IsSucceed,
                        Key.D3 => ConnectionInvokeType.IsFail,
                        Key.D4 => ConnectionInvokeType.IsError,
                        _ => ConnectionInvokeType.None,
                    };

                    if (connectionInvokeType != ConnectionInvokeType.None)
                    {
                        cd.ConnectionInvokeType = connectionInvokeType;
                        cd.MyLine.Line.UpdateLineColor(connectionInvokeType.ToLineColor());
                    }
                }
                else if (cd.Type == JunctionOfConnectionType.Arg)
                {
                    ConnectionArgSourceType connectionArgSourceType = key switch
                    {
                        Key.D1 => ConnectionArgSourceType.GetOtherNodeData,
                        Key.D2 => ConnectionArgSourceType.GetOtherNodeDataOfInvoke,
                        _ => ConnectionArgSourceType.GetPreviousNodeData,
                    };

                    if (connectionArgSourceType != ConnectionArgSourceType.GetPreviousNodeData)
                    {
                        cd.ConnectionArgSourceType = connectionArgSourceType;
                        cd.MyLine.Line.UpdateLineColor(connectionArgSourceType.ToLineColor());
                    }
                }
                cd.CurrentJunction.InvalidateVisual(); // 刷新目标节点控制点样式

            }
        }
        
        #endregion

        #region 画布鼠标操作


        /// <summary>
        /// 鼠标在画布移动。
        /// 选择控件状态下，调整选择框大小
        /// 连接状态下，实时更新连接线的终点位置。
        /// 移动画布状态下，移动画布。
        /// </summary>
        private void FlowChartCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var cd = flowNodeService.ConnectingData;
            if (cd.IsCreateing && e.LeftButton == MouseButtonState.Pressed)
            {

                if (cd.Type == JunctionOfConnectionType.Invoke)
                {
                    ViewModel.IsConnectionInvokeNode = true; // 正在连接节点的调用关系

                }
                else
                {
                    ViewModel.IsConnectionArgSourceNode = true; // 正在连接节点的调用关系
                }
                var currentPoint = e.GetPosition(FlowChartCanvas);
                currentPoint.X -= 2;
                currentPoint.Y -= 2;
                cd.UpdatePoint(currentPoint);
                return;
            }


            if (IsCanvasDragging && e.MiddleButton == MouseButtonState.Pressed) // 正在移动画布（按住中键） 
            {
                Point currentMousePosition = e.GetPosition(this);
                double deltaX = currentMousePosition.X - startCanvasDragPoint.X;
                double deltaY = currentMousePosition.Y - startCanvasDragPoint.Y;

                translateTransform.X += deltaX;
                translateTransform.Y += deltaY;

                startCanvasDragPoint = currentMousePosition;

                RefreshAllLine();
            }

            if (IsSelectControl) // 正在选取节点
            {
                IsSelectDragging = e.LeftButton == MouseButtonState.Pressed;
                // 获取当前鼠标位置
                Point currentPoint = e.GetPosition(FlowChartCanvas);

                // 更新选取矩形的位置和大小
                double x = Math.Min(currentPoint.X, startSelectControolPoint.X);
                double y = Math.Min(currentPoint.Y, startSelectControolPoint.Y);
                double width = Math.Abs(currentPoint.X - startSelectControolPoint.X);
                double height = Math.Abs(currentPoint.Y - startSelectControolPoint.Y);

                Canvas.SetLeft(SelectionRectangle, x);
                Canvas.SetTop(SelectionRectangle, y);
                SelectionRectangle.Width = width;
                SelectionRectangle.Height = height;

            }
        }

        /// <summary>
        /// 放置操作，根据拖放数据创建相应的控件，并处理相关操作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlowChartCanvas_Drop(object sender, DragEventArgs e)
        {
            try
            {
                var canvasDropPosition = e.GetPosition(FlowChartCanvas); // 更新画布落点
                PositionOfUI position = new PositionOfUI(canvasDropPosition.X, canvasDropPosition.Y);
                if (e.Data.GetDataPresent(MouseNodeType.CreateDllNodeInCanvas))
                {
                    if (e.Data.GetData(MouseNodeType.CreateDllNodeInCanvas) is MoveNodeModel nodeModel)
                    {
                        flowNodeService.CurrentNodeControlType = nodeModel.NodeControlType; // 设置基础节点类型
                        flowNodeService.CurrentMethodDetailsInfo = nodeModel.MethodDetailsInfo; // 基础节点不需要参数信息
                        flowNodeService.CurrentMouseLocation = position; // 设置当前鼠标为止
                        flowNodeService.CreateNode(); // 创建来自DLL加载的方法节点
                    }
                }
                else if (e.Data.GetDataPresent(MouseNodeType.CreateBaseNodeInCanvas))
                {
                    if (e.Data.GetData(MouseNodeType.CreateBaseNodeInCanvas) is Type droppedType)
                    {
                        NodeControlType nodeControlType = droppedType switch
                        {
                            //Type when typeof(ConditionRegionControl).IsAssignableFrom(droppedType) => NodeControlType.ConditionRegion, // 条件区域
                            Type when typeof(ConditionNodeControl).IsAssignableFrom(droppedType) => NodeControlType.ExpCondition,
                            Type when typeof(ExpOpNodeControl).IsAssignableFrom(droppedType) => NodeControlType.ExpOp,
                            Type when typeof(GlobalDataControl).IsAssignableFrom(droppedType) => NodeControlType.GlobalData,
                            Type when typeof(ScriptNodeControl).IsAssignableFrom(droppedType) => NodeControlType.Script,
                            Type when typeof(NetScriptNodeControl).IsAssignableFrom(droppedType) => NodeControlType.NetScript,
                            Type when typeof(FlowCallNodeControl).IsAssignableFrom(droppedType) => NodeControlType.FlowCall,
                            _ => NodeControlType.None,
                        };
                        if (nodeControlType != NodeControlType.None)
                        {
                            flowNodeService.CurrentNodeControlType = nodeControlType; // 设置基础节点类型
                            flowNodeService.CurrentMethodDetailsInfo = null; // 基础节点不需要参数信息
                            flowNodeService.CurrentMouseLocation = position; // 设置当前鼠标为止
                            flowNodeService.CreateNode(); // 创建基础节点

                        }


                    }
                }
                e.Handled = true;
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(ex);
            }
        }

        /// <summary>
        /// 拖动效果，根据拖放数据是否为指定类型设置拖放效果
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlowChartCanvas_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(MouseNodeType.CreateDllNodeInCanvas)
                || e.Data.GetDataPresent(MouseNodeType.CreateBaseNodeInCanvas))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        /// <summary>
        /// 在画布中尝试选取控件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlowChartCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

            if (flowNodeService.ConnectingData.IsCreateing)
            {
                return;
            }
            if (!IsSelectControl)
            {
                // 进入选取状态
                IsSelectControl = true;
                IsSelectDragging = false; // 初始化为非拖动状态

                // 记录鼠标起始点
                startSelectControolPoint = e.GetPosition(FlowChartCanvas);

                // 初始化选取矩形的位置和大小
                Canvas.SetLeft(SelectionRectangle, startSelectControolPoint.X);
                Canvas.SetTop(SelectionRectangle, startSelectControolPoint.Y);
                SelectionRectangle.Width = 0;
                SelectionRectangle.Height = 0;

                // 显示选取矩形
                SelectionRectangle.Visibility = Visibility.Visible;
                SelectionRectangle.ContextMenu ??= ConfiguerSelectionRectangle();

                // 捕获鼠标，以便在鼠标移动到Canvas外部时仍能处理事件
                FlowChartCanvas.CaptureMouse();
            }
            else
            {
                // 如果已经是选取状态，单击则认为结束框选
                CompleteSelection();
            }

            e.Handled = true; // 防止事件传播影响其他控件
        }

        /// <summary>
        /// 在画布中释放鼠标按下，结束选取状态 / 停止创建连线，尝试连接节点
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlowChartCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsSelectControl)
            {
                // 松开鼠标时判断是否为拖动操作
                if (IsSelectDragging)
                {
                    // 完成拖动框选
                    CompleteSelection();
                }

                // 释放鼠标捕获
                FlowChartCanvas.ReleaseMouseCapture();
            }

            // 创建连线
            var cd = flowNodeService.ConnectingData;
            if (cd.IsCreateing && cd.CurrentJunction is not null)
            {
                if (cd.IsCanConnected)
                {
                    var canvas = this.FlowChartCanvas;
                    var currentendPoint = e.GetPosition(canvas); // 当前鼠标落点
                    var changingJunctionPosition = cd.CurrentJunction.TranslatePoint(new Point(0, 0), canvas);
                    var changingJunctionRect = new Rect(changingJunctionPosition, new Size(cd.CurrentJunction.Width, cd.CurrentJunction.Height));

                    if (changingJunctionRect.Contains(currentendPoint)) // 可以创建连接
                    {
                        #region 方法调用关系创建
                        if (cd.Type == JunctionOfConnectionType.Invoke)
                        {
                            var canvasGuid = this.Guid;

                             flowEnvironment.FlowEdit.ConnectInvokeNode(
                                        canvasGuid,
                                        cd.StartJunction.NodeGuid,
                                        cd.CurrentJunction.NodeGuid,
                                        cd.StartJunction.JunctionType,
                                        cd.CurrentJunction.JunctionType,
                                        cd.ConnectionInvokeType);
                        }
                        #endregion

                        #region 参数来源关系创建
                        else if (cd.Type == JunctionOfConnectionType.Arg)
                        {
                            var canvasGuid = this.Guid;
                            var argIndex = 0;
                            var startNodeGuid = "";
                            var toNodeGuid = "";
                            if (cd.StartJunction is ArgJunctionControl argJunction1)
                            {
                                startNodeGuid = cd.CurrentJunction.NodeGuid;
                                argIndex = argJunction1.ArgIndex;
                                toNodeGuid = argJunction1.NodeGuid;
                            }
                            else if (cd.CurrentJunction is ArgJunctionControl argJunction2)
                            {
                                startNodeGuid = cd.StartJunction.NodeGuid;
                                startNodeGuid = cd.StartJunction.NodeGuid;
                                argIndex = argJunction2.ArgIndex;
                                toNodeGuid = argJunction2.NodeGuid;
                            }


                            flowEnvironment.FlowEdit.ConnectArgSourceNode(
                                    canvasGuid,
                                    startNodeGuid,
                                    toNodeGuid,
                                    JunctionType.ReturnData,
                                    JunctionType.ArgData,
                                    cd.ConnectionArgSourceType,
                                    argIndex);
                        }
                        #endregion
                    }
                    EndConnection(); // 完成创建连线请求后取消连线
                }

            }
            e.Handled = true;

        }


        #region 拖动画布实现缩放平移效果

        /// <summary>
        /// 开始拖动画布
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlowChartCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            IsCanvasDragging = true;
            startCanvasDragPoint = e.GetPosition(this);
            FlowChartCanvas.CaptureMouse();
            e.Handled = true; // 防止事件传播影响其他控件
        }

       /// <summary>
       /// 停止拖动
       /// </summary>
       /// <param name="sender"></param>
       /// <param name="e"></param>
        private void FlowChartCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (IsCanvasDragging)
            {
                IsCanvasDragging = false;
                FlowChartCanvas.ReleaseMouseCapture();
            }
        }

        /// <summary>
        /// 单纯缩放画布，不改变画布大小
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlowChartCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (e.Delta < 0 && scaleTransform.ScaleX < 0.05) return;
                if (e.Delta > 0 && scaleTransform.ScaleY > 2.0) return;
                // 获取鼠标在 Canvas 内的相对位置
                var mousePosition = e.GetPosition(FlowChartCanvas);

                // 缩放因子，根据滚轮方向调整
                //double zoomFactor = e.Delta > 0 ? 0.1 : -0.1;
                double zoomFactor = e.Delta > 0 ? 1.1 : 0.9;

                // 当前缩放比例
                double oldScale = scaleTransform.ScaleX;
                double newScale = oldScale * zoomFactor;
                //double newScale = oldScale + zoomFactor;
                // 更新缩放比例
                scaleTransform.ScaleX = newScale;
                scaleTransform.ScaleY = newScale;

                // 计算缩放前后鼠标相对于 Canvas 的位置差异
                // double offsetX = mousePosition.X - (mousePosition.X * zoomFactor);
                // double offsetY = mousePosition.Y - (mousePosition.Y * zoomFactor);

                // 更新 TranslateTransform，确保以鼠标位置为中心进行缩放
                translateTransform.X -= (mousePosition.X * (newScale - oldScale));
                translateTransform.Y -= (mousePosition.Y * (newScale - oldScale));
            }
        }

        // 设置画布宽度高度
        private void InitializeCanvas(double width, double height)
        {
            FlowChartCanvas.Width = width;
            FlowChartCanvas.Height = height;
        }


        #region 动态调整区域大小
        //private void Thumb_DragDelta_TopLeft(object sender, DragDeltaEventArgs e)
        //{
        //    // 从左上角调整大小
        //    double newWidth = Math.Max(FlowChartCanvas.ActualWidth - e.HorizontalChange, 0);
        //    double newHeight = Math.Max(FlowChartCanvas.ActualHeight - e.VerticalChange, 0);

        //    FlowChartCanvas.Width = newWidth;
        //    FlowChartCanvas.Height = newHeight;

        //    Canvas.SetLeft(FlowChartCanvas, Canvas.GetLeft(FlowChartCanvas) + e.HorizontalChange);
        //    Canvas.SetTop(FlowChartCanvas, Canvas.GetTop(FlowChartCanvas) + e.VerticalChange);
        //}

        //private void Thumb_DragDelta_TopRight(object sender, DragDeltaEventArgs e)
        //{
        //    // 从右上角调整大小
        //    double newWidth = Math.Max(FlowChartCanvas.ActualWidth + e.HorizontalChange, 0);
        //    double newHeight = Math.Max(FlowChartCanvas.ActualHeight - e.VerticalChange, 0);

        //    FlowChartCanvas.Width = newWidth;
        //    FlowChartCanvas.Height = newHeight;

        //    Canvas.SetTop(FlowChartCanvas, Canvas.GetTop(FlowChartCanvas) + e.VerticalChange);
        //}

        //private void Thumb_DragDelta_BottomLeft(object sender, DragDeltaEventArgs e)
        //{
        //    // 从左下角调整大小
        //    double newWidth = Math.Max(FlowChartCanvas.ActualWidth - e.HorizontalChange, 0);
        //    double newHeight = Math.Max(FlowChartCanvas.ActualHeight + e.VerticalChange, 0);

        //    FlowChartCanvas.Width = newWidth;
        //    FlowChartCanvas.Height = newHeight;

        //    Canvas.SetLeft(FlowChartCanvas, Canvas.GetLeft(FlowChartCanvas) + e.HorizontalChange);
        //}

        private void Thumb_DragDelta_BottomRight(object sender, DragDeltaEventArgs e)
        {
            // 获取缩放后的水平和垂直变化
            double horizontalChange = e.HorizontalChange * scaleTransform.ScaleX;
            double verticalChange = e.VerticalChange * scaleTransform.ScaleY;

            // 计算新的宽度和高度，确保不会小于400
            double newWidth = Math.Max(FlowChartCanvas.ActualWidth + horizontalChange, 400);
            double newHeight = Math.Max(FlowChartCanvas.ActualHeight + verticalChange, 400);

            newHeight = newHeight < 400 ? 400 : newHeight;
            newWidth = newWidth < 400 ? 400 : newWidth;

            InitializeCanvas(newWidth, newHeight);

            //// 从右下角调整大小
            //double newWidth = Math.Max(FlowChartCanvas.ActualWidth + e.HorizontalChange * scaleTransform.ScaleX, 0);
            //double newHeight = Math.Max(FlowChartCanvas.ActualHeight + e.VerticalChange * scaleTransform.ScaleY, 0);

            //newWidth = newWidth < 400 ? 400 : newWidth;
            //newHeight = newHeight < 400 ? 400 : newHeight;

            //if (newWidth > 400 && newHeight > 400)
            //{
            //    FlowChartCanvas.Width = newWidth;
            //    FlowChartCanvas.Height = newHeight;

            //    double x = e.HorizontalChange > 0 ? -0.5 : 0.5;
            //    double y = e.VerticalChange > 0 ? -0.5 : 0.5;

            //    double deltaX = x * scaleTransform.ScaleX;
            //    double deltaY = y * scaleTransform.ScaleY;
            //    Test(deltaX, deltaY);
            //}
        }

        //private void Thumb_DragDelta_Left(object sender, DragDeltaEventArgs e)
        //{
        //    // 从左侧调整大小
        //    double newWidth = Math.Max(FlowChartCanvas.ActualWidth - e.HorizontalChange, 0);

        //    FlowChartCanvas.Width = newWidth;
        //    Canvas.SetLeft(FlowChartCanvas, Canvas.GetLeft(FlowChartCanvas) + e.HorizontalChange);
        //}

        private void Thumb_DragDelta_Right(object sender, DragDeltaEventArgs e)
        {
            //从右侧调整大小
            // 获取缩放后的水平变化
            double horizontalChange = e.HorizontalChange * scaleTransform.ScaleX;

            // 计算新的宽度，确保不会小于400
            double newWidth = Math.Max(FlowChartCanvas.ActualWidth + horizontalChange, 400);

            newWidth = newWidth < 400 ? 400 : newWidth;
            InitializeCanvas(newWidth, FlowChartCanvas.Height);

        }

        //private void Thumb_DragDelta_Top(object sender, DragDeltaEventArgs e)
        //{
        //    // 从顶部调整大小
        //    double newHeight = Math.Max(FlowChartCanvas.ActualHeight - e.VerticalChange, 0);

        //    FlowChartCanvas.Height = newHeight;
        //    Canvas.SetTop(FlowChartCanvas, Canvas.GetTop(FlowChartCanvas) + e.VerticalChange);
        //}

        private void Thumb_DragDelta_Bottom(object sender, DragDeltaEventArgs e)
        {
            // 获取缩放后的垂直变化
            double verticalChange = e.VerticalChange * scaleTransform.ScaleY;
            // 计算新的高度，确保不会小于400
            double newHeight = Math.Max(FlowChartCanvas.ActualHeight + verticalChange, 400);
            newHeight = newHeight < 400 ? 400 : newHeight;
            InitializeCanvas(FlowChartCanvas.Width, newHeight);
        }


        #endregion
        #endregion
        #endregion

        #region 私有方法

        /// <summary>
        /// 刷新画布所有连线
        /// </summary>
        public void RefreshAllLine()
        {
            foreach (var line in Connections)
            {
                line.RefreshLine(); 
            }
        }

        /// <summary>
        /// 完成选取操作
        /// </summary>
        private void CompleteSelection()
        {
            IsSelectControl = false;

            // 隐藏选取矩形
            SelectionRectangle.Visibility = Visibility.Collapsed;

            // 获取选取范围
            Rect selectionArea = new Rect(Canvas.GetLeft(SelectionRectangle),
                                          Canvas.GetTop(SelectionRectangle),
                                          SelectionRectangle.Width,
                                          SelectionRectangle.Height);

            // 处理选取范围内的控件
            // selectNodeControls.Clear();
            foreach (UIElement element in FlowChartCanvas.Children)
            {
                Rect elementBounds = new Rect(Canvas.GetLeft(element), Canvas.GetTop(element),
                                              element.RenderSize.Width, element.RenderSize.Height);

                if (selectionArea.Contains(elementBounds))
                {
                    if (element is NodeControlBase control)
                    {
                        if (!selectNodeControls.Contains(control))
                        {
                            selectNodeControls.Add(control);
                        }
                    }
                }
            }

            // 选中后的操作
            SelectedNode();
        }

        /// <summary>
        /// 选择控件
        /// </summary>
        private void SelectedNode()
        {

            if (selectNodeControls.Count == 0)
            {
                //Console.WriteLine($"没有选择控件");
                SelectionRectangle.Visibility = Visibility.Collapsed;
                return;
            }
            if (selectNodeControls.Count == 1)
            {
                var nodeConotrol = selectNodeControls[0]; 
                // 选取了控件
                flowNodeService.CurrentSelectNodeControl = nodeConotrol; // 更新选取节点显示
                // App.GetService<FlowNodeService>().CurrentMethodDetailsInfo = nodeConotrol.ViewModel.NodeModel.MethodDetails.ToInfo();
                // ChangeViewerObjOfNode(selectNodeControls[0]);
            }

            //Console.WriteLine($"一共选取了{selectNodeControls.Count}个控件");
            foreach (var node in selectNodeControls)
            {
                //node.ViewModel.IsSelect =true;
                // node.ViewModel.CancelSelect();
                node.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC700"));
                node.BorderThickness = new Thickness(4);
            }
        }


        private void CancelSelectNode()
        {
            IsSelectControl = false;
            foreach (var nodeControl in selectNodeControls)
            {
                //nodeControl.ViewModel.IsSelect = false;
                nodeControl.BorderBrush = Brushes.Black;
                nodeControl.BorderThickness = new Thickness(0);

                var startNode = ViewModel.Model.StartNode;
                var canvasStartNode = nodeControl.ViewModel.NodeModel;
                if (canvasStartNode.Equals(startNode))
                {
                    nodeControl.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#04FC10"));
                    nodeControl.BorderThickness = new Thickness(2);
                }
            }
            selectNodeControls.Clear();
        }

        /// <summary>
        /// 结束连接操作，清理状态并移除虚线。
        /// </summary>
        private void EndConnection()
        {
            Mouse.OverrideCursor = null; // 恢复视觉效果
            ViewModel.IsConnectionArgSourceNode = false;
            ViewModel.IsConnectionInvokeNode = false;
            flowNodeService.ConnectingData.Reset();
        }


        /// <summary>
        /// 选择范围配置
        /// </summary>
        /// <returns></returns>
        private ContextMenu ConfiguerSelectionRectangle()
        {
            var contextMenu = new ContextMenu();
            /*contextMenu.Items.Add(WpfFuncTool.CreateMenuItem("删除", (s, e) =>
            {
                if (selectNodeControls.Count > 0)
                {
                    foreach (var node in selectNodeControls.ToArray())
                    {
                        var guid = node?.ViewModel?.NodeModel?.Guid;
                        if (!string.IsNullOrEmpty(guid))
                        {
                            var canvasGuid = this.Guid;
                            flowEnvironment.FlowEdit.RemoveNode(canvasGuid, guid);
                        }
                    }
                }
                SelectionRectangle.Visibility = Visibility.Collapsed;
            }));*/
            return contextMenu;
            // nodeControl.ContextMenu = contextMenu;
        }



        #endregion

        #region 节点控件相关事件
        /// <summary>
        /// 配置节点事件(移动，点击相关）
        /// </summary>
        /// <param name="nodeControl"></param>
        private void ConfigureNodeEvents(NodeControlBase nodeControl)
        {
            
            nodeControl.MouseLeftButtonDown += Block_MouseLeftButtonDown;
            nodeControl.MouseMove += Block_MouseMove;
            nodeControl.MouseLeftButtonUp += Block_MouseLeftButtonUp;
            


        }

        /// <summary>
        /// 控件的鼠标左键按下事件，启动拖动操作。同时显示当前正在传递的数据。
        /// </summary>
        private void Block_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is NodeControlBase nodeControl)
            {
                //ChangeViewerObjOfNode(nodeControl); // 对象树
                //if (nodeControl?.ViewModel?.NodeModel?.DebugSetting.IsProtectionParameter == true) return;
                IsControlDragging = true;
                startControlDragPoint = e.GetPosition(FlowChartCanvas); // 记录鼠标按下时的位置
                ((UIElement)sender).CaptureMouse(); // 捕获鼠标
            }
                e.Handled = true; // 防止事件传播影响其他控件
        }

        /// <summary>
        /// 控件的鼠标移动事件，根据鼠标拖动更新控件的位置。批量移动计算移动逻辑。
        /// </summary>
        private void Block_MouseMove(object sender, MouseEventArgs e)
        {
            
            if (IsCanvasDragging)
                return;
            if (IsSelectControl)
                return;

            if(sender is not NodeControlBase nodeControlMain)
            {
                return;
            }

            flowNodeService.CurrentSelectNodeControl = nodeControlMain;

            if (IsControlDragging && !flowNodeService.ConnectingData.IsCreateing) // 如果正在拖动控件
            {
                Point currentPosition = e.GetPosition(FlowChartCanvas); // 获取当前鼠标位置 

                if (selectNodeControls.Count > 0 && selectNodeControls.Contains(nodeControlMain))
                {
                    // 进行批量移动
                    // 获取旧位置
                    var oldLeft = Canvas.GetLeft(nodeControlMain);
                    var oldTop = Canvas.GetTop(nodeControlMain);

                    // 计算被选择控件的偏移量
                    var deltaX = /*(int)*/(currentPosition.X - startControlDragPoint.X);
                    var deltaY = /*(int)*/(currentPosition.Y - startControlDragPoint.Y);

                    // 移动被选择的控件
                    var newLeft = oldLeft + deltaX;
                    var newTop = oldTop + deltaY;

                    // 计算控件实际移动的距离
                    var actualDeltaX = newLeft - oldLeft;
                    var actualDeltaY = newTop - oldTop;

                    List<(IFlowNode Node, double NewLeft, double NewTop, double MaxWidth, double MaxHeight)> moveSizes  =
                        selectNodeControls.Select(control =>
                            (control.ViewModel.NodeModel,
                            Canvas.GetLeft(control) + actualDeltaX, 
                            Canvas.GetTop(control) + actualDeltaY, 
                            control.FlowCanvas.Model.Width - control.ActualWidth - 10,
                            control.FlowCanvas.Model.Height - control.ActualHeight - 10)).ToList();

                    var isNeedCancel = moveSizes.Exists(item => item.NewLeft < 5 || item.NewTop < 5|| item.NewLeft > item.MaxWidth || item.NewTop > item.MaxHeight);
                    if (isNeedCancel)
                    {
                        return;
                    }
                    foreach (var item in moveSizes)
                    {
                        item.Node.Position.X = item.NewLeft;
                        item.Node.Position.Y = item.NewTop;

                        //this.flowEnvironment.MoveNode(this.Guid, item.Guid, item.NewLeft, item.NewTop); // 移动节点
                    }

                    // 更新节点之间线的连接位置
                    foreach (var nodeControl in selectNodeControls)
                    {
                        nodeControl.UpdateLocationConnections();
                    }
                }
                else
                {   // 单个节点移动
                    double deltaX = currentPosition.X - startControlDragPoint.X; // 计算X轴方向的偏移量
                    double deltaY = currentPosition.Y - startControlDragPoint.Y; // 计算Y轴方向的偏移量
                    double newLeft = Canvas.GetLeft(nodeControlMain) + deltaX; // 新的左边距
                    double newTop = Canvas.GetTop(nodeControlMain) + deltaY; // 新的上边距

                    // 如果被移动的控件接触到画布边缘，则限制移动范围
                    var canvasModel = nodeControlMain.FlowCanvas.Model;
                    var canvasWidth = canvasModel.Width - nodeControlMain.ActualWidth - 10;
                    var canvasHeight= canvasModel.Height - nodeControlMain.ActualHeight - 10;
                    newLeft = newLeft < 5 ? 5 : newLeft > canvasWidth ? canvasWidth : newLeft;
                    newTop = newTop < 5 ? 5 : newTop > canvasHeight ? canvasHeight : newTop;

                    var node = nodeControlMain.ViewModel.NodeModel;
                    node.Position.X = newLeft;
                    node.Position.Y = newTop;

                    //this.flowEnvironment.MoveNode(Guid, nodeControl.ViewModel.NodeModel.Guid, newLeft, newTop); // 移动节点
                    nodeControlMain.UpdateLocationConnections();
                }
                startControlDragPoint = currentPosition; // 更新起始点位置
            }

        }

        /// <summary>
        /// 控件的鼠标左键松开事件，结束拖动操作
        /// </summary>
        private void Block_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsControlDragging)
            {
                IsControlDragging = false;
                ((UIElement)sender).ReleaseMouseCapture();  // 释放鼠标捕获

            }

            //if (IsConnecting)
            //{
            //    var formNodeGuid = startConnectNodeControl?.ViewModel.NodeModel.Guid;
            //    var toNodeGuid = (sender as NodeControlBase)?.ViewModel.NodeModel.Guid;
            //    if (string.IsNullOrEmpty(formNodeGuid) || string.IsNullOrEmpty(toNodeGuid))
            //    {
            //        return;
            //    }
            //    env.ConnectNodeAsync(formNodeGuid, toNodeGuid,0,0, currentConnectionType);
            //}
            //GlobalJunctionData.OK();
        }



        #endregion

        #region 配置节点右键菜单

        /// <summary>
        /// 配置节点右键菜单
        /// </summary>
        /// <param name="nodeControl">
        /// <para> 任何情景下都尽量避免直接修改 ViewModel 中的 NodeModel 节点实体相关数据。</para>
        /// <para> 而是应该调用 FlowEnvironment 提供接口进行操作。</para> 
        /// <para> 因为 Workbench 应该更加关注UI视觉效果，而非直接干扰流程环境运行的逻辑。</para>
        /// <para> 之所以暴露 NodeModel 属性，因为有些场景下不可避免的需要直接获取节点的属性。</para> 
        /// </param>
        private void ConfigureContextMenu(NodeControlBase nodeControl)
        {
            /*if(nodeControl.ViewModel.NodeModel.ControlType == NodeControlType.UI)
            {
                return;
            }*/
            var canvasGuid = Guid;
            var contextMenu = new ContextMenu();
            var nodeGuid = nodeControl.ViewModel?.NodeModel?.Guid;
            #region 触发器节点

            if (nodeControl.ViewModel?.NodeModel.ControlType == NodeControlType.Flipflop)
            {
                contextMenu.Items.Add(WpfFuncTool.CreateMenuItem("启动触发器", (s, e) =>
                {
                    if (s is MenuItem menuItem)
                    {
                        if (menuItem.Header.ToString() == "启动触发器")
                        {
                            flowEnvironment.FlowControl.ActivateFlipflopNode(nodeGuid);

                            menuItem.Header = "终结触发器";
                        }
                        else
                        {
                            flowEnvironment.FlowControl.TerminateFlipflopNode(nodeGuid);
                            menuItem.Header = "启动触发器";

                        }
                    }
                }));
            }

            #endregion

            if (nodeControl.ViewModel?.NodeModel?.MethodDetails?.ReturnType is Type returnType && returnType != typeof(void))
            {
                contextMenu.Items.Add(WpfFuncTool.CreateMenuItem("查看返回类型", (s, e) =>
                {
                    DisplayReturnTypeTreeViewer(returnType);
                }));
            }



            contextMenu.Items.Add(WpfFuncTool.CreateMenuItem("设为起点", (s, e) => flowEnvironment.FlowEdit.SetStartNode(canvasGuid, nodeGuid)));
            contextMenu.Items.Add(WpfFuncTool.CreateMenuItem("删除",  (s, e) =>
            {
                flowNodeService.RemoteNode(nodeControl);
            }));

            #region 右键菜单功能 - 控件对齐

            var AvoidMenu = new MenuItem();
            AvoidMenu.Items.Add(WpfFuncTool.CreateMenuItem("群组对齐", (s, e) =>
            {
                AlignControlsWithGrouping(selectNodeControls, AlignMode.Grouping);
            }));
            AvoidMenu.Items.Add(WpfFuncTool.CreateMenuItem("规划对齐", (s, e) =>
            {
                AlignControlsWithGrouping(selectNodeControls, AlignMode.Planning);
            }));
            AvoidMenu.Items.Add(WpfFuncTool.CreateMenuItem("水平中心对齐", (s, e) =>
            {
                AlignControlsWithGrouping(selectNodeControls, AlignMode.HorizontalCenter);
            }));
            AvoidMenu.Items.Add(WpfFuncTool.CreateMenuItem("垂直中心对齐 ", (s, e) =>
            {
                AlignControlsWithGrouping(selectNodeControls, AlignMode.VerticalCenter);
            }));

            AvoidMenu.Items.Add(WpfFuncTool.CreateMenuItem("垂直对齐时水平斜分布", (s, e) =>
            {
                AlignControlsWithGrouping(selectNodeControls, AlignMode.Vertical);
            }));
            AvoidMenu.Items.Add(WpfFuncTool.CreateMenuItem("水平对齐时垂直斜分布", (s, e) =>
            {
                AlignControlsWithGrouping(selectNodeControls, AlignMode.Horizontal);
            }));

            AvoidMenu.Header = "对齐";
            contextMenu.Items.Add(AvoidMenu);


            #endregion

            nodeControl.ContextMenu = contextMenu;
        }

        private void EmptyContextMenu(NodeControlBase nodeControl)
        {
            nodeControl.ContextMenu.Items.Clear();
        }

        /// <summary>
        /// 查看返回类型（树形结构展开类型的成员）
        /// </summary>
        /// <param name="type"></param>
        private void DisplayReturnTypeTreeViewer(Type type)
        {
            try
            {
                var typeViewerWindow = new TypeViewerWindow
                {
                    Type = type,
                };
                typeViewerWindow.LoadTypeInformation();
                typeViewerWindow.Show();
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(ex);
            }
        }
        #endregion

        #region 节点对齐 （有些小瑕疵）




        #region Plan A 群组对齐

        /// <summary>
        /// 对齐选中的控件，支持多种对齐方式。
        /// </summary>
        /// <param name="selectNodeControls"></param>
        /// <param name="proximityThreshold"></param>
        /// <param name="spacing"></param>
        public void AlignControlsWithGrouping(List<NodeControlBase> selectNodeControls, double proximityThreshold = 50, double spacing = 10)
        {
            if (selectNodeControls is null || selectNodeControls.Count < 2)
                return;

            // 按照控件的相对位置进行分组
            var horizontalGroups = GroupByProximity(selectNodeControls, proximityThreshold, isHorizontal: true);
            var verticalGroups = GroupByProximity(selectNodeControls, proximityThreshold, isHorizontal: false);

            // 对每个水平群组进行垂直对齐
            foreach (var group in horizontalGroups)
            {
                double avgY = group.Average(c => Canvas.GetTop(c)); // 计算Y坐标平均值
                foreach (var control in group)
                {
                    Canvas.SetTop(control, avgY); // 对齐Y坐标
                }
            }

            // 对每个垂直群组进行水平对齐
            foreach (var group in verticalGroups)
            {
                double avgX = group.Average(c => Canvas.GetLeft(c)); // 计算X坐标平均值
                foreach (var control in group)
                {
                    Canvas.SetLeft(control, avgX); // 对齐X坐标
                }
            }
        }

        // 基于控件间的距离来分组，按水平或垂直方向
        private List<List<NodeControlBase>> GroupByProximity(List<NodeControlBase> controls, double proximityThreshold, bool isHorizontal)
        {
            var groups = new List<List<NodeControlBase>>();

            foreach (var control in controls)
            {
                bool addedToGroup = false;

                // 尝试将控件加入现有的群组
                foreach (var group in groups)
                {
                    if (IsInProximity(group, control, proximityThreshold, isHorizontal))
                    {
                        group.Add(control);
                        addedToGroup = true;
                        break;
                    }
                }

                // 如果没有加入任何群组，创建新群组
                if (!addedToGroup)
                {
                    groups.Add(new List<NodeControlBase> { control });
                }
            }

            return groups;
        }

        // 判断控件是否接近某个群组
        private bool IsInProximity(List<NodeControlBase> group, NodeControlBase control, double proximityThreshold, bool isHorizontal)
        {
            foreach (var existingControl in group)
            {
                double distance = isHorizontal
                    ? Math.Abs(Canvas.GetTop(existingControl) - Canvas.GetTop(control)) // 垂直方向的距离
                    : Math.Abs(Canvas.GetLeft(existingControl) - Canvas.GetLeft(control)); // 水平方向的距离

                if (distance <= proximityThreshold)
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Plan B 规划对齐
        /// <summary>
        /// 使用动态规划对齐选中的控件，确保控件之间有间距，并且不重叠。
        /// </summary>
        /// <param name="selectNodeControls"></param>
        /// <param name="spacing"></param>
        public void AlignControlsWithDynamicProgramming(List<NodeControlBase> selectNodeControls, double spacing = 10)
        {
            if (selectNodeControls is null || selectNodeControls.Count < 2)
                return;

            int n = selectNodeControls.Count;
            double[] dp = new double[n];
            int[] split = new int[n];

            // 初始化动态规划数组
            for (int i = 1; i < n; i++)
            {
                dp[i] = double.MaxValue;
                for (int j = 0; j < i; j++)
                {
                    double cost = CalculateAlignmentCost(selectNodeControls, j, i, spacing);
                    if (dp[j] + cost < dp[i])
                    {
                        dp[i] = dp[j] + cost;
                        split[i] = j;
                    }
                }
            }

            // 回溯找到最优的对齐方式
            AlignWithSplit(selectNodeControls, split, n - 1, spacing);
        }

        // 计算从控件[j]到控件[i]的对齐代价，并考虑控件的大小和间距
        private double CalculateAlignmentCost(List<NodeControlBase> controls, int start, int end, double spacing)
        {
            double totalWidth = 0;
            double totalHeight = 0;

            for (int i = start; i <= end; i++)
            {
                totalWidth += controls[i].ActualWidth;
                totalHeight += controls[i].ActualHeight;
            }

            // 水平和垂直方向代价计算，包括控件大小和间距
            double widthCost = totalWidth + (end - start) * spacing;
            double heightCost = totalHeight + (end - start) * spacing;

            // 返回较小的代价，表示更优的对齐方式
            return Math.Min(widthCost, heightCost);
        }

        // 根据split数组调整控件位置，确保控件不重叠
        private void AlignWithSplit(List<NodeControlBase> controls, int[] split, int end, double spacing)
        {
            if (end <= 0)
                return;

            AlignWithSplit(controls, split, split[end], spacing);

            // 从split[end]到end的控件进行对齐操作
            double currentX = Canvas.GetLeft(controls[split[end]]);
            double currentY = Canvas.GetTop(controls[split[end]]);

            for (int i = split[end] + 1; i <= end; i++)
            {
                // 水平或垂直对齐，确保控件之间有间距
                if (currentX + controls[i].ActualWidth + spacing <= Canvas.GetLeft(controls[end]))
                {
                    Canvas.SetLeft(controls[i], currentX + controls[i].ActualWidth + spacing);
                    currentX += controls[i].ActualWidth + spacing;
                }
                else
                {
                    Canvas.SetTop(controls[i], currentY + controls[i].ActualHeight + spacing);
                    currentY += controls[i].ActualHeight + spacing;
                }
            }
        }

        #endregion


        /// <summary>
        /// 对齐模式枚举
        /// </summary>
        public enum AlignMode
        {
            /// <summary>
            /// 水平对齐
            /// </summary>
            Horizontal,
            /// <summary>
            /// 垂直对齐
            /// </summary>
            Vertical,
            /// <summary>
            /// 水平中心对齐
            /// </summary>
            HorizontalCenter,
            /// <summary>
            /// 垂直中心对齐
            /// </summary>
            VerticalCenter,

            /// <summary>
            /// 规划对齐
            /// </summary>
            Planning,
            /// <summary>
            /// 群组对齐
            /// </summary>
            Grouping,
        }

        /// <summary>
        /// 对齐控件，支持多种对齐方式。
        /// </summary>
        /// <param name="selectNodeControls"></param>
        /// <param name="alignMode"></param>
        /// <param name="proximityThreshold"></param>
        /// <param name="spacing"></param>
        public void AlignControlsWithGrouping(List<NodeControlBase> selectNodeControls, AlignMode alignMode, double proximityThreshold = 50, double spacing = 10)
        {
            if (selectNodeControls is null || selectNodeControls.Count < 2)
                return;

            switch (alignMode)
            {
                case AlignMode.Horizontal:
                    AlignHorizontally(selectNodeControls, spacing);// AlignToCenter
                    break;

                case AlignMode.Vertical:

                    AlignVertically(selectNodeControls, spacing);
                    break;

                case AlignMode.HorizontalCenter:
                    AlignToCenter(selectNodeControls, isHorizontal: false, spacing);
                    break;

                case AlignMode.VerticalCenter:
                    AlignToCenter(selectNodeControls, isHorizontal: true, spacing);
                    break;

                case AlignMode.Planning:
                    AlignControlsWithDynamicProgramming(selectNodeControls, spacing);
                    break;
                case AlignMode.Grouping:
                    AlignControlsWithGrouping(selectNodeControls, proximityThreshold, spacing);
                    break;
            }


        }

        // 垂直对齐并避免重叠
        private void AlignHorizontally(List<NodeControlBase> controls, double spacing)
        {
            double avgY = controls.Average(c => Canvas.GetTop(c)); // 计算Y坐标平均值
            double currentY = avgY;

            foreach (var control in controls.OrderBy(c => Canvas.GetTop(c))) // 按Y坐标排序对齐
            {
                Canvas.SetTop(control, currentY);
                currentY += control.ActualHeight + spacing; // 保证控件之间有足够的垂直间距
            }
        }

        // 水平对齐并避免重叠
        private void AlignVertically(List<NodeControlBase> controls, double spacing)
        {
            double avgX = controls.Average(c => Canvas.GetLeft(c)); // 计算X坐标平均值
            double currentX = avgX;

            foreach (var control in controls.OrderBy(c => Canvas.GetLeft(c))) // 按X坐标排序对齐
            {
                Canvas.SetLeft(control, currentX);
                currentX += control.ActualWidth + spacing; // 保证控件之间有足够的水平间距
            }
        }

        // 按中心点对齐
        private void AlignToCenter(List<NodeControlBase> controls, bool isHorizontal, double spacing)
        {
            double avgCenter = isHorizontal
                ? controls.Average(c => Canvas.GetLeft(c) + c.ActualWidth / 2) // 水平中心点
                : controls.Average(c => Canvas.GetTop(c) + c.ActualHeight / 2); // 垂直中心点

            foreach (var control in controls)
            {
                if (isHorizontal)
                {
                    double left = avgCenter - control.ActualWidth / 2;
                    Canvas.SetLeft(control, left);
                }
                else
                {
                    double top = avgCenter - control.ActualHeight / 2;
                    Canvas.SetTop(control, top);
                }
            }
        }

        #endregion




    }
}
