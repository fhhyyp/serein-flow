using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Serein.Library.Api;
using Serein.Library.Entity;
using Serein.Library.Enums;
using Serein.Library.Utils;
using Serein.NodeFlow;
using Serein.NodeFlow.Base;
using Serein.NodeFlow.Model;
using Serein.WorkBench.Node.View;
using Serein.WorkBench.Node.ViewModel;
using Serein.WorkBench.Themes;
using Serein.WorkBench.tool;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using DataObject = System.Windows.DataObject;

namespace Serein.WorkBench
{
    /// <summary>
    /// 拖拽创建节点类型
    /// </summary>
    public static class MouseNodeType
    {
        public static string CreateDllNodeInCanvas { get; } = nameof(CreateDllNodeInCanvas);
        public static string CreateBaseNodeInCanvas { get; } = nameof(CreateBaseNodeInCanvas);
        //public static string RegionType { get; } = nameof(RegionType);
        //public static string BaseNodeType { get; } = nameof(BaseNodeType);
        //public static string DllNodeType { get; } = nameof(DllNodeType);
    }


   

    /// <summary>
    /// Interaction logic for MainWindow.xaml，第一次用git，不太懂
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 全局捕获Console输出事件，打印在这个窗体里面
        /// </summary>
        private readonly LogWindow logWindow;

        /// <summary>
        /// 流程运行环境
        /// </summary>
        private IFlowEnvironment FlowEnvironment { get; }
        private MainWindowViewModel ViewModel { get; set; }

        /// <summary>
        /// 存储所有与节点有关的控件
        /// </summary>
        private Dictionary<string, NodeControlBase> NodeControls { get; } = [];
        /// <summary>
        /// 存储所有的连接
        /// </summary>
        private List<Connection> Connections { get; } = [];



        #region 与画布相关的字段
        /// <summary>
        /// 当前选取的控件
        /// </summary>
        private readonly List<NodeControlBase> selectControls = [];

        /// <summary>
        /// 拖动创建节点控件时的鼠标位置
        /// </summary>
        // private Point canvasDropPosition;

        /// <summary>
        /// 记录拖动开始时的鼠标位置
        /// </summary>
        private Point startPoint;
        /// <summary>
        /// 流程图起点的控件
        /// </summary>
        // private NodeControlBase? flowStartBlock;
        /// <summary>
        /// 记录开始连接的文本块
        /// </summary>
        private NodeControlBase? startConnectNodeControl;
        /// <summary>
        /// 当前正在绘制的连接线
        /// </summary>
        private Line? currentLine;
        /// <summary>
        /// 当前正在绘制的真假分支属性
        /// </summary>
        private ConnectionType currentConnectionType;
        /// <summary>
        /// 标记是否正在进行连接操作
        /// </summary>
        private bool IsConnecting;
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
        /// 组合变换容器
        /// </summary>
        private TransformGroup canvasTransformGroup;
        /// <summary>
        /// 缩放画布
        /// </summary>
        private ScaleTransform scaleTransform;
        /// <summary>
        /// 平移画布 
        /// </summary>
        private TranslateTransform translateTransform;
        #endregion

        public MainWindow()
        {
            ViewModel = new MainWindowViewModel(this);
            FlowEnvironment = ViewModel.FlowEnvironment;
            InitFlowEvent();

            InitializeComponent();
            logWindow = new LogWindow();
            logWindow.Show();
            // 重定向 Console 输出
            var logTextWriter = new LogTextWriter(WriteLog);
            Console.SetOut(logTextWriter);
            InitUI();

            var project = App.FData;
            if (project == null)
            {
                return;
            }
            InitializeCanvas(project.Basic.Canvas.Width, project.Basic.Canvas.Lenght);// 设置画布大小
            FlowEnvironment.LoadProject(project, App.FileDataPath); // 加载项目
        }

        private void InitFlowEvent()
        {
            FlowEnvironment.OnDllLoad += FlowEnvironment_DllLoadEvent;
            // FlowEnvironment.OnLoadNode += FlowEnvironment_NodeLoadEvent;
            FlowEnvironment.OnProjectLoaded += FlowEnvironment_OnProjectLoaded;
            FlowEnvironment.OnStartNodeChange += FlowEnvironment_StartNodeChangeEvent;
            FlowEnvironment.OnNodeConnectChange += FlowEnvironment_NodeConnectChangeEvemt;
            FlowEnvironment.OnNodeCreate += FlowEnvironment_NodeCreateEvent;
            FlowEnvironment.OnNodeRemote += FlowEnvironment_NodeRemoteEvent;
            FlowEnvironment.OnFlowRunComplete += FlowEnvironment_OnFlowRunComplete;

        }


