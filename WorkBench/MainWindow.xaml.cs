using Serein.WorkBench.Node;
using Serein.WorkBench.Node.View;
using Serein.WorkBench.Themes;
using Serein.WorkBench.tool;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Serein;
using Serein.DynamicFlow;
using Serein.DynamicFlow.NodeModel;
using Serein.DynamicFlow.Tool;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using static Serein.WorkBench.Connection;
using DynamicDemo.Node;

namespace Serein.WorkBench
{

    public static class MouseNodeType
    {
        public static string RegionType { get; } = nameof(RegionType);
        public static string BaseNodeType { get; } = nameof(BaseNodeType);
        public static string DllNodeType { get; } = nameof(DllNodeType);
    }


    /// <summary>
    /// 表示两个节点之间的连接关系（UI层面）
    /// </summary>
    public class Connection
    {
        

        public required NodeControlBase Start { get; set; } // 起始TextBlock
        public required NodeControlBase End { get; set; }   // 结束TextBlock
        public required Line Line { get; set; }       // 连接的线
        public ConnectionType Type { get; set; }       // 连接的线是否为真分支或者假分支

        private Storyboard? _animationStoryboard; // 动画Storyboard

        /// <summary>
        /// 从Canvas中移除连接线
        /// </summary>
        /// <param name="canvas"></param>
        public void RemoveFromCanvas(Canvas canvas)
        {
            canvas.Children.Remove(Line); // 移除线
            _animationStoryboard?.Stop(); // 停止动画
        }

        /// <summary>
        /// 开始动画
        /// </summary>
        public void StartAnimation()
        {
            // 停止现有的动画
            _animationStoryboard?.Stop();

            // 计算线条的长度
            double length = Math.Sqrt(Math.Pow(Line.X2 - Line.X1, 4) + Math.Pow(Line.Y2 - Line.Y1, 4));
            double dashLength = length / 200;

            // 创建新的 DoubleAnimation 反转方向
            var animation = new DoubleAnimation
            {
                From = dashLength,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.5),
                RepeatBehavior = RepeatBehavior.Forever
            };

            // 设置线条的样式
            Line.Stroke = Type == ConnectionType.IsTrue ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#04FC10"))
                        : Type == ConnectionType.IsFalse ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F18905"))
                                                         : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AB616B"));
            Line.StrokeDashArray = [dashLength, dashLength];