        private void InitUI()
        {
            canvasTransformGroup = new TransformGroup();
            scaleTransform = new ScaleTransform();
            translateTransform = new TranslateTransform();

            canvasTransformGroup.Children.Add(scaleTransform);
            canvasTransformGroup.Children.Add(translateTransform);

            FlowChartCanvas.RenderTransform = canvasTransformGroup;
            FlowChartCanvas.RenderTransformOrigin = new Point(0.5, 0.5);
        }
        #region Main窗体加载方法
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            logWindow.Close();
            System.Windows.Application.Current.Shutdown();
        }
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            foreach (var connection in Connections)
            {
                connection.Refresh();
            }
        } 
        #endregion
        public void WriteLog(string message)
        {
            logWindow.AppendText(message);
        }

        #region 运行环境事件
        private void FlowEnvironment_OnProjectLoaded(ProjectLoadedEventArgs eventArgs)
        {
            //foreach(var connection in Connections)
            //{
            //    connection.Refresh();
            //}
            Console.WriteLine((FlowChartStackPanel.ActualWidth, FlowChartStackPanel.ActualHeight));
        }


        /// <summary>
        /// 运行完成
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEnvironment_OnFlowRunComplete(FlowEventArgs eventArgs)
        {
            WriteLog("-------运行完成---------\r\n");
        }

        /// <summary>
        /// 加载了DLL文件，dll内容
        /// </summary>
        private void FlowEnvironment_DllLoadEvent(LoadDLLEventArgs eventArgs)
        {
            this.Dispatcher.Invoke(() => {
                Assembly assembly = eventArgs.Assembly;
                List<MethodDetails> methodDetailss = eventArgs.MethodDetailss;

                var dllControl = new DllControl
                {
                    Header = "DLL name :  " + assembly.GetName().Name // 设置控件标题为程序集名称
                };

                foreach (var methodDetails in methodDetailss)
                {
                    switch (methodDetails.MethodDynamicType)
                    {
                        case Library.Enums.NodeType.Action:
                            dllControl.AddAction(methodDetails.Clone());  // 添加动作类型到控件
                            break;
                        case Library.Enums.NodeType.Flipflop:
                            dllControl.AddFlipflop(methodDetails.Clone());  // 添加触发器方法到控件
                            break;
                    }
                }
                DllStackPanel.Children.Add(dllControl);  // 将控件添加到界面上显示
            });
            
        }

        /// <summary>
        /// 运行环境成功加载了节点，需要在画布上创建节点控件
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <param name="methodDetailss"></param>
        //private void FlowEnvironment_NodeLoadEvent(LoadNodeEventArgs eventArgs)
        //{
        //    if (!eventArgs.IsSucceed)
        //    {
        //        MessageBox.Show(eventArgs.ErrorTips);
        //        return;
        //    }

        //    NodeInfo nodeInfo = eventArgs.NodeInfo;
        //    MethodDetails methodDetailss = eventArgs.MethodDetailss;

        //    // 创建对应的实例（包含NodeModel，NodeControl，NodeControlViewModel）
        //    NodeControlBase? nodeControl = CreateNodeControlOfNodeInfo(nodeInfo, methodDetailss);
        //    if (nodeControl == null)
        //    {
        //        WriteLog($"无法为节点类型创建节点控件: {nodeInfo.MethodName}\r\n");
        //        return;
        //        // ConfigureNodeControl(nodeInfo, nodeControl, nodeControls, regionControls);
        //    }

        //    // 判断是否属于区域控件，如果是，则加载区域子项
        //    // if (nodeControl is ActionRegionControl || nodeControl is ConditionRegionControl)
        //    // {
        //    //     AddNodeControlInRegeionControl(nodeControl, nodeInfo.ChildNodes);
        //    // }

        //    NodeControls.TryAdd(nodeInfo.Guid, nodeControl); // 存放对应的控件
        //    PlaceNodeOnCanvas(nodeControl, nodeInfo.Position.X, nodeInfo.Position.Y); // 配置节点，并放置在画布上
        //}

        /// <summary>
        /// 节点连接关系变更
        /// </summary>
        /// <param name="fromNodeGuid"></param>
        /// <param name="toNodeGuid"></param>
        /// <param name="connectionType"></param>
        private void FlowEnvironment_NodeConnectChangeEvemt(NodeConnectChangeEventArgs eventArgs)
        {
            this.Dispatcher.Invoke(() =>
            {
                string fromNodeGuid = eventArgs.FromNodeGuid;
                string toNodeGuid = eventArgs.ToNodeGuid;
                if (!NodeControls.TryGetValue(fromNodeGuid, out var fromNode) || !NodeControls.TryGetValue(toNodeGuid, out var toNode))
                {
                    return;
                }
                ConnectionType connectionType = eventArgs.ConnectionType;
                if (eventArgs.ChangeType == NodeConnectChangeEventArgs.ConnectChangeType.Create)
                {
                    lock (Connections)
                    {
                        // 添加连接
                        var connection = new Connection
                        {
                            Start = fromNode,
                            End = toNode,
                            Type = connectionType
                        };

                        BsControl.Draw(FlowChartCanvas, connection); // 添加贝塞尔曲线显示
                        ConfigureLineContextMenu(connection); // 设置连接右键事件
                        Connections.Add(connection);
                        EndConnection();

                    }

                }
                else if (eventArgs.ChangeType == NodeConnectChangeEventArgs.ConnectChangeType.Remote)
                {
                    // 需要移除连接
                    var removeConnections = Connections.Where(c => c.Start.ViewModel.Node.Guid.Equals(fromNodeGuid)
                                           && c.End.ViewModel.Node.Guid.Equals(toNodeGuid))
                                .ToList();
                    foreach (var connection in removeConnections)
                    {
                        connection.RemoveFromCanvas(FlowChartCanvas);
                        Connections.Remove(connection);
                    }
                }
            });
        }

        /// <summary>
        /// 节点移除事件
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FlowEnvironment_NodeRemoteEvent(NodeRemoteEventArgs eventArgs)
        {
            this.Dispatcher.Invoke(() =>
            {
                var nodeGuid = eventArgs.NodeGuid;
                if (!NodeControls.TryGetValue(nodeGuid, out var nodeControl))
                {
                    return;
                }
                FlowChartCanvas.Children.Remove(nodeControl);
                NodeControls.Remove(nodeControl.ViewModel.Node.Guid);
            });
        }

        /// <summary>
        /// 编辑项目时添加了节点
        /// </summary>
        /// <param name="nodeDataBase"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEnvironment_NodeCreateEvent(NodeCreateEventArgs eventArgs)
        {
            this.Dispatcher.Invoke(() =>
            {
                if (eventArgs.NodeModel is not NodeModelBase nodeModelBase)
                {
                    return;
                }

                // MethodDetails methodDetailss = eventArgs.MethodDetailss;
                Position position = eventArgs.Position;

                // 创建对应控件
                NodeControlBase? nodeControl = nodeModelBase.ControlType switch
                {
                    NodeControlType.Action => CreateNodeControl<ActionNodeControl, ActionNodeControlViewModel>(nodeModelBase), //typeof(ActionNodeControl),
                    NodeControlType.Flipflop => CreateNodeControl<FlipflopNodeControl, FlipflopNodeControlViewModel>(nodeModelBase),
                    NodeControlType.ExpCondition => CreateNodeControl<ConditionNodeControl, ConditionNodeControlViewModel>(nodeModelBase),
                    NodeControlType.ExpOp => CreateNodeControl<ExpOpNodeControl, ExpOpNodeViewModel>(nodeModelBase),
                    NodeControlType.ConditionRegion => CreateNodeControl<ConditionRegionControl, ConditionRegionNodeControlViewModel>(nodeModelBase),
                    _ => null,
                };
                if (nodeControl == null)
                {
                    return;
                }
                NodeControls.TryAdd(nodeModelBase.Guid, nodeControl);

                if (eventArgs.IsAddInRegion && NodeControls.TryGetValue(eventArgs.RegeionGuid, out NodeControlBase? regionControl))
                {
                    if (regionControl is not null)
                    {
                        TryPlaceNodeInRegion(regionControl, nodeControl);
                    }
                    return;
                }
                else
                {
                    if (!TryPlaceNodeInRegion(nodeControl, position))
                    {
                        PlaceNodeOnCanvas(nodeControl, position.X, position.Y);
                    }
                }




            });
        }


        /// <summary>
        /// 设置了流程起始控件
        /// </summary>
        /// <param name="oldNodeGuid"></param>
        /// <param name="newNodeGuid"></param>
        private void FlowEnvironment_StartNodeChangeEvent(StartNodeChangeEventArgs eventArgs)
        {
            this.Dispatcher.Invoke(() =>
            {
                string oldNodeGuid = eventArgs.OldNodeGuid;
                string newNodeGuid = eventArgs.NewNodeGuid;
                if (!NodeControls.TryGetValue(newNodeGuid, out var newStartNodeControl))
                {
                    return;
                }
                if (newStartNodeControl == null)
                {
                    return;
                }
                if (!string.IsNullOrEmpty(oldNodeGuid))
                {
                    NodeControls.TryGetValue(oldNodeGuid, out var oldStartNodeControl);
                    if (oldStartNodeControl != null)
                    {
                        oldStartNodeControl.BorderBrush = Brushes.Black;
                        oldStartNodeControl.BorderThickness = new Thickness(0);
                    }

                }

                newStartNodeControl.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#04FC10"));
                newStartNodeControl.BorderThickness = new Thickness(2);
            });

        }

        #endregion

        #region 加载 DynamicNodeFlow 文件
        
        /// <summary>
        /// 运行环节加载了项目文件，需要创建节点控件
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <param name="methodDetailss"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private NodeControlBase? CreateNodeControlOfNodeInfo(NodeInfo nodeInfo, MethodDetails methodDetailss)
        {
            // 创建控件实例
            NodeControlBase nodeControl = nodeInfo.Type switch
            {
                $"{NodeStaticConfig.NodeSpaceName}.{nameof(SingleActionNode)}" =>
                    CreateNodeControl<SingleActionNode, ActionNodeControl, ActionNodeControlViewModel>(methodDetailss),// 动作节点控件
                $"{NodeStaticConfig.NodeSpaceName}.{nameof(SingleFlipflopNode)}" =>
                    CreateNodeControl<SingleFlipflopNode, FlipflopNodeControl, FlipflopNodeControlViewModel>(methodDetailss), // 触发器节点控件

                $"{NodeStaticConfig.NodeSpaceName}.{nameof(SingleConditionNode)}" =>
                    CreateNodeControl<SingleConditionNode, ConditionNodeControl, ConditionNodeControlViewModel>(), // 条件表达式控件
                $"{NodeStaticConfig.NodeSpaceName}.{nameof(SingleExpOpNode)}" =>
                    CreateNodeControl<SingleExpOpNode, ExpOpNodeControl, ExpOpNodeViewModel>(), // 操作表达式控件

                $"{NodeStaticConfig.NodeSpaceName}.{nameof(CompositeConditionNode)}" =>
                    CreateNodeControl<CompositeConditionNode, ConditionRegionControl, ConditionRegionNodeControlViewModel>(), // 条件区域控件
                _ => throw new NotImplementedException($"非预期的节点类型{nodeInfo.Type}"),
            };
            return nodeControl;
        }
        /// <summary>
        /// 加载文件时，添加节点到区域中
        /// </summary>
        /// <param name="regionControl"></param>
        /// <param name="childNodes"></param>
        private void AddNodeControlInRegeionControl(NodeControlBase regionControl, NodeInfo[] childNodes)
        {
            foreach (var childNode in childNodes)
            {
                if (FlowEnvironment.TryGetMethodDetails(childNode.MethodName, out MethodDetails md))
                {
                    var childNodeControl = CreateNodeControlOfNodeInfo(childNode, md);
                    if (childNodeControl == null)
                    {
                        WriteLog($"无法为节点类型创建节点控件: {childNode.MethodName}\r\n");
                        continue;
                    }

                    if (regionControl is ConditionRegionControl conditionRegion)
                    {
                        conditionRegion.AddCondition(childNodeControl);
                    }
                }
            }
        }
       
        #endregion

        #region 节点控件的创建

        private static TControl CreateNodeControl<TControl, TViewModel>(NodeModelBase model)
            where TControl : NodeControlBase
            where TViewModel : NodeControlViewModelBase
        {

            if (model == null)
            {
                throw new Exception("无法创建节点控件");
            }

            var viewModel = Activator.CreateInstance(typeof(TViewModel), [model]);
            var controlObj = Activator.CreateInstance(typeof(TControl), [viewModel]);
            if (controlObj is TControl control)
            {
                return control;
            }
            else
            {
                throw new Exception("无法创建节点控件");
            }
        }

        private static TControl CreateNodeControl<TNode, TControl,TViewModel>(MethodDetails? methodDetails = null) 
            where TNode : NodeModelBase
            where TControl : NodeControlBase
            where TViewModel : NodeControlViewModelBase
        {
            
            var nodeObj = Activator.CreateInstance(typeof(TNode));
            var nodeBase = nodeObj as NodeModelBase;
            if (nodeBase == null)
            {
                throw new Exception("无法创建节点控件");
            }

            
            nodeBase.Guid = Guid.NewGuid().ToString();

            if (methodDetails != null)
            {
                var md = methodDetails.Clone();
                nodeBase.DisplayName = md.MethodTips;
                nodeBase.MethodDetails = md;
            }

            var viewModel = Activator.CreateInstance(typeof(TViewModel), [nodeObj]);
            var controlObj = Activator.CreateInstance(typeof(TControl), [viewModel] );
            if(controlObj is TControl control)
            {
                return control;
            }
            else
            {
                throw new Exception("无法创建节点控件");
            }
        }

        /// <summary>
        /// 创建了节点，添加到画布。配置默认事件
        /// </summary>
        /// <param name="nodeControl"></param>
        /// <param name="position"></param>
        private void PlaceNodeOnCanvas(NodeControlBase nodeControl, double x, double y)
        {
            FlowChartCanvas.Dispatcher.Invoke(() =>
            {
                // 添加控件到画布
                FlowChartCanvas.Children.Add(nodeControl);
                Canvas.SetLeft(nodeControl, x);
                Canvas.SetTop(nodeControl, y);
                
                ConfigureContextMenu(nodeControl); // 配置节点右键菜单
                ConfigureNodeEvents(nodeControl); // 配置节点事件
            });
        }

        /// <summary>
        /// 配置节点右键菜单
        /// </summary>
        /// <param name="nodeControl"></param>
        private void ConfigureContextMenu(NodeControlBase nodeControl)
        {
            var contextMenu = new ContextMenu();

            // var nodeModel = nodeControl.ViewModel.Node;

            if (nodeControl.ViewModel.Node?.MethodDetails?.ReturnType is Type returnType && returnType != typeof(void))
            {
                contextMenu.Items.Add(CreateMenuItem("查看返回类型", (s, e) =>
                {
                    DisplayReturnTypeTreeViewer(returnType);
                }));
            }
            var nodeGuid = nodeControl?.ViewModel?.Node?.Guid;
            contextMenu.Items.Add(CreateMenuItem("设为起点", (s, e) => FlowEnvironment.SetStartNode(nodeGuid)));
            contextMenu.Items.Add(CreateMenuItem("删除", (s, e) => FlowEnvironment.RemoteNode(nodeGuid)));

            contextMenu.Items.Add(CreateMenuItem("添加 真分支", (s, e) => StartConnection(nodeControl, ConnectionType.IsSucceed)));
            contextMenu.Items.Add(CreateMenuItem("添加 假分支", (s, e) => StartConnection(nodeControl, ConnectionType.IsFail)));
            contextMenu.Items.Add(CreateMenuItem("添加 异常分支", (s, e) => StartConnection(nodeControl, ConnectionType.IsError)));
            contextMenu.Items.Add(CreateMenuItem("添加 上游分支", (s, e) => StartConnection(nodeControl, ConnectionType.Upstream)));


            nodeControl.ContextMenu = contextMenu;
        }

        /// <summary>
        /// 创建菜单子项
        /// </summary>
        /// <param name="header"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
        private static MenuItem CreateMenuItem(string header, RoutedEventHandler handler)
        {
            var menuItem = new MenuItem { Header = header };
            menuItem.Click += handler;
            return menuItem;
        }


        /// <summary>
        /// 配置节点事件
        /// </summary>
        /// <param name="nodeControl"></param>
        private void ConfigureNodeEvents(NodeControlBase nodeControl)
        {
            nodeControl.MouseLeftButtonDown += Block_MouseLeftButtonDown;
            nodeControl.MouseMove += Block_MouseMove;
            nodeControl.MouseLeftButtonUp += Block_MouseLeftButtonUp;
        }



        #endregion

        #region 右键菜单事件
        /// <summary>
        /// 开始创建连接 True线 操作，设置起始块和绘制连接线。
        /// </summary>
        private void StartConnection(NodeControlBase startNodeControl, ConnectionType connectionType)
        {
            var tf = Connections.FirstOrDefault(it => it.Start == startNodeControl)?.Type;
            IsConnecting = true;
            currentConnectionType = connectionType;
            startConnectNodeControl = startNodeControl;

            // 确保起点和终点位置的正确顺序
            currentLine = new Line
            {
                Stroke = connectionType == ConnectionType.IsSucceed ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#04FC10"))
                        : connectionType == ConnectionType.IsFail ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F18905"))
                        : connectionType == ConnectionType.IsError ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AB616B"))
                                                                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A82E4")),
                StrokeDashArray = new DoubleCollection([2]),
                StrokeThickness = 2,
                X1 = Canvas.GetLeft(startConnectNodeControl) + startConnectNodeControl.ActualWidth / 2,
                Y1 = Canvas.GetTop(startConnectNodeControl) + startConnectNodeControl.ActualHeight / 2,
                X2 = Canvas.GetLeft(startConnectNodeControl) + startConnectNodeControl.ActualWidth / 2, // 初始时终点与起点重合
                Y2 = Canvas.GetTop(startConnectNodeControl) + startConnectNodeControl.ActualHeight / 2,
            };

            FlowChartCanvas.Children.Add(currentLine);
            this.KeyDown += MainWindow_KeyDown;
        }

        /// <summary>
        /// 配置连接曲线的右键菜单
        /// </summary>
        /// <param name="line"></param>
        private void ConfigureLineContextMenu(Connection connection)
        {
            var contextMenu = new ContextMenu();

            contextMenu.Items.Add(CreateMenuItem("删除连线", (s, e) => DeleteConnection(connection)));
            connection.ArrowPath.ContextMenu = contextMenu;
            connection.BezierPath.ContextMenu = contextMenu;
        }
        /// <summary>
        /// 删除该连线
        /// </summary>
        /// <param name="line"></param>
        private void DeleteConnection(Connection connection)
        {
            var connectionToRemove = connection;
            if (connectionToRemove == null)
            {
                return;
            }
            // 获取起始节点与终止节点，消除映射关系
            var fromNodeGuid = connectionToRemove.Start.ViewModel.Node.Guid;
            var toNodeGuid = connectionToRemove.End.ViewModel.Node.Guid;
            FlowEnvironment.RemoteConnect(fromNodeGuid, toNodeGuid, connection.Type);
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
                Console.WriteLine(ex);
            }
        }
        #endregion

        #region 拖拽DLL文件到左侧功能区，加载相关节点清单
        /// <summary>
        /// 当拖动文件到窗口时触发，加载DLL文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                {
                    if (file.EndsWith(".dll"))
                    {
                        FlowEnvironment.LoadDll(file);
                    }
                }
            }
        }

        /// <summary>
        /// 当拖动文件经过窗口时触发，设置拖放效果为复制
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        #endregion


        #region 与流程图相关的拖拽操作

        /// <summary>
        /// 基础节点的拖拽放置创建
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BaseNodeControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is UserControl control)
            {
                // 创建一个 DataObject 用于拖拽操作，并设置拖拽效果
                var dragData = new DataObject(MouseNodeType.CreateBaseNodeInCanvas, control.GetType());
                DragDrop.DoDragDrop(control, dragData, DragDropEffects.Move);
            }
        }

        /// <summary>
        /// 放置操作，根据拖放数据创建相应的控件，并处理相关操作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlowChartCanvas_Drop(object sender, DragEventArgs e)
        {
            var canvasDropPosition = e.GetPosition(FlowChartCanvas); // 更新画布落点
            Position position = new Position(canvasDropPosition.X, canvasDropPosition.Y);
            if (e.Data.GetDataPresent(MouseNodeType.CreateDllNodeInCanvas))
            {
                if (e.Data.GetData(MouseNodeType.CreateDllNodeInCanvas) is MoveNodeData nodeData)
                {
                    // 创建DLL文件的节点对象
                    FlowEnvironment.CreateNode(nodeData.NodeControlType, position, nodeData.MethodDetails);
                }
            }
            else if (e.Data.GetDataPresent(MouseNodeType.CreateBaseNodeInCanvas))
            {
                if (e.Data.GetData(MouseNodeType.CreateBaseNodeInCanvas) is Type droppedType)
                {
                    NodeControlType nodeControlType = droppedType switch
                    {
                        Type when typeof(ConditionRegionControl).IsAssignableFrom(droppedType) => NodeControlType.ConditionRegion, // 条件区域
                        Type when typeof(ConditionNodeControl).IsAssignableFrom(droppedType) => NodeControlType.ExpCondition,
                        Type when typeof(ExpOpNodeControl).IsAssignableFrom(droppedType) => NodeControlType.ExpOp,
                        _ => NodeControlType.None,
                    };
                    if(nodeControlType != NodeControlType.None)
                    {
                        // 创建基础节点对象
                        FlowEnvironment.CreateNode(nodeControlType, position);
                    }
                }
            }
            e.Handled = true;
        }

        /// <summary>
        /// 尝试将节点放置在区域中
        /// </summary>
        /// <param name="nodeControl"></param>
        /// <param name="dropPosition"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        private bool TryPlaceNodeInRegion(NodeControlBase nodeControl, Position position)
        {
            var point = new Point(position.X, position.Y);
            HitTestResult hitTestResult = VisualTreeHelper.HitTest(FlowChartCanvas, point);
            if (hitTestResult != null && hitTestResult.VisualHit is UIElement hitElement)
            {
                // 准备放置条件表达式控件
                if (nodeControl.ViewModel.Node.ControlType == NodeControlType.ExpCondition)
                {
                    ConditionRegionControl conditionRegion = GetParentOfType<ConditionRegionControl>(hitElement);
                    if (conditionRegion != null)
                    {
                        TryPlaceNodeInRegion(conditionRegion, nodeControl);
                        //// 如果存在条件区域容器
                        //conditionRegion.AddCondition(nodeControl);
                        return true;
                    }

                }
            }
            return false;
        }

        /// <summary>
        /// 穿透元素获取区域容器
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="element"></param>
        /// <returns></returns>
        private static T GetParentOfType<T>(DependencyObject element) where T : DependencyObject
        {
            while (element != null)
            {
                if (element is T)
                {
                    return element as T;
                }
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }

        /// <summary>
        /// 将节点放在目标区域中
        /// </summary>
        /// <param name="regionControl">区域容器</param>
        /// <param name="nodeControl">节点控件</param>
        private void TryPlaceNodeInRegion(NodeControlBase regionControl, NodeControlBase nodeControl)
        {
            // 准备放置条件表达式控件
            if (nodeControl.ViewModel.Node.ControlType == NodeControlType.ExpCondition)
            {
                ConditionRegionControl conditionRegion = regionControl as ConditionRegionControl;
                if (conditionRegion != null)
                {
                    // 如果存在条件区域容器
                    conditionRegion.AddCondition(nodeControl);
                }
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
        /// 控件的鼠标左键按下事件，启动拖动操作。
        /// </summary>
        private void Block_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsConnecting)
                return;

            IsControlDragging = true;
            startPoint = e.GetPosition(FlowChartCanvas); // 记录鼠标按下时的位置
            ((UIElement)sender).CaptureMouse(); // 捕获鼠标
        }

        /// <summary>
        /// 控件的鼠标移动事件，根据鼠标拖动更新控件的位置。
        /// </summary>
        private void Block_MouseMove(object sender, MouseEventArgs e)
        {
            if (IsControlDragging) // 如果正在拖动控件
            {
                Point currentPosition = e.GetPosition(FlowChartCanvas); // 获取当前鼠标位置
                                                                        // 获取引发事件的控件
                if (sender is not UserControl block)
                {
                    return;
                }

                double deltaX = currentPosition.X - startPoint.X; // 计算X轴方向的偏移量
                double deltaY = currentPosition.Y - startPoint.Y; // 计算Y轴方向的偏移量

                double newLeft = Canvas.GetLeft(block) + deltaX; // 新的左边距
                double newTop = Canvas.GetTop(block) + deltaY; // 新的上边距

                // 限制控件不超出FlowChartCanvas的边界
                if (newLeft >= 0 && newLeft + block.ActualWidth <= FlowChartCanvas.ActualWidth)
                {
                    Canvas.SetLeft(block, newLeft);
                }
                if (newTop >= 0 && newTop + block.ActualHeight <= FlowChartCanvas.ActualHeight)
                {
                    Canvas.SetTop(block, newTop);
                }

                UpdateConnections(block);

                startPoint = currentPosition; // 更新起始点位置

            }
        }

        #region UI连接控件操作

        /// <summary>
        /// 控件的鼠标左键松开事件，结束拖动操作，创建连线
        /// </summary>
        private void Block_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsControlDragging)
            {
                IsControlDragging = false;
                ((UIElement)sender).ReleaseMouseCapture();  // 释放鼠标捕获
            }
            else if (IsConnecting)
            {
                var formNodeGuid = startConnectNodeControl?.ViewModel.Node.Guid;
                var toNodeGuid = (sender as NodeControlBase)?.ViewModel.Node.Guid;
                if (string.IsNullOrEmpty(formNodeGuid) || string.IsNullOrEmpty(toNodeGuid))
                {
                    return;
                }
                FlowEnvironment.ConnectNode(formNodeGuid, toNodeGuid, currentConnectionType);
            }
            /*else if (IsConnecting)
            {
                bool isRegion = false;
                NodeControlBase? targetBlock;

                if (sender is ActionNodeControl)
                {
                    targetBlock = sender as ActionNodeControl; // 动作
                }
                else if (sender is ActionRegionControl)
                {
                    targetBlock = sender as ActionRegionControl; // 组合动作 
                    isRegion = true;
                }
                else if (sender is ConditionNodeControl)
                {
                    targetBlock = sender as ConditionNodeControl; // 条件
                }
                else if (sender is ConditionRegionControl)
                {
                    targetBlock = sender as ConditionRegionControl; // 组合条件
                    isRegion = true;
                }
                else if (sender is FlipflopNodeControl)
                {
                    targetBlock = sender as FlipflopNodeControl; // 触发器
                }
                else if (sender is ExpOpNodeControl)
                {
                    targetBlock = sender as ExpOpNodeControl; // 触发器
                }
                else
                {
                    targetBlock = null;
                }
                if (targetBlock == null)
                {
                    return;
                }

                if (startConnectBlock != null && targetBlock != null && startConnectBlock != targetBlock)
                {

                    var connection = new Connection { Start = startConnectBlock, End = targetBlock, Type = currentConnectionType };

                    if (currentConnectionType == ConnectionType.IsSucceed)
                    {
                        startConnectBlock.ViewModel.Node.SucceedBranch.Add(targetBlock.ViewModel.Node);
                    }
                    else if (currentConnectionType == ConnectionType.IsFail)
                    {
                        startConnectBlock.ViewModel.Node.FailBranch.Add(targetBlock.ViewModel.Node);
                    }
                    else if (currentConnectionType == ConnectionType.IsError)
                    {
                        startConnectBlock.ViewModel.Node.ErrorBranch.Add(targetBlock.ViewModel.Node);
                    }
                    else if (currentConnectionType == ConnectionType.Upstream)
                    {
                        startConnectBlock.ViewModel.Node.UpstreamBranch.Add(targetBlock.ViewModel.Node);
                    }

                    // 保存连接关系
                    BsControl.Draw(FlowChartCanvas, connection);
                    ConfigureLineContextMenu(connection);

                    targetBlock.ViewModel.Node.PreviousNodes.Add(startConnectBlock.ViewModel.Node); // 将当前发起连接的节点，添加到被连接的节点的上一节点队列。（用于回溯）
                    connections.Add(connection);
                }
                EndConnection();
            }*/
        }

        /// <summary>
        /// 主窗口的KeyDown事件处理，用于在连接操作中按下Esc键取消连接。
        /// </summary>
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && IsConnecting)
            {
                this.KeyDown -= MainWindow_KeyDown;
                EndConnection();
            }
        }

        /// <summary>
        /// 结束连接操作，清理状态并移除虚线。
        /// </summary>
        private void EndConnection()
        {
            IsConnecting = false;
            startConnectNodeControl = null;
            // 移除虚线
            if (currentLine != null)
            {
                FlowChartCanvas.Children.Remove(currentLine);
                currentLine = null;
            }
        }

        /// <summary>
        /// 更新与指定控件相关的所有连接的位置。
        /// </summary>
        private void UpdateConnections(UserControl block)
        {
            foreach (var connection in Connections)
            {
                if (connection.Start == block || connection.End == block)
                {
                    BezierLineDrawer.UpdateBezierLine(FlowChartCanvas, connection.Start, connection.End, connection.BezierPath, connection.ArrowPath);
                }
            }
        }
        #endregion

        #region 画布中框选节点控件动作

        /// <summary>
        /// 在画布中尝试选取控件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlowChartCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                IsSelectControl = true;

                // 开始选取时，记录鼠标起始点
                startPoint = e.GetPosition(FlowChartCanvas);

                // 初始化选取矩形的位置和大小
                Canvas.SetLeft(SelectionRectangle, startPoint.X);
                Canvas.SetTop(SelectionRectangle, startPoint.Y);
                SelectionRectangle.Width = 0;
                SelectionRectangle.Height = 0;

                // 显示选取矩形
                SelectionRectangle.Visibility = Visibility.Visible;

                // 捕获鼠标，以便在鼠标移动到Canvas外部时仍能处理事件
                FlowChartCanvas.CaptureMouse();
            }
        }

        /// <summary>
        /// 在画布中释放鼠标按下，结束选取状态
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlowChartCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsSelectControl)
            {

                IsSelectControl = false;
                // 释放鼠标捕获
                FlowChartCanvas.ReleaseMouseCapture();

                // 隐藏选取矩形（如果需要保持选取状态，可以删除此行）
                SelectionRectangle.Visibility = Visibility.Collapsed;

                // 处理选取区域内的元素（例如，获取选取范围内的控件）
                Rect selectionArea = new Rect(Canvas.GetLeft(SelectionRectangle),
                                              Canvas.GetTop(SelectionRectangle),
                                              SelectionRectangle.Width,
                                              SelectionRectangle.Height);


                selectControls.Clear();
                // 在此处处理选取的逻辑
                foreach (UIElement element in FlowChartCanvas.Children)
                {
                    Rect elementBounds = new Rect(Canvas.GetLeft(element), Canvas.GetTop(element),
                                                  element.RenderSize.Width, element.RenderSize.Height);

                    if (selectionArea.Contains(elementBounds))
                    {
                        // 选中元素，执行相应操作
                        if (element is NodeControlBase control)
                        {
                            selectControls.Add(control);
                        }
                    }
                }
                Console.WriteLine($"一共选取了{selectControls.Count}个控件");
            }
        }

        /// <summary>
        /// 鼠标在画布移动。
        /// 选择控件状态下，调整选择框大小
        /// 连接状态下，实时更新连接线的终点位置。
        /// 移动画布状态下，移动画布。
        /// </summary>
        private void FlowChartCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (IsSelectControl && e.LeftButton == MouseButtonState.Pressed) // 正在选取节点
            {
                // 获取当前鼠标位置
                Point currentPoint = e.GetPosition(FlowChartCanvas);

                // 更新选取矩形的位置和大小
                double x = Math.Min(currentPoint.X, startPoint.X);
                double y = Math.Min(currentPoint.Y, startPoint.Y);
                double width = Math.Abs(currentPoint.X - startPoint.X);
                double height = Math.Abs(currentPoint.Y - startPoint.Y);

                Canvas.SetLeft(SelectionRectangle, x);
                Canvas.SetTop(SelectionRectangle, y);
                SelectionRectangle.Width = width;
                SelectionRectangle.Height = height;
            }


            if (IsConnecting) // 正在连接节点
            {
                Point position = e.GetPosition(FlowChartCanvas);
                if (currentLine == null || startConnectNodeControl == null)
                {
                    return;
                }
                currentLine.X1 = Canvas.GetLeft(startConnectNodeControl) + startConnectNodeControl.ActualWidth / 2;
                currentLine.Y1 = Canvas.GetTop(startConnectNodeControl) + startConnectNodeControl.ActualHeight / 2;
                currentLine.X2 = position.X;
                currentLine.Y2 = position.Y;
            }
            if (IsCanvasDragging) // 正在移动画布
            {
                Point currentMousePosition = e.GetPosition(this);
                double deltaX = currentMousePosition.X - startPoint.X;
                double deltaY = currentMousePosition.Y - startPoint.Y;

                translateTransform.X += deltaX;
                translateTransform.Y += deltaY;

                startPoint = currentMousePosition;

                foreach (var line in Connections)
                {
                    line.Refresh();
                }

                e.Handled = true; // 防止事件传播影响其他控件
            }

        } 
        #endregion

        #region 拖动画布实现缩放平移效果
        private void FlowChartCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                IsCanvasDragging = true;
                startPoint = e.GetPosition(this);
                FlowChartCanvas.CaptureMouse();
                e.Handled = true; // 防止事件传播影响其他控件
            }
        }

        private void FlowChartCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (IsCanvasDragging)
            {
                IsCanvasDragging = false;
                FlowChartCanvas.ReleaseMouseCapture();
            }
        }

        // 单纯缩放画布，不改变画布大小
        private void FlowChartCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            //var w = (int)(FlowChartCanvas.Width * scaleTransform.ScaleX);
            //var h = (int)(FlowChartCanvas.Height * scaleTransform.ScaleY);

            //var TMP1 = w / FlowChartStackPanel.ActualWidth < 0.9;
            //var TMP2 = h / FlowChartStackPanel.ActualHeight < 0.9;

            //Console.WriteLine("w"+(w, FlowChartStackPanel.ActualWidth, TMP1));
            //Console.WriteLine("h"+(h, FlowChartStackPanel.ActualHeight, TMP2));


            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (e.Delta  < 0 && scaleTransform.ScaleX < 0.2) return;
                if (e.Delta  > 0 && scaleTransform.ScaleY > 1.5) return;
                double scale = e.Delta > 0 ? 0.1 : -0.1;

                scaleTransform.ScaleX += scale;
                scaleTransform.ScaleY += scale;

            }
        }

        // 设置画布宽度高度
        private void InitializeCanvas(double width, double height)
        {
            FlowChartCanvas.Width = width;
            FlowChartCanvas.Height = height;
            //FlowChartStackPanel.Width = width;
            //FlowChartStackPanel.Height = height;
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

            // 更新 Canvas 大小
            FlowChartCanvas.Width = newWidth;
            FlowChartCanvas.Height = newHeight;

            // 如果宽度和高度超过400，调整TranslateTransform以保持左上角不动
            if (newWidth > 400 && newHeight > 400)
            {
                // 计算平移的变化，保持左上角不动
                double deltaX = -horizontalChange / 2;  // 水平方向的平移
                double deltaY = -verticalChange / 2;    // 垂直方向的平移

                // 调整TranslateTransform以补偿尺寸变化
                translateTransform.X += deltaX;
                translateTransform.Y += deltaY;
            }

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
            //double newWidth = Math.Max(FlowChartCanvas.ActualWidth + e.HorizontalChange * scaleTransform.ScaleX, 0);
            //newWidth = newWidth < 400 ? 400 : newWidth;
            //if (newWidth > 400)
            //{
            //    FlowChartCanvas.Width = newWidth;

            //    double x = e.HorizontalChange > 0 ? -0.5 : 0.5;
            //    double y = 0;

            //    double deltaX = x * scaleTransform.ScaleX;
            //    double deltaY = y * 0;
            //    Test(deltaX, deltaY);
            //}



            // 获取缩放后的水平和垂直变化
            double horizontalChange = e.HorizontalChange * scaleTransform.ScaleX;
            //double verticalChange = e.VerticalChange * scaleTransform.ScaleY;

            // 计算新的宽度和高度，确保不会小于400
            double newWidth = Math.Max(FlowChartCanvas.ActualWidth + horizontalChange, 400);
            //double newHeight = Math.Max(FlowChartCanvas.ActualHeight + verticalChange, 400);

            // 更新 Canvas 大小
            FlowChartCanvas.Width = newWidth;
            //FlowChartCanvas.Height = newHeight;

            // 如果宽度和高度超过400，调整TranslateTransform以保持左上角不动
            if (newWidth > 400 /*&& newHeight > 400*/)
            {
                // 计算平移的变化，保持左上角不动
                double deltaX = -horizontalChange / 2;  // 水平方向的平移
                //double deltaY = -verticalChange / 2;    // 垂直方向的平移

                // 调整TranslateTransform以补偿尺寸变化
                translateTransform.X += deltaX;
                //translateTransform.Y += deltaY;
            }

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
            //// 从底部调整大小
            //double oldHeight = FlowChartCanvas.Height;

            //double newHeight = Math.Max(FlowChartCanvas.ActualHeight + e.VerticalChange * scaleTransform.ScaleY, 0);
            ////newHeight = newHeight < 400 ? 400 : newHeight;
            //if(newHeight > 400)
            //{
            //    FlowChartCanvas.Height = newHeight;

            //    double x = 0;
            //    double y = e.VerticalChange > 0 ? -0.5 : 0.5 ;

            //    double deltaX = x * 0;
            //    double deltaY = y * (scaleTransform.ScaleY);

            //    Test(deltaX, deltaY);
            //}


            // 获取缩放后的水平和垂直变化
            //double horizontalChange = e.HorizontalChange * scaleTransform.ScaleX;
            double verticalChange = e.VerticalChange * scaleTransform.ScaleY;

            // 计算新的宽度和高度，确保不会小于400
            //double newWidth = Math.Max(FlowChartCanvas.ActualWidth + horizontalChange, 400);
            double newHeight = Math.Max(FlowChartCanvas.ActualHeight + verticalChange, 400);

            // 更新 Canvas 大小
            //FlowChartCanvas.Width = newWidth;
            FlowChartCanvas.Height = newHeight;

            // 如果宽度和高度超过400，调整TranslateTransform以保持左上角不动
            if (/*newWidth > 400 &&*/ newHeight > 400)
            {
                // 计算平移的变化，保持左上角不动
                //double deltaX = -horizontalChange / 2;  // 水平方向的平移
                double deltaY = -verticalChange / 2;    // 垂直方向的平移

                // 调整TranslateTransform以补偿尺寸变化
                //translateTransform.X += deltaX;
                translateTransform.Y += deltaY;
            }


        }


        private void Test(double deltaX, double deltaY)
        {
            translateTransform.X += deltaX;
            translateTransform.Y += deltaY;
            //Console.WriteLine((translateTransform.X, translateTransform.Y));
        }

        #endregion
        #endregion

        #endregion


        /// <summary>
        /// 卸载DLL文件，清空当前项目
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UnloadAllButton_Click(object sender, RoutedEventArgs e)
        {
            FlowEnvironment.ClearAll();
        }
        /// <summary>
        /// 卸载DLL文件，清空当前项目
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UnloadAllAssemblies()
        {
            DllStackPanel.Children.Clear();
            FlowChartCanvas.Children.Clear();
            Connections.Clear();
            NodeControls.Clear();
            currentLine = null;
            startConnectNodeControl = null;
            MessageBox.Show("所有DLL已卸载。", "信息", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 运行测试
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ButtonDebugRun_Click(object sender, RoutedEventArgs e)
        {
            logWindow?.Show();
            await FlowEnvironment.StartAsync();
        }

        /// <summary>
        /// 退出
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonDebugFlipflopNode_Click(object sender, RoutedEventArgs e)
        {
            FlowEnvironment?.Exit(); // 在运行平台上点击了退出
        }

        /// <summary>
        /// 保存为项目文件 （正在重写）
        /// JsonConvert.SerializeObject 对象序列化字符串
        /// JArray.FromObject           数组序列化
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonSaveFile_Click(object sender, RoutedEventArgs e)
        {
            var projectData = FlowEnvironment.SaveProject();
            projectData.Basic = new Basic
            {
                Canvas = new FlowCanvas
                {
                    Lenght = (float)FlowChartCanvas.Width,
                    Width = (float)FlowChartCanvas.Height,
                },
                Versions = "1",
            };

            foreach(var node in projectData.Nodes)
            {
                
                if(NodeControls.TryGetValue(node.Guid,out var nodeControl))
                {
                    Point positionRelativeToParent = nodeControl.TranslatePoint(new Point(0, 0), FlowChartCanvas);
                    node.Position = new Position(positionRelativeToParent.X, positionRelativeToParent.Y);
                }
            }
            var projectJsonData = JObject.FromObject(projectData);
            var savePath = SaveContentToFile(projectJsonData.ToString());
            savePath = System.IO.Path.GetDirectoryName(savePath);

            // 复制dll文件
            //if (string.IsNullOrEmpty(savePath))
            //{
            //    return;
            //}
            //foreach (var dll in loadedAssemblies)
            //{
            //    try
            //    {
            //        string targetPath = System.IO.Path.Combine(savePath, System.IO.Path.GetFileName(dll.CodeBase));
            //        // 确保目标目录存在
            //        Directory.CreateDirectory(savePath);
            //        var sourceFile = new Uri(dll.CodeBase).LocalPath;
            //        // 复制文件到目标目录
            //        File.Copy(sourceFile, targetPath, true);
            //    }
            //    catch (Exception ex)
            //    {
            //        WriteLog($"DLL复制失败：{dll.CodeBase} \r\n错误：{ex}\r\n");
            //    }
            //}
        }
        public static string? SaveContentToFile(string content)
        {
            // 创建一个新的保存文件对话框
            SaveFileDialog saveFileDialog = new()
            {
                Filter = "NF Files (*.dnf)|*.dnf",
                DefaultExt = "nf",
                FileName = "project.dnf"
            };

            // 显示保存文件对话框
            bool? result = saveFileDialog.ShowDialog();

            // 如果用户选择了文件并点击了保存按钮
            if (result == true)
            {
                string filePath = saveFileDialog.FileName;

                try
                {
                    // 将文本内容写入文件
                    File.WriteAllText(filePath, content);
                    MessageBox.Show($"文本已成功保存到文件: {filePath}", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    return filePath;

                }
                catch (Exception ex)
                {

                    MessageBox.Show($"保存文件时出现错误: {ex.Message}", "保存错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            return null;
        }
        public static string GetRelativePath(string baseDirectory, string fullPath)
        {
            Uri baseUri = new(baseDirectory + System.IO.Path.DirectorySeparatorChar);
            Uri fullUri = new(fullPath);
            Uri relativeUri = baseUri.MakeRelativeUri(fullUri);
            return Uri.UnescapeDataString(relativeUri.ToString().Replace('/', System.IO.Path.DirectorySeparatorChar));
        }
    }

    #region 创建两个控件之间的连接关系，在UI层面上显示为 带箭头指向的贝塞尔曲线


    public static class BsControl
    {
        public static Connection Draw(Canvas canvas, Connection connection)
        {
            connection.Canvas = canvas;
            UpdateBezierLine(canvas, connection);
            //MakeDraggable(canvas, connection, connection.Start);
            //MakeDraggable(canvas, connection, connection.End);

            if (connection.BezierPath == null)
            {
                connection.BezierPath = new System.Windows.Shapes.Path { Stroke = BezierLineDrawer.GetStroke(connection.Type), StrokeThickness = 1 };
                Canvas.SetZIndex(connection.BezierPath, -1);
                canvas.Children.Add(connection.BezierPath);
            }
            if (connection.ArrowPath == null)
            {
                connection.ArrowPath = new System.Windows.Shapes.Path { Stroke = BezierLineDrawer.GetStroke(connection.Type), Fill = BezierLineDrawer.GetStroke(connection.Type), StrokeThickness = 1 };
                Canvas.SetZIndex(connection.ArrowPath, -1);
                canvas.Children.Add(connection.ArrowPath);
            }

           
            BezierLineDrawer.UpdateBezierLine(canvas, connection.Start, connection.End, connection.BezierPath, connection.ArrowPath);
            
            return connection;
        }

        private static bool isUpdating = false; // 是否正在更新线条显示


        // 拖动时重新绘制
        public static void UpdateBezierLine(Canvas canvas, Connection connection)
        {
            if (isUpdating)
                return;

            isUpdating = true;

            canvas.Dispatcher.InvokeAsync(() =>
            {
                if (connection != null && connection.BezierPath == null)
                {
                    connection.BezierPath = new System.Windows.Shapes.Path { Stroke = BezierLineDrawer.GetStroke(connection.Type), StrokeThickness = 1 };
                    //Canvas.SetZIndex(connection.BezierPath, -1);
                    canvas.Children.Add(connection.BezierPath);
                }

                if (connection != null && connection.ArrowPath == null)
                {
                    connection.ArrowPath = new System.Windows.Shapes.Path { Stroke = BezierLineDrawer.GetStroke(connection.Type), Fill = BezierLineDrawer.GetStroke(connection.Type), StrokeThickness = 1 };
                    //Canvas.SetZIndex(connection.ArrowPath, -1);
                    canvas.Children.Add(connection.ArrowPath);
                }

                BezierLineDrawer.UpdateBezierLine(canvas, connection.Start, connection.End, connection.BezierPath, connection.ArrowPath);
                isUpdating = false;
            });
        }

        // private static Point clickPosition; // 当前点击事件
        // private static bool isDragging = false; // 是否正在移动控件
        //private static void MakeDraggable(Canvas canvas, Connection connection, UIElement element)
        //{
        //    if (connection.IsSetEven)
        //    {
        //        return;
        //    }

        //    element.MouseLeftButtonDown += (sender, e) =>
        //    {
        //        isDragging = true;
        //        //clickPosition = e.GetPosition(element);
        //        //element.CaptureMouse();
        //    };
        //    element.MouseLeftButtonUp += (sender, e) =>
        //    {
        //        isDragging = false;
        //        //element.ReleaseMouseCapture();
        //    };

        //    element.MouseMove += (sender, e) =>
        //    {
        //        if (isDragging)
        //        {
        //            if (VisualTreeHelper.GetParent(element) is Canvas canvas)
        //            {
        //                Point currentPosition = e.GetPosition(canvas);
        //                double newLeft = currentPosition.X - clickPosition.X;
        //                double newTop = currentPosition.Y - clickPosition.Y;

        //                Canvas.SetLeft(element, newLeft);
        //                Canvas.SetTop(element, newTop);
        //                UpdateBezierLine(canvas, connection);
        //            }
        //        }
        //    };


        //}
    }


    public class Connection
    {
        public ConnectionType Type { get; set; }
        public Canvas Canvas { get; set; }// 贝塞尔曲线所在画布

        public System.Windows.Shapes.Path BezierPath { get; set; }// 贝塞尔曲线路径
        public System.Windows.Shapes.Path ArrowPath { get; set; } // 箭头路径

        public required NodeControlBase Start { get; set; } // 起始
        public required NodeControlBase End { get; set; }   // 结束

        private Storyboard? _animationStoryboard; // 动画Storyboard

        public void RemoveFromCanvas(Canvas canvas)
        {
            canvas.Children.Remove(BezierPath); // 移除线
            canvas.Children.Remove(ArrowPath); // 移除线
            _animationStoryboard?.Stop(); // 停止动画
        }

        public void Refresh()
        {
            // BsControl.Draw(Canvas, this);
            BezierLineDrawer.UpdateBezierLine(Canvas, Start, End, BezierPath, ArrowPath);
        }
    }

    public static class BezierLineDrawer
    {
        public enum Localhost
        {
            Left,
            Right,
            Top,
            Bottom,
        }

        // 绘制曲线
        public static void UpdateBezierLine(Canvas canvas,
                                            FrameworkElement startElement,
                                            FrameworkElement endElement,
                                            System.Windows.Shapes.Path bezierPath,
                                            System.Windows.Shapes.Path arrowPath)
        {
            Point startPoint = startElement.TranslatePoint(new Point(startElement.ActualWidth / 2, startElement.ActualHeight / 2), canvas);
            Point endPoint = CalculateEndpointOutsideElement(endElement, canvas, startPoint, out Localhost localhost);

            PathFigure pathFigure = new PathFigure { StartPoint = startPoint };
            BezierSegment bezierSegment;

            if (localhost == Localhost.Left || localhost == Localhost.Right)
            {
                bezierSegment = new BezierSegment
                {
                    Point1 = new Point((startPoint.X + endPoint.X) / 2, startPoint.Y),
                    Point2 = new Point((startPoint.X + endPoint.X) / 2, endPoint.Y),
                    Point3 = endPoint,
                };
            }
            else // if (localhost == Localhost.Top || localhost == Localhost.Bottom)
            {

                bezierSegment = new BezierSegment
                {
                    Point1 = new Point(startPoint.X, (startPoint.Y + endPoint.Y) / 2),
                    Point2 = new Point(endPoint.X, (startPoint.Y + endPoint.Y) / 2),
                    Point3 = endPoint,
                };
            }


            pathFigure.Segments.Add(bezierSegment);

            PathGeometry pathGeometry = new PathGeometry();
            pathGeometry.Figures.Add(pathFigure);
            bezierPath.Data = pathGeometry;

            Point arrowStartPoint = CalculateBezierTangent(startPoint, bezierSegment.Point3, bezierSegment.Point2, endPoint);
            UpdateArrowPath(endPoint, arrowStartPoint, arrowPath);
        }

        private static Point CalculateBezierTangent(Point startPoint, Point controlPoint1, Point controlPoint2, Point endPoint)
        {
            double t = 10.0; // 末端点

            // 计算贝塞尔曲线在 t = 1 处的一阶导数
            double dx = 3 * Math.Pow(1 - t, 2) * (controlPoint1.X - startPoint.X) +
                        6 * (1 - t) * t * (controlPoint2.X - controlPoint1.X) +
                        3 * Math.Pow(t, 2) * (endPoint.X - controlPoint2.X);

            double dy = 3 * Math.Pow(1 - t, 2) * (controlPoint1.Y - startPoint.Y) +
                        6 * (1 - t) * t * (controlPoint2.Y - controlPoint1.Y) +
                        3 * Math.Pow(t, 2) * (endPoint.Y - controlPoint2.Y);

            // 返回切线向量
            return new Point(dx, dy);
        }

        // 绘制箭头
        private static void UpdateArrowPath(Point endPoint,
                                            Point controlPoint,
                                            System.Windows.Shapes.Path arrowPath)
        {

            double arrowLength = 10;
            double arrowWidth = 5;

            Vector direction = endPoint - controlPoint;
            direction.Normalize();

            Point arrowPoint1 = endPoint + direction * arrowLength + new Vector(-direction.Y, direction.X) * arrowWidth;
            Point arrowPoint2 = endPoint + direction * arrowLength + new Vector(direction.Y, -direction.X) * arrowWidth;

            PathFigure arrowFigure = new PathFigure { StartPoint = endPoint };
            arrowFigure.Segments.Add(new LineSegment(arrowPoint1, true));
            arrowFigure.Segments.Add(new LineSegment(arrowPoint2, true));
            arrowFigure.Segments.Add(new LineSegment(endPoint, true));

            PathGeometry arrowGeometry = new PathGeometry();
            arrowGeometry.Figures.Add(arrowFigure);

            arrowPath.Data = arrowGeometry;

        }
        // 计算终点落点位置
        private static Point CalculateEndpointOutsideElement(FrameworkElement element, Canvas canvas, Point startPoint, out Localhost localhost)
        {
            Point centerPoint = element.TranslatePoint(new Point(element.ActualWidth / 2, element.ActualHeight / 2), canvas);
            Vector direction = centerPoint - startPoint;
            direction.Normalize();



            var tx = centerPoint.X - startPoint.X;
            var ty = startPoint.Y - centerPoint.Y;


            localhost = (tx < ty, Math.Abs(tx) > Math.Abs(ty)) switch
            {
                (true, true) => Localhost.Right,
                (true, false) => Localhost.Bottom,
                (false, true) => Localhost.Left,
                (false, false) => Localhost.Top,
            };

            double halfWidth = element.ActualWidth / 2 + 6;
            double halfHeight = element.ActualHeight / 2 + 6;
            double margin = 0;

            if (localhost == Localhost.Left)
            {
                centerPoint.X -= halfWidth;
                centerPoint.Y -= direction.Y / Math.Abs(direction.X) * halfHeight - margin;
            }
            else if (localhost == Localhost.Right)
            {
                centerPoint.X -= -halfWidth;
                centerPoint.Y -= direction.Y / Math.Abs(direction.X) * halfHeight - margin;
            }
            else if (localhost == Localhost.Top)
            {
                centerPoint.Y -= halfHeight;
                centerPoint.X -= direction.X / Math.Abs(direction.Y) * halfWidth - margin;
            }
            else if (localhost == Localhost.Bottom)
            {
                centerPoint.Y -= -halfHeight;
                centerPoint.X -= direction.X / Math.Abs(direction.Y) * halfWidth - margin;
            }

            return centerPoint;
        }

        public static SolidColorBrush GetStroke(ConnectionType currentConnectionType)
        {
            return currentConnectionType switch
            {
                ConnectionType.IsSucceed => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#04FC10")),
                ConnectionType.IsFail => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F18905")),
                ConnectionType.IsError => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FE1343")),
                ConnectionType.Upstream => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A82E4")),
                _ => throw new Exception(),
            };
        }

    }
    #endregion

}