            // 创建新的 Storyboard
            _animationStoryboard = new Storyboard();
            _animationStoryboard.Children.Add(animation);
            Storyboard.SetTarget(animation, Line);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Line.StrokeDashOffsetProperty));

            // 开始动画
            _animationStoryboard.Begin();
        }

        /// <summary>
        /// 停止动画
        /// </summary>
        public void StopAnimation()
        {
            if (_animationStoryboard != null)
            {
                _animationStoryboard.Stop();
                Line.Stroke = Type == ConnectionType.IsTrue ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#04FC10"))
                            : Type == ConnectionType.IsFalse ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F18905"))
                                                         : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AB616B"));
                Line.StrokeDashArray = null;
            }
        }
    }


    /// <summary>
    /// Interaction logic for MainWindow.xaml，第一次用git，不太懂
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 节点的命名空间
        /// </summary>
        public const string NodeSpaceName = $"{nameof(Serein)}.{nameof(Serein.DynamicFlow)}.{nameof(Serein.DynamicFlow.NodeModel)}";
        /// <summary>
        /// 一种轻量的IOC容器
        /// </summary>
        private ServiceContainer ServiceContainer { get; } = new ServiceContainer();

        /// <summary>
        /// 全局捕获Console输出事件，打印在这个窗体里面
        /// </summary>
        private readonly LogWindow logWindow;

        /// <summary>
        /// 存储加载的程序集
        /// </summary>
        private readonly List<Assembly> loadedAssemblies = [];
        /// <summary>
        /// 存储加载的程序集路径
        /// </summary>
        private readonly List<string> loadedAssemblyPaths = [];

        /// <summary>
        /// 存储所有方法信息
        /// </summary>
        ConcurrentDictionary<string, MethodDetails> DictMethodDetail = [];
        /// <summary>
        /// 存储所有与节点有关的控件
        /// </summary>
        private readonly List<NodeControlBase> nodeControls = [];
        // private readonly List<NodeBase> nodeBases = [];
        /// <summary>
        /// 存储所有的连接
        /// </summary>
        private readonly List<Connection> connections = [];

        /// <summary>
        /// 存放触发器节点（运行时全部调用）
        /// </summary>
        private readonly List<SingleFlipflopNode> flipflopNodes = [];

        ///// <summary>
        ///// 运行前的初始化（实例化类型）
        ///// </summary>
        //private readonly List<MethodDetails> loadingMethods = [];
        ///// <summary>
        ///// 初始化后属性注入以及某些需要设置的状态（注入依赖项）
        ///// </summary>
        //private readonly List<MethodDetails> initMethods = [];
        ///// <summary>
        ///// 结束运行时需要调用的方法
        ///// </summary>
        //private readonly List<MethodDetails> exitMethods = [];

        /// <summary>
        /// 记录拖动开始时的鼠标位置
        /// </summary>
        private Point startPoint;
        /// <summary>
        /// 流程图起点的控件
        /// </summary>
        private NodeControlBase? flowStartBlock;
        /// <summary>
        /// 记录开始连接的文本块
        /// </summary>
        private NodeControlBase? startConnectBlock;
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
        /// 标记是否正在拖动控件
        /// </summary>
        private bool IsDragging;




        public MainWindow()
        {
            InitializeComponent();
            logWindow = new LogWindow();
            logWindow.Show();

            // 重定向 Console 输出
            var logTextWriter = new LogTextWriter(WriteLog);
            Console.SetOut(logTextWriter);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            logWindow.Close();
            System.Windows.Application.Current.Shutdown();
        }

        public void WriteLog(string message)
        {
            logWindow.AppendText(message);
        }

        #region 加载 DynamicNodeFlow 文件
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var nf = App.FData;
            if (nf != null)
            {
                InitializeCanvas(nf.basic.canvas.width, nf.basic.canvas.lenght);
                LoadDll(nf);
                LoadNodes(nf);

                var startNode = nodeControls.FirstOrDefault(item => item.Node.Guid.Equals(nf.startNode));
                if (startNode != null)
                {
                    startNode.Node.IsStart = true;
                    SetIsStartBlock(startNode);
                }
            }
        }

        private void InitializeCanvas(double width, double height)
        {
            FlowChartCanvas.Width = width;
            FlowChartCanvas.Height = height;
        }

        /// <summary>
        /// 加载配置文件时加载DLL
        /// </summary>
        /// <param name="nf"></param>
        private void LoadDll(SereinOutputFileData nf)
        {
            var dllPaths = nf.library.Select(it => it.path).ToList();
            foreach (var dll in dllPaths)
            {
                var filePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(App.FileDataPath, dll));
                LoadAssembly(filePath);
            }
        }

        /// <summary>
        /// 加载配置文件时加载节点/区域
        /// </summary>
        /// <param name="nf"></param>
        private void LoadNodes(SereinOutputFileData nf)
        {
            var nodeControls = new Dictionary<string, NodeControlBase>();
            var regionControls = new Dictionary<string, NodeControlBase>();

            foreach (var nodeInfo in nf.nodes)
            {
                NodeControlBase? nodeControl = CreateNodeControl(nodeInfo);// 加载DLL时创建控件
                if (nodeControl != null)
                {
                    ConfigureNodeControl(nodeInfo, nodeControl, nodeControls, regionControls);
                }
                else
                {
                    WriteLog($"无法为节点类型创建节点控件: {nodeInfo.name}\r\n");
                }
            }

            FlowChartCanvas.UpdateLayout();
            LoadRegionChildNodes(nf, regionControls);
            StartConnectNodeControls(nf, nodeControls);
        }


        /// <summary>
        /// 加载配置文件时加载区域子项
        /// </summary>
        /// <param name="nf"></param>
        /// <param name="regionControls"></param>
        private void LoadRegionChildNodes(SereinOutputFileData nf, Dictionary<string, NodeControlBase> regionControls)
        {
            foreach (var region in nf.regions)
            {
                foreach (var childNode in region.childNodes)
                {
                    if (regionControls.TryGetValue(region.guid, out var regionControl))
                    {
                        var nodeControl = CreateNodeControl(childNode); // 加载区域的子项
                        if (nodeControl != null)
                        {
                            if (regionControl is ConditionRegionControl conditionRegion)
                            {
                                conditionRegion.AddCondition(nodeControl);
                            }
                            else if (regionControl is ActionRegionControl actionRegionControl)
                            {
                                actionRegionControl.AddAction(nodeControl);
                            }
                        }
                        else
                        {
                            WriteLog($"无法为节点类型创建节点控件: {childNode.name}\r\n");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 加载配置文件时开始连接节点/区域
        /// </summary>
        /// <param name="nf"></param>
        /// <param name="nodeControls"></param>
        private void StartConnectNodeControls(SereinOutputFileData nf, Dictionary<string, NodeControlBase> nodeControls)
        {
            foreach (var node in nf.nodes)
            {
                if (nodeControls.TryGetValue(node.guid, out var fromNode))
                {
                    ConnectNodeControlChildren(fromNode, node.trueNodes, nodeControls, ConnectionType.IsTrue);
                    ConnectNodeControlChildren(fromNode, node.falseNodes, nodeControls, ConnectionType.IsFalse);
                }
            }
        }
        /// <summary>
        /// 加载配置文件时递归连接节点/区域 1
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <param name="fromNode"></param>
        /// <param name="childNodeGuids"></param>
        /// <param name="nodeControls"></param>
        /// <param name="isTrueNode"></param>
        private void ConnectNodeControlChildren(NodeControlBase fromNode,
                                                string[] childNodeGuids,
                                                Dictionary<string, NodeControlBase> nodeControls,
                                                ConnectionType connectionType)
        {
            foreach (var childNodeGuid in childNodeGuids)
            {
                if (nodeControls.TryGetValue(childNodeGuid, out var toNode))
                {
                    ConnectNodeControls(fromNode, toNode, connectionType);
                }
            }
        }

        /// <summary>
        /// 加载配置文件时递归连接节点/区域 2
        /// </summary>
        /// <param name="fromNode"></param>
        /// <param name="toNode"></param>
        /// <param name="isTrueNode"></param>
        private void ConnectNodeControls(NodeControlBase fromNode, NodeControlBase toNode, ConnectionType connectionType)
        {
            if (fromNode != null && toNode != null && fromNode != toNode)
            {
                var line = new Line
                {

                    IsHitTestVisible = true,
                    Stroke = connectionType == ConnectionType.IsTrue ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#04FC10"))
                            : connectionType == ConnectionType.IsFalse ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F18905"))
                                             : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AB616B")),

                    StrokeThickness = 2,
                    X1 = Canvas.GetLeft(fromNode) + fromNode.ActualWidth / 2,
                    Y1 = Canvas.GetTop(fromNode) + fromNode.ActualHeight / 2,
                    X2 = Canvas.GetLeft(toNode) + toNode.ActualWidth / 2,
                    Y2 = Canvas.GetTop(toNode) + toNode.ActualHeight / 2
                };

                ConfigureLineContextMenu(line);
                FlowChartCanvas.Children.Add(line);

                if (connectionType == ConnectionType.IsTrue)
                {
                    fromNode.Node.TrueBranch.Add(toNode.Node);
                }
                else if (connectionType == ConnectionType.IsFalse)
                {
                    fromNode.Node.FalseBranch.Add(toNode.Node);
                }
                else if (connectionType == ConnectionType.IsEx)
                {
                    fromNode.Node.ExBranch.Add(toNode.Node);
                }

                toNode.Node.PreviousNodes.Add(fromNode.Node);
                connections.Add(new Connection { Start = fromNode, End = toNode, Line = line, Type = connectionType });
            }

            EndConnection();
        }


        /// <summary>
        /// 加载配置文件时配置节点
        /// </summary>
        /// <param name="nodeConfig">节点配置数据</param>
        /// <param name="nodeControl">需要配置的节点</param>
        /// <param name="nodeControls">节点列表</param>
        /// <param name="regionControls">区域列表</param>
        private void ConfigureNodeControl(NodeInfo nodeConfig, NodeControlBase nodeControl, Dictionary<string, NodeControlBase> nodeControls, Dictionary<string, NodeControlBase> regionControls)
        {
            FlowChartCanvas.Dispatcher.Invoke(() =>
            {
                FlowChartCanvas.Children.Add(nodeControl);
                Canvas.SetLeft(nodeControl, nodeConfig.position.x);
                Canvas.SetTop(nodeControl, nodeConfig.position.y);
                nodeControls[nodeConfig.guid] = nodeControl;
                this.nodeControls.Add(nodeControl);

                if (nodeControl is ActionRegionControl || nodeControl is ConditionRegionControl)//如果是区域，则需要创建区域
                {
                    regionControls[nodeConfig.guid] = nodeControl;
                }

                ConfigureContextMenu(nodeControl); // 创建区域
                ConfigureNodeEvents(nodeControl); // 创建区域事件（UI相关）
            });
        }

        /// <summary>
        /// 加载配置文件时创建控件
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        private NodeControlBase CreateNodeControl(NodeInfo nodeInfo)
        {
            if (!DllMethodDetails.TryGetValue(nodeInfo.name, out var md))
            {
                WriteLog($"目标节点不存在方法信息: {nodeInfo.name}\r\n"); 
                return null;
            }

            NodeControlBase control = nodeInfo.type switch
            {
                $"{NodeSpaceName}.{nameof(SingleActionNode)}" => CreateNodeControl<SingleActionNode, ActionNodeControl>(md),
                $"{NodeSpaceName}.{nameof(SingleConditionNode)}" => CreateNodeControl<SingleConditionNode, ConditionNodeControl>(md),
                $"{NodeSpaceName}.{nameof(SingleFlipflopNode)}" => CreateNodeControl<SingleFlipflopNode, FlipflopNodeControl>(md),
                $"{NodeSpaceName}.{nameof(CompositeActionNode)}" => CreateNodeControl<CompositeActionNode, ActionRegionControl>(),
                $"{NodeSpaceName}.{nameof(CompositeConditionNode)}" => CreateNodeControl<CompositeConditionNode, ConditionRegionControl>(),
                _ => throw new NotImplementedException($"非预期的节点类型{nodeInfo.type}"),
            };

            // 如果是触发器，则需要添加到集合中
            if (control is FlipflopNodeControl flipflopNodeControl && flipflopNodeControl.Node is SingleFlipflopNode flipflopNode)
            {
                var guid = flipflopNode.Guid;
                if (!flipflopNodes.Exists(it => it.Guid.Equals(guid)))
                {
                    flipflopNodes.Add(flipflopNode);
                }
            }
            return control;// DNF文件加载时创建

            


            /* NodeControl? nodeControl = nodeInfo.type switch
           {
               $"{NodeSpaceName}.{nameof(SingleActionNode)}" => CreateActionNodeControl(md),
               $"{NodeSpaceName}.{nameof(SingleConditionNode)}" => CreateConditionNodeControl(md),
               $"{NodeSpaceName}.{nameof(CompositeActionNode)}" => CreateCompositeActionNodeControl(md),
               $"{NodeSpaceName}.{nameof(CompositeConditionNode)}" => CreateCompositeConditionNodeControl(md),
               _ => null
           };*/
        }

        #endregion

        #region 节点控件的创建

        private static TControl CreateNodeControl<TNode, TControl>(MethodDetails? md = null) 
            where TNode : NodeBase
            where TControl : NodeControlBase
        {
            var nodeObj = Activator.CreateInstance(typeof(TNode));
            var nodeBase = nodeObj as NodeBase;
            if (nodeBase == null)
            {
                throw new Exception("无法创建节点控件");
            }

            nodeBase.Guid = Guid.NewGuid().ToString();
            if (md != null)
            {
                nodeBase.DelegateName = md.MethodName;
                nodeBase.DisplayName = md.MethodName;
                nodeBase.MethodDetails = md;
            }

            var controlObj = Activator.CreateInstance(typeof(TControl), [nodeObj] );
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
        /// 配置节点右键菜单
        /// </summary>
        /// <param name="nodeControl"></param>
        private void ConfigureContextMenu(NodeControlBase nodeControl)
        {
            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(CreateMenuItem("查看返回类型", (s, e) =>
            {
                if (nodeControl.Node?.MethodDetails?.ReturnType is Type returnType && returnType != typeof(void))
                {
                    DisplayReturnTypeTreeViewer(returnType);
                }
            }));
            contextMenu.Items.Add(CreateMenuItem("设为起点", (s, e) => SetIsStartBlock(nodeControl)));
            contextMenu.Items.Add(CreateMenuItem("删除", (s, e) => DeleteBlock(nodeControl)));
            contextMenu.Items.Add(CreateMenuItem("添加 真分支", (s, e) => StartConnection(nodeControl, ConnectionType.IsTrue)));
            contextMenu.Items.Add(CreateMenuItem("添加 假分支", (s, e) => StartConnection(nodeControl, ConnectionType.IsFalse)));

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
        /// 配置节点/区域连接的线
        /// </summary>
        /// <param name="line"></param>
        private void ConfigureLineContextMenu(Line line)
        {
            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(CreateMenuItem("删除连线", (s, e) => DeleteConnection(line)));
            contextMenu.Items.Add(CreateMenuItem("连线动画", (s, e) => AnimateConnection(line)));
            line.ContextMenu = contextMenu;
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
            nodeControl.MouseLeftButtonUp += Block_MouseLeftButtonUp_Animation;
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
                        LoadAssembly(file);
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

        /// <summary>
        /// 本地换成的方法
        /// </summary>
        // private static ConcurrentDictionary<string, Delegate> globalDicDelegates = new ConcurrentDictionary<string, Delegate>();

        private static readonly ConcurrentDictionary<string, MethodDetails> DllMethodDetails = [];
        /// <summary>
        /// 加载指定路径的DLL文件
        /// </summary>
        /// <param name="dllPath"></param>
        private void LoadAssembly(string dllPath)
        {
            try
            {
                Assembly assembly = Assembly.LoadFrom(dllPath); // 加载DLL文件
                loadedAssemblies.Add(assembly); // 将加载的程序集添加到列表中
                loadedAssemblyPaths.Add(dllPath); // 记录加载的DLL路径

                Type[] types = assembly.GetTypes(); // 获取程序集中的所有类型

                List<MethodDetails> conditionMethods = [];
                List<MethodDetails> actionMethods = [];
                List<MethodDetails> flipflopMethods = [];

                /* // 遍历类型，根据接口分类
                 foreach (Type type in types)
                 {
                     if (typeof(ICondition).IsAssignableFrom(type) && type.IsClass)
                     {
                         conditionTypes.Add(type); // 条件类型
                     }
                     if (typeof(IAction).IsAssignableFrom(type) && type.IsClass)
                     {
                         actionTypes.Add(type); // 动作类型
                     }
                     if (typeof(IState).IsAssignableFrom(type) && type.IsClass)
                     {
                         stateTypes.Add(type); // 状态类型
                     }
                 }*/

                var scanTypes = assembly.GetTypes()
                           .Where(t => t.GetCustomAttribute<DynamicFlowAttribute>()?.Scan == true).ToList();

                foreach (var type in scanTypes)
                {
                    //加载DLL
                    var dict = DelegateGenerator.GenerateMethodDetails(ServiceContainer, type);

                    foreach (var detail in dict)
                    {
                        WriteLog($"Method: {detail.Key}, Type: {detail.Value.MethodDynamicType}\r\n");
                        DllMethodDetails.TryAdd(detail.Key, detail.Value);

                        // 根据 DynamicType 分类
                        switch (detail.Value.MethodDynamicType)
                        {
                            case DynamicNodeType.Condition:
                                conditionMethods.Add(detail.Value);
                                break;
                            case DynamicNodeType.Action:
                                actionMethods.Add(detail.Value);
                                break;
                            case DynamicNodeType.Flipflop:
                                flipflopMethods.Add(detail.Value);
                                break;
                            //case DynamicNodeType.Init:
                            //    initMethods.Add(detail.Value);
                            //    break;
                            //case DynamicNodeType.Loading:
                            //    loadingMethods.Add(detail.Value);
                            //    break;
                            //case DynamicNodeType.Exit:
                            //    exitMethods.Add(detail.Value);
                            //    break;
                        }

                        DictMethodDetail.TryAdd(detail.Key, detail.Value);
                        // 将委托缓存到全局字典
                        DelegateCache.GlobalDicDelegates.TryAdd(detail.Key, detail.Value.MethodDelegate);
                        //globalDicDelegates.TryAdd(kvp.Key, kvp.Value.MethodDelegate);

                    }
                }

                // 遍历类型，根据接口分类
                /*foreach (Type type in types)
                {
                    // 确保类型是一个类并且实现了ICondition、IAction、IState中的一个或多个接口
                    if (type.IsClass && (typeof(ICondition).IsAssignableFrom(type) || typeof(IAction).IsAssignableFrom(type) || typeof(IState).IsAssignableFrom(type)))
                    {
                        // 使用反射创建实例
                        var instance = Activator.CreateInstance(type);
                        // 获取 InitDynamicDelegate 方法
                        
                        var method = type.GetMethod("InitDynamicDelegate");
                        if (method != null)
                        {
                            // 调用 InitDynamicDelegate 方法
                            var result = method.Invoke(instance, null);
                            if (result is ConcurrentDictionary<string, MethodDetails> dic)
                            {
                                foreach (var kvp in dic)
                                {
                                    // 根据 DynamicType 分类
                                    if (kvp.Value.MethodDynamicType == DynamicType.Condition)
                                    {
                                        conditionMethods.Add(kvp.Value);
                                    }
                                    else if (kvp.Value.MethodDynamicType == DynamicType.Action)
                                    {
                                        actionMethods.Add(kvp.Value);
                                    }
                                    //else if (kvp.Value == DynamicType.State)
                                    //{
                                    //    stateTypes.Add(type);
                                    //}

                                    // 将委托缓存到全局字典
                                    globalDicDelegates.TryAdd(kvp.Key, kvp.Value.MethodDelegate);
                                }
                            }
                        }
                    }
                }*/

                // 显示加载的DLL信息
                DisplayControlDll(assembly, conditionMethods, actionMethods, flipflopMethods);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                MessageBox.Show($"加载程序集失败: {ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 显示DLL信息
        /// </summary>
        /// <param name="assembly">dll对象</param>
        /// <param name="conditionTypes">条件接口</param>
        /// <param name="actionTypes">动作接口</param>
        /// <param name="stateTypes">状态接口</param>
        private void DisplayControlDll(Assembly assembly,
                                List<MethodDetails> conditionTypes,
                                List<MethodDetails> actionTypes,
                                List<MethodDetails> flipflopMethods)
        {

            var dllControl = new DllControl
            {
                Header = "DLL name :  " + assembly.GetName().Name // 设置控件标题为程序集名称
            };

            foreach (var item in conditionTypes)
            {
                dllControl.AddCondition(item);  // 添加动作类型到控件
            }

            foreach (var item in actionTypes)
            {
                dllControl.AddAction(item);  // 添加状态类型到控件
            }
            foreach (var item in flipflopMethods)
            {
                dllControl.AddFlipflop(item);  // 添加触发器方法到控件
            }

            /*foreach (var item in stateTypes)
            {
                dllControl.AddState(item);
            }*/

            DllStackPanel.Children.Add(dllControl);  // 将控件添加到界面上显示
        }
        #endregion

        #region 左侧功能区拖拽到区域

        private void ConditionNodeControl_Drop(object sender, DragEventArgs e)
        {

        }

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
                var dragData = new DataObject(MouseNodeType.BaseNodeType, control.GetType());
                DragDrop.DoDragDrop(control, dragData, DragDropEffects.Move);
            }
        }

        private void ConditionRegionControl_Drop(object sender, DragEventArgs e)
        {
            //if (e.Data.GetDataPresent(MouseNodeType.DllNodeType))
            //{
            //    MethodDetails methodDetails = e.Data.GetData(MouseNodeType.DllNodeType) as MethodDetails;
            //    if (methodDetails == null) return;
            //    var droppedType = methodDetails.MInstance.GetType();
            //    ICondition condition = (ICondition)Activator.CreateInstance(droppedType);
            //    var baseNode = new SingleConditionNode(condition);// 放置新的节点
            //    var node = new ConditionNodeControl(baseNode)
            //    {
            //        DataContext = droppedType,
            //        Header = methodDetails.MethodName,
            //    };
            //    baseNode.MethodDetails = methodDetails;

            //    ConditionRegionControl.AddCondition(baseNode);
            //}
        }

        private void ActionRegionControl_Drop(object sender, DragEventArgs e)
        {
            //if (e.Data.GetDataPresent(MouseNodeType.DllNodeType))
            //{
            //    MethodDetails methodDetails = e.Data.GetData(MouseNodeType.DllNodeType) as MethodDetails;
            //    if (methodDetails == null) return;
            //    var droppedType = methodDetails.MInstance.GetType();
            //    IAction action = (IAction)Activator.CreateInstance(droppedType);
            //    var baseNode = new SingleActionNode(action);// 放置新的节点
            //    baseNode.MethodDetails = methodDetails;

            //    ActionRegionControl.AddAction(baseNode, false);

            //}
        }

        private void StateRegionControl_Drop(object sender, DragEventArgs e)
        {
            // 处理区域的拖拽
        }

        private void RegionControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is UserControl control)
            {
                // 创建一个 DataObject 用于拖拽操作，并设置拖拽效果
                var dragData = new DataObject(MouseNodeType.RegionType, control.GetType());
                DragDrop.DoDragDrop(control, dragData, DragDropEffects.Move);
            }
        }
        #endregion

        #region 与流程图相关的拖拽操作

        /// <summary>
        /// 放置操作，根据拖放数据创建相应的控件，并处理相关操作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlowChartCanvas_Drop(object sender, DragEventArgs e)
        {
            NodeControlBase? nodeControl = null;
            Point dropPosition = e.GetPosition(FlowChartCanvas);

            if (e.Data.GetDataPresent(MouseNodeType.RegionType)) // 拖动左侧功能区的区域控件
            {
                if (e.Data.GetData(MouseNodeType.RegionType) is Type type)
                {
                    nodeControl = CreateNodeForRegionType(type);
                }
            }
            else if (e.Data.GetDataPresent(MouseNodeType.BaseNodeType)) // 基础控件
            {
                if (e.Data.GetData(MouseNodeType.BaseNodeType) is Type type)
                {
                    nodeControl = CreateNodeForBase(type);
                }
            }
            else if (e.Data.GetDataPresent(MouseNodeType.DllNodeType)) // 拖动dll的控件
            {
                if(e.Data.GetData(MouseNodeType.DllNodeType) is MethodDetails methodDetails)
                {
                    nodeControl = CreateNodeForMethodDetails(methodDetails); // 创建新节点
                }
            }

            if (nodeControl != null)
            {
                // 尝试放置节点
                if (TryPlaceNodeInRegion(nodeControl, dropPosition, e)) 
                {
                    return;
                }

                PlaceNodeOnCanvas(nodeControl, dropPosition);
            }

            e.Handled = true;
        }

        /// <summary>
        /// 拖拽创建区域
        /// </summary>
        /// <param name="droppedType"></param>
        /// <returns></returns>
        private NodeControlBase? CreateNodeForRegionType(Type droppedType)
        {
            return droppedType switch
            {
                Type when typeof(ConditionRegionControl).IsAssignableFrom(droppedType) 
                    => CreateNodeControl<CompositeConditionNode, ConditionRegionControl>(), // 条件区域

                //Type when typeof(CompositeActionNode).IsAssignableFrom(droppedType) 
                //    => CreateNodeControl<CompositeActionNode,ActionRegionControl>(),   // 动作区域

                _ => throw new NotImplementedException("非预期的区域类型"),
            };
           
        }
        /// <summary>
        /// 拖拽创建来自基础节点
        /// </summary>
        /// <param name="methodDetails"></param>
        /// <returns></returns>
        private NodeControlBase? CreateNodeForBase(Type droppedType)
        {
            return droppedType switch
            {
                Type when typeof(ConditionNodeControl).IsAssignableFrom(droppedType)
                    => CreateNodeControl<SingleConditionNode, ConditionNodeControl>(), // 条件控件
                Type when typeof(ExpOpNodeControl).IsAssignableFrom(droppedType)
                    => CreateNodeControl<SingleExpOpNode, ExpOpNodeControl>(), // 操作表达式控件
                _ => throw new NotImplementedException("非预期的基础节点类型"),
            };
        }

        /// <summary>
        /// 拖拽创建来自DLL的节点
        /// </summary>
        /// <param name="methodDetails"></param>
        /// <returns></returns>
        private NodeControlBase? CreateNodeForMethodDetails(MethodDetails methodDetails)
        {

            NodeControlBase control = methodDetails.MethodDynamicType switch
            {
                //DynamicNodeType.Condition => CreateNodeControl(typeof(SingleConditionNode), methodDetails), // 单个条件控件
                DynamicNodeType.Action => CreateNodeControl<SingleActionNode, ActionNodeControl>(methodDetails),// 单个动作控件
                DynamicNodeType.Flipflop => CreateNodeControl<SingleFlipflopNode, FlipflopNodeControl>(methodDetails), // 单个动作控件
                _ => throw new NotImplementedException("非预期的Dll节点类型"),
            };

            // 如果是触发器，则需要添加到集合中
            if (control is FlipflopNodeControl flipflopNodeControl && flipflopNodeControl.Node is SingleFlipflopNode flipflopNode)
            {
                var guid = flipflopNode.Guid;
                if (!flipflopNodes.Exists(it => it.Guid.Equals(guid)))
                {
                    flipflopNodes.Add(flipflopNode);
                }
            }
            return control;
        }







        /// <summary>
        /// 尝试将节点放置在区域中
        /// </summary>
        /// <param name="nodeControl"></param>
        /// <param name="dropPosition"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        private bool TryPlaceNodeInRegion(NodeControlBase nodeControl, Point dropPosition, DragEventArgs e)
        {
            HitTestResult hitTestResult = VisualTreeHelper.HitTest(FlowChartCanvas, dropPosition);
            if (hitTestResult != null && hitTestResult.VisualHit is UIElement hitElement)
            {
                var data = e.Data.GetData(MouseNodeType.BaseNodeType);

                if (data == typeof(ConditionNodeControl))
                {

                    ConditionRegionControl conditionRegion = GetParentOfType<ConditionRegionControl>(hitElement);
                    if (conditionRegion != null)
                    {
                        conditionRegion.AddCondition(nodeControl);
                        return true;
                    }



                }

                //if (e.Data.GetData(MouseNodeType.DllNodeType) is MethodDetails methodDetails)
                //{
                //    if (methodDetails.MethodDynamicType == DynamicNodeType.Condition)
                //    {
                //        ConditionRegionControl conditionRegion = GetParentOfType<ConditionRegionControl>(hitElement);
                //        if (conditionRegion != null)
                //        {
                //            conditionRegion.AddCondition(nodeControl);
                //            return true;
                //        }
                //    }
                //    else if (methodDetails.MethodDynamicType == DynamicNodeType.Action)
                //    {
                //        ActionRegionControl actionRegion = GetParentOfType<ActionRegionControl>(hitElement);
                //        if (actionRegion != null)
                //        {
                //            actionRegion.AddAction(nodeControl);
                //            return true;
                //        }
                //    }
                //}

            }
            return false;
        }
        /// <summary>
        /// 在画布上放置节点
        /// </summary>
        /// <param name="nodeControl"></param>
        /// <param name="dropPosition"></param>
        private void PlaceNodeOnCanvas(NodeControlBase nodeControl, Point dropPosition)
        {
            if (flowStartBlock == null)
            {
                SetIsStartBlock(nodeControl);
            }

            Canvas.SetLeft(nodeControl, dropPosition.X);
            Canvas.SetTop(nodeControl, dropPosition.Y);
            FlowChartCanvas.Children.Add(nodeControl);
            nodeControls.Add(nodeControl);

            ConfigureContextMenu(nodeControl); // 配置右键菜单
            ConfigureNodeEvents(nodeControl);// 配置节点UI相关的事件
            AdjustCanvasSize(nodeControl); // 更新画布
        }


        /// <summary>
        /// 获得目标类型的父类
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
        /// 鼠标左键按下事件，关闭所有连接的动画效果
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlowChartCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 关闭所有连线的动画效果
            foreach (var connection in connections)
            {
                connection.StopAnimation();
            }
        }

        /// <summary>
        /// 拖动效果，根据拖放数据是否为指定类型设置拖放效果
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlowChartCanvas_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(MouseNodeType.RegionType)
                || e.Data.GetDataPresent(MouseNodeType.DllNodeType)
                || e.Data.GetDataPresent(MouseNodeType.BaseNodeType))
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
        /// 处理控件的鼠标左键松开事件，启动或停止连接的动画效果
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Block_MouseLeftButtonUp_Animation(object sender, MouseButtonEventArgs e)
        {
            if (sender is UserControl clickedBlock)
            {
                foreach (var connection in connections)
                {
                    if (connection.Start == clickedBlock)
                    {
                        connection.StartAnimation(); // 开启动画
                    }
                }
            }
        }

        /// <summary>
        /// 控件的鼠标左键按下事件，启动拖动操作。
        /// </summary>
        private void Block_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsConnecting)
                return;

            IsDragging = true;
            startPoint = e.GetPosition(FlowChartCanvas); // 记录鼠标按下时的位置
            ((UIElement)sender).CaptureMouse(); // 捕获鼠标
        }




        /// <summary>
        /// 控件的鼠标移动事件，根据鼠标拖动更新控件的位置。
        /// </summary>
        private void Block_MouseMove(object sender, MouseEventArgs e)
        {
            if (IsDragging)
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
        /// <summary>
        /// 调整FlowChartCanvas的尺寸，确保显示所有控件。
        /// </summary>
        private void AdjustCanvasSize(UIElement element)
        {
            double right = Canvas.GetLeft(element) + ((FrameworkElement)element).ActualWidth;
            double bottom = Canvas.GetTop(element) + ((FrameworkElement)element).ActualHeight;

            bool adjusted = false;

            // 如果控件超出了FlowChartCanvas的宽度或高度，调整FlowChartCanvas的尺寸
            if (right > FlowChartCanvas.Width)
            {
                FlowChartCanvas.Width = right + 20; // 添加一些额外的缓冲空间
                adjusted = true;
            }

            if (bottom > FlowChartCanvas.Height)
            {
                FlowChartCanvas.Height = bottom + 20; // 添加一些额外的缓冲空间
                adjusted = true;
            }

            // 如果没有调整，则确保FlowChartCanvas的尺寸不小于ScrollViewer的可见区域
            if (!adjusted)
            {
                // 确保 FlowChartCanvas 的最小尺寸
                var scrollViewerViewportWidth = FlowChartScrollViewer.ViewportWidth;
                var scrollViewerViewportHeight = FlowChartScrollViewer.ViewportHeight;

                if (FlowChartCanvas.Width < scrollViewerViewportWidth)
                {
                    FlowChartCanvas.Width = scrollViewerViewportWidth;
                }

                if (FlowChartCanvas.Height < scrollViewerViewportHeight)
                {
                    FlowChartCanvas.Height = scrollViewerViewportHeight;
                }
            }
        }

        /// <summary>
        /// 开始创建连接 True线 操作，设置起始块和绘制连接线。
        /// </summary>
        private void StartConnection(NodeControlBase startBlock, ConnectionType connectionType)
        {
            var tf = connections.FirstOrDefault(it => it.Start == startBlock)?.Type;

            /*if (!TorF && startBlock.Node.MethodDetails.MethodDynamicType == DynamicNodeType.Action)
            {
                MessageBox.Show($"动作节点不允许存在假分支");
                return;
            }*/

            currentConnectionType = connectionType;

            IsConnecting = true;
            startConnectBlock = startBlock;

            // 确保起点和终点位置的正确顺序
            currentLine = new Line
            {
                Stroke = connectionType == ConnectionType.IsTrue ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#04FC10"))
                        : connectionType == ConnectionType.IsFalse ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F18905"))
                                                         : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AB616B")),
                StrokeDashArray = new DoubleCollection([2]),
                StrokeThickness = 2,
                X1 = Canvas.GetLeft(startConnectBlock) + startConnectBlock.ActualWidth / 2,
                Y1 = Canvas.GetTop(startConnectBlock) + startConnectBlock.ActualHeight / 2,
                X2 = Canvas.GetLeft(startConnectBlock) + startConnectBlock.ActualWidth / 2, // 初始时终点与起点重合
                Y2 = Canvas.GetTop(startConnectBlock) + startConnectBlock.ActualHeight / 2,
            };
             
            FlowChartCanvas.Children.Add(currentLine);
            FlowChartCanvas.MouseMove += FlowChartCanvas_MouseMove;
            this.KeyDown += MainWindow_KeyDown;
        }

        /// <summary>
        /// FlowChartCanvas中移动时处理，用于实时更新连接线的终点位置。
        /// </summary>
        private void FlowChartCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (IsConnecting)
            {
                Point position = e.GetPosition(FlowChartCanvas);
                if(currentLine == null || startConnectBlock == null)
                {
                    return;
                }
                currentLine.X1 = Canvas.GetLeft(startConnectBlock) + startConnectBlock.ActualWidth / 2;
                currentLine.Y1 = Canvas.GetTop(startConnectBlock) + startConnectBlock.ActualHeight / 2;
                currentLine.X2 = position.X;
                currentLine.Y2 = position.Y;
            }
        }


        /// <summary>
        /// 控件的鼠标左键松开事件，结束拖动操作，创建连线
        /// </summary>
        private void Block_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsDragging)
            {
                IsDragging = false;
                ((UIElement)sender).ReleaseMouseCapture();  // 释放鼠标捕获
            }
            else if (IsConnecting)
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

                if (startConnectBlock != null && isRegion && startConnectBlock.Node.MethodDetails != null && startConnectBlock.Node.MethodDetails.MethodDynamicType == DynamicNodeType.Action)
                {
                    if (!targetBlock.Node.MethodDetails.IsCanConnect(startConnectBlock.Node.MethodDetails.ReturnType))
                    {
                       
                        string mboxStr = "类型不匹配。" +
                                        "\r\n" +
                                         $"起始节点：{startConnectBlock.Node.MethodDetails.MethodName}" + "\r\n" +
                                         $"返回类型：{startConnectBlock.Node.MethodDetails.ReturnType.Name}" + "\r\n" +
                                         "\r\n" +
                                         $"起始节点：{targetBlock.Node.MethodDetails.MethodName}" + "\r\n" +
                                         $"接收类型：{string.Join("\r\n", targetBlock.Node.MethodDetails.ExplicitDatas.Select(it => it.ToString()))}" + "\r\n";

                        MessageBox.Show(mboxStr);
                        return;
                    }
                }

                if (startConnectBlock != null && targetBlock != null && startConnectBlock != targetBlock)
                {

                    // 创建连接线
                    Line line = new()
                    {
                        IsHitTestVisible = true,
                        Stroke = currentConnectionType == ConnectionType.IsTrue ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#04FC10"))
                                : currentConnectionType == ConnectionType.IsFalse ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F18905"))
                                                                                  : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AB616B")),
                        StrokeThickness = 2,
                        X1 = Canvas.GetLeft(startConnectBlock) + startConnectBlock.ActualWidth / 2,
                        Y1 = Canvas.GetTop(startConnectBlock) + startConnectBlock.ActualHeight / 2,
                        X2 = Canvas.GetLeft(targetBlock) + targetBlock.ActualWidth / 2,
                        Y2 = Canvas.GetTop(targetBlock) + targetBlock.ActualHeight / 2
                    };

                    // 添加右键菜单删除连线
                    ContextMenu lineContextMenu = new();
                    MenuItem deleteLineMenuItem = new() { Header = "删除连线" };
                    deleteLineMenuItem.Click += (s, args) => DeleteConnection(line);
                    MenuItem animateLineMenuItem = new() { Header = "连线动画" };
                    animateLineMenuItem.Click += (s, args) => AnimateConnection(line);
                    lineContextMenu.Items.Add(deleteLineMenuItem);
                    lineContextMenu.Items.Add(animateLineMenuItem);
                    line.ContextMenu = lineContextMenu;
                    FlowChartCanvas.Children.Add(line);


                    if (currentConnectionType == ConnectionType.IsTrue)
                    {
                        startConnectBlock.Node.TrueBranch.Add(targetBlock.Node);
                    }
                    else if (currentConnectionType == ConnectionType.IsFalse)
                    {
                        startConnectBlock.Node.FalseBranch.Add(targetBlock.Node);
                    }
                    else if (currentConnectionType == ConnectionType.IsEx)
                    {
                        startConnectBlock.Node.ExBranch.Add(targetBlock.Node);
                    }

                    targetBlock.Node.PreviousNodes.Add(startConnectBlock.Node); // 将当前发起连接的节点，添加到被连接的节点的上一节点队列。（用于回溯）

                    // 保存连接关系
                    connections.Add(new Connection { Start = startConnectBlock, End = targetBlock, Line = line, Type = currentConnectionType });
                }
                EndConnection();
            }
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
            startConnectBlock = null;
            FlowChartCanvas.MouseMove -= FlowChartCanvas_MouseMove;

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
            foreach (var connection in connections)
            {
                if (connection.Start == block || connection.End == block)
                {
                    connection.Line.X1 = Canvas.GetLeft(connection.Start) + connection.Start.ActualWidth / 2;
                    connection.Line.Y1 = Canvas.GetTop(connection.Start) + connection.Start.ActualHeight / 2;
                    connection.Line.X2 = Canvas.GetLeft(connection.End) + connection.End.ActualWidth / 2;
                    connection.Line.Y2 = Canvas.GetTop(connection.End) + connection.End.ActualHeight / 2;
                }
            }
        }

        /// <summary>
        /// 确保 FlowChartCanvas 的最小尺寸不小于 FlowChartScrollViewer 的可见区域的尺寸
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlowChartScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 确保 FlowChartCanvas 的最小尺寸等于 ScrollViewer 的可见尺寸
            var scrollViewerViewportWidth = FlowChartScrollViewer.ViewportWidth;
            var scrollViewerViewportHeight = FlowChartScrollViewer.ViewportHeight;

            if (FlowChartCanvas.Width < scrollViewerViewportWidth)
            {
                FlowChartCanvas.Width = scrollViewerViewportWidth;
            }

            if (FlowChartCanvas.Height < scrollViewerViewportHeight)
            {
                FlowChartCanvas.Height = scrollViewerViewportHeight;
            }
        }
        /// <summary>
        /// 删除该控件，以及与该控件相关的所有连线
        /// </summary>
        /// <param name="nodeControl"></param>
        private void DeleteBlock(NodeControlBase nodeControl)
        {
            if (nodeControl.Node.IsStart)
            {
                if (nodeControls.Count > 1)
                {
                    MessageBox.Show("若存在其它控件时，起点控件不能删除");
                    return;
                }
                flowStartBlock = null;
            }
            var RemoveEonnections = connections.Where(c => c.Start.Node.Guid.Equals(nodeControl.Node.Guid)
                                                        || c.End.Node.Guid.Equals(nodeControl.Node.Guid)).ToList();

            Remove(RemoveEonnections, nodeControl.Node);
            // 获取起点是该控件的所有连线（获取该节点的子节点）
            //var IsStartConnections = connections.Where(c => c.Start == nodeControl).ToList();
            //// 获取终点是该控件的所有连线（获取该节点的父节点）
            //var EndConnectionsToRemove = connections.Where(c => c.End == nodeControl).ToList();

            // 删除控件
            FlowChartCanvas.Children.Remove(nodeControl);
            nodeControls.Remove(nodeControl);


            /*foreach (var connection in StartConnectionsToRemove)
            {
                var StartNode = node.Node;
                var EndNode = connection.End.Node;
                if (connection.TorF)
                {
                    StartNode.TrueBranchNextNodes.Remove(EndNode);
                }
                else
                {
                    StartNode.FalseBranchNextNodes.Remove(EndNode);
                }
                EndNode.PreviousNodes.Remove(StartNode);

                connection.RemoveFromCanvas(FlowChartCanvas);
                connections.Remove(connection);

               if(node is FlipflopNodeControl flipflopNodeControl && flipflopNodeControl.Node is SingleFlipflopNode singleFlipflop)
               {
                    flipflopNodes.Remove(singleFlipflop);
               }
            }*/


            /*foreach (var connection in EndConnectionsToRemove)
            {
                var StartNode = connection.Start.Node;
                var EndNode = node.Node;
                if (connection.TorF)
                {
                    StartNode.TrueBranchNextNodes.Remove(EndNode);
                }
                else
                {
                    StartNode.FalseBranchNextNodes.Remove(EndNode);
                }
                EndNode.PreviousNodes.Remove(StartNode);

                connection.RemoveFromCanvas(FlowChartCanvas);
                connections.Remove(connection);
            }*/
        }
        /// <summary>
        /// 移除控件连接关系
        /// </summary>
        /// <param name="connections"></param>
        /// <param name="nodeControl"></param>
        private void Remove(List<Connection> connections, NodeBase targetNode)
        {
            if (connections.Count == 0)
            {
                return;
            }
            var tempArr = connections.ToArray();
            foreach (var connection in tempArr)
            {
                var startNode = connection.Start.Node;
                var endNode = connection.End.Node;
                bool IsStart = false;
                // 要删除的节点（targetNode），在连接关系中是否为起点
                // 如果是，则需要从 targetNode 中删除子节点。
                // 如果不是，则需要从连接关系中的起始节点删除 targetNode 。
                if (startNode.Guid.Equals(targetNode.Guid))
                {
                    IsStart = true;
                }

                if (connection.Type == ConnectionType.IsTrue)
                {
                    if (IsStart)
                    {
                        targetNode.TrueBranch.Remove(endNode);
                    }
                    else
                    {
                        startNode.TrueBranch.Remove(targetNode);
                    }
                }
                else if (connection.Type == ConnectionType.IsFalse)
                {
                    if (IsStart)
                    {
                        targetNode.FalseBranch.Remove(endNode);
                    }
                    else
                    {
                        startNode.FalseBranch.Remove(targetNode);
                    }
                }
                else if (connection.Type == ConnectionType.IsEx)
                {
                    if (IsStart)
                    {
                        targetNode.ExBranch.Remove(endNode);
                    }
                    else
                    {
                        startNode.ExBranch.Remove(targetNode);
                    }
                }

                connection.RemoveFromCanvas(FlowChartCanvas);
                connections.Remove(connection);

                if (startNode is SingleFlipflopNode singleFlipflopNode)
                {
                    flipflopNodes.Remove(singleFlipflopNode);
                }
            }
        }

        /// <summary>
        /// 开启以该控件为起点的动画效果
        /// </summary>
        /// <param name="line"></param>
        private void AnimateConnection(Line line)
        {
            var connectionToAnimate = connections.FirstOrDefault(c => c.Line == line);
            connectionToAnimate?.StartAnimation();
        }
        /// <summary>
        /// 设为起点
        /// </summary>
        /// <param name="node"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void SetIsStartBlock(NodeControlBase nodeControl)
        {
            if (nodeControl == null) { return; }
            if (flowStartBlock != null)
            {
                flowStartBlock.Node.IsStart = false;
                flowStartBlock.BorderBrush = Brushes.Black;
                flowStartBlock.BorderThickness = new Thickness(0);
            }

            nodeControl.Node.IsStart = true;
            nodeControl.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#04FC10"));
            nodeControl.BorderThickness = new Thickness(2);

            flowStartBlock = nodeControl;
            /* foreach (var node in _nodeControls)
             {
                 if(node == setNode)
                 {

                 }
                 else
                 {
                     node.Node.IsStart = false;

                 }

             }*/
        }
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

        /// <summary>
        /// 删除该连线
        /// </summary>
        /// <param name="line"></param>
        private void DeleteConnection(Line line)
        {
            var connectionToRemove = connections.FirstOrDefault(c => c.Line == line);
            if (connectionToRemove == null)
            {
                return;
            }
            // 获取起始节点与终止节点，消除映射关系
            var StartNode = connectionToRemove.Start.Node;
            var EndNode = connectionToRemove.End.Node;

            if (connectionToRemove.Type == ConnectionType.IsTrue)
            {
                StartNode.TrueBranch.Remove(EndNode);
            }
            else if (connectionToRemove.Type == ConnectionType.IsFalse)
            {
                StartNode.FalseBranch.Remove(EndNode);
            }
            else if (connectionToRemove.Type == ConnectionType.IsEx)
            {
                StartNode.ExBranch.Remove(EndNode);
            }

    
            EndNode.PreviousNodes.Remove(StartNode);



            if (connectionToRemove != null)
            {
                connectionToRemove.RemoveFromCanvas(FlowChartCanvas);
                connections.Remove(connectionToRemove);
            }
        }


        #endregion


        /// <summary>
        /// 卸载DLL文件，清空当前项目
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UnloadAllButton_Click(object sender, RoutedEventArgs e)
        {
            UnloadAllAssemblies();
        }
        /// <summary>
        /// 卸载DLL文件，清空当前项目
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UnloadAllAssemblies()
        {
            loadedAssemblies.Clear();
            loadedAssemblyPaths.Clear();
            DllStackPanel.Children.Clear();
            FlowChartCanvas.Children.Clear();

            connections.Clear();
            currentLine = null;
            flowStartBlock = null;
            startConnectBlock = null;
            MessageBox.Show("所有DLL已卸载。", "信息", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        private NodeFlowStarter nodeFlowStarter;

        /// <summary>
        /// 运行测试
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ButtonDebugRun_Click(object sender, RoutedEventArgs e)
        {
            logWindow?.Show();
            var nodes = nodeControls.Select(it => it.Node).ToList();
            var methodDetails = DictMethodDetail.Values.ToList();
            nodeFlowStarter ??= new NodeFlowStarter(ServiceContainer, methodDetails);
            await nodeFlowStarter.RunAsync(nodes);
            WriteLog("----------------\r\n");
        }



        private void ButtonDebugFlipflopNode_Click(object sender, RoutedEventArgs e)
        {
           nodeFlowStarter?.Exit();
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
            try
            {
                // 保存节点
                var nodeObjs = nodeControls.Select(item =>
                {
                    var node = item.Node;
                    Point positionRelativeToParent = item.TranslatePoint(new Point(0, 0), FlowChartCanvas);
                    var trueNodes = item.Node.TrueBranch.Select(item => item.Guid).ToArray();
                    var falseNodes = item.Node.FalseBranch.Select(item => item.Guid).ToArray();

                    return new
                    {
                        guid = node.Guid,
                        name = node.MethodDetails?.MethodName,
                        label = node.DisplayName ?? "",
                        type = node.GetType().ToString(),
                        position = new
                        {
                            x = positionRelativeToParent.X,
                            y = positionRelativeToParent.Y,
                        },
                        trueNodes = trueNodes,
                        falseNodes = falseNodes,
                    };
                }).ToList();


                // 保存区域
                var regionObjs = nodeControls.Where(item =>
                        item.GetType() == typeof(ConditionRegionControl) ||
                        item.GetType() == typeof(ActionRegionControl))
                    .ToList()
                    .Select(region =>
                    {
                        WriteLog(region.GetType().ToString() + "\r\n");
                        if (region is ConditionRegionControl && region.Node is CompositeConditionNode conditionRegion) // 条件区域控件
                        {
                            List<object> childNodes = [];
                            var tmpChildNodes = conditionRegion.ConditionNodes;
                            foreach (var node in tmpChildNodes)
                            {
                                WriteLog(node.GetType().ToString() + "\r\n");
                                childNodes.Add(new
                                {
                                    guid = node.Guid,
                                    name = node.MethodDetails?.MethodName,
                                    label = node.DisplayName ?? "",
                                    type = node.GetType().ToString(),
                                    position = new
                                    {
                                        x = 0,
                                        y = 0,
                                    },
                                    trueNodes = (string[])[],
                                    falseNodes = (string[])[],
                                });
                            }
                            return new
                            {
                                guid = region.Node.Guid,
                                childNodes = childNodes
                            };
                        }
                        else if (region is ActionRegionControl && region.Node is CompositeActionNode actionRegion) // 动作区域控件
                        {
                            //WriteLog(region.Node.GetType().ToString() + "\r\n");

                            List<object> childNodes = [];
                            var tmpChildNodes = actionRegion.ActionNodes;
                            foreach (var node in tmpChildNodes)
                            {
                                WriteLog(node.GetType().ToString() + "\r\n");
                                childNodes.Add(new
                                {
                                    guid = node.Guid,
                                    name = node.MethodDetails?.MethodName,
                                    label = node.DisplayName ?? "",
                                    type = node.GetType().ToString(),
                                    position = new
                                    {
                                        x = 0,
                                        y = 0,
                                    },
                                    trueNodes = (string[])[],
                                    falseNodes = (string[])[],
                                });
                            }
                            return new
                            {
                                guid = region.Node.Guid,
                                childNodes = childNodes
                            };
                        }
                        else
                        {
                            return null;
                        }
                    });


                // 将 DLL 的绝对路径转换为相对于配置文件目录的相对路径
                var dlls = loadedAssemblies.Select(assembly =>
                {
                    var temp = assembly.GetName();
                    string codeBasePath = assembly.CodeBase;
                    string filePath = new Uri(codeBasePath).LocalPath;
                    string relativePath;
                    if (string.IsNullOrEmpty(App.FileDataPath))
                    {
                        relativePath = System.IO.Path.GetFileName(filePath);
                    }
                    else
                    {
                        relativePath = GetRelativePath(App.FileDataPath, filePath);
                    }


                    var result = new
                    {
                        name = temp.Name,
                        path = relativePath,
                        tips = assembly.FullName,
                    };
                    return result;
                }).ToList();

                JObject keyValuePairs = new()
                {
                    ["basic"] = new JObject
                    {
                        ["canvas"] = new JObject
                        {
                            ["width"] = FlowChartCanvas.Width,
                            ["lenght"] = FlowChartCanvas.Height,
                        },
                        ["versions"] = "1",
                    },
                    ["library"] = JArray.FromObject(dlls),
                    ["startNode"] = flowStartBlock == null ? "" : flowStartBlock.Node.Guid,
                    ["nodes"] = JArray.FromObject(nodeObjs),
                    ["regions"] = JArray.FromObject(regionObjs),
                };
                // WriteLog(keyValuePairs.ToString());


                var savePath = SaveContentToFile(keyValuePairs.ToString());
                savePath = System.IO.Path.GetDirectoryName(savePath);
                if (string.IsNullOrEmpty(savePath))
                {
                    return;
                }
                foreach (var dll in loadedAssemblies)
                {
                    try
                    {
                        string targetPath = System.IO.Path.Combine(savePath, System.IO.Path.GetFileName(dll.CodeBase));

                        // 确保目标目录存在
                        Directory.CreateDirectory(savePath);
                        var sourceFile = new Uri(dll.CodeBase).LocalPath;
                        // 复制文件到目标目录
                        File.Copy(sourceFile, targetPath, true);
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"DLL复制失败：{dll.CodeBase} \r\n错误：{ex}\r\n");
                    }
                }
                /*string filePath = System.IO.Path.Combine(Environment.CurrentDirectory, "project.nf");

                try
                {
                    // 将文本内容写入文件
                    File.WriteAllText(filePath, keyValuePairs.ToString());

                    Console.WriteLine($"文本已成功保存到文件: {filePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"保存文件时出现错误: {ex.Message}");
                }*/

                /*if (item is SingleActionNode)
                {
                }
                else if (item is SingleConditionNode)
                {

                }
                else if (item is CompositeActionNode)
                {

                }
                else if (item is CompositeConditionNode)
                {

                }*/
            }
            catch (Exception ex)
            {
                Debug.Write(ex.Message);
            }

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
        // 计算相对路径的方法
        public static string GetRelativePath(string baseDirectory, string fullPath)
        {
            Uri baseUri = new(baseDirectory + System.IO.Path.DirectorySeparatorChar);
            Uri fullUri = new(fullPath);
            Uri relativeUri = baseUri.MakeRelativeUri(fullUri);
            return Uri.UnescapeDataString(relativeUri.ToString().Replace('/', System.IO.Path.DirectorySeparatorChar));
        }

        
    }

}