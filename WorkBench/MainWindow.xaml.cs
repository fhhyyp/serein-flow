using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.Library.Utils.SereinExpression;
using Serein.NodeFlow;
using Serein.NodeFlow.Env;
using Serein.NodeFlow.Tool;
using Serein.Workbench.Extension;
using Serein.Workbench.Node;
using Serein.Workbench.Node.View;
using Serein.Workbench.Node.ViewModel;
using Serein.Workbench.Themes;
using Serein.Workbench.Tool;
using SqlSugar.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using static Dm.net.buffer.ByteArrayBuffer;
using DataObject = System.Windows.DataObject;

namespace Serein.Workbench
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
    /// Interaction logic for MainWindow.xaml，第一次用git，不太懂
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 全局捕获Console输出事件，打印在这个窗体里面
        /// </summary>
        private readonly LogWindow LogOutWindow = new LogWindow();

        /// <summary>
        /// 流程环境装饰器，方便在本地与远程环境下切换
        /// </summary>
        private IFlowEnvironment EnvDecorator => ViewModel.FlowEnvironment;
        private MainWindowViewModel ViewModel { get; set; }


        /// <summary>
        /// 节点对应的控件类型
        /// </summary>
        // private Dictionary<NodeControlType, (Type controlType, Type viewModelType)> NodeUITypes { get; } = [];

        /// <summary>
        /// 存储所有与节点有关的控件
        /// 任何情景下都应避免直接操作 ViewModel 中的 NodeModel 节点，
        /// 而是应该调用 FlowEnvironment 提供接口进行操作，
        /// 因为 Workbench 应该更加关注UI视觉效果，而非直接干扰流程环境运行的逻辑。
        /// 之所以暴露 NodeModel 属性，因为有些场景下不可避免的需要直接获取节点的属性。
        /// </summary>
        private Dictionary<string, NodeControlBase> NodeControls { get; } = [];

        /// <summary>
        /// 存储所有的连接。考虑集成在运行环境中。
        /// </summary>
        private List<ConnectionControl> Connections { get; } = [];

        /// <summary>
        /// 起始节点
        /// </summary>
        //private NodeControlBase StartNodeControl{ get; set; }

        #region 与画布相关的字段

        /// <summary>
        /// 标记是否正在尝试选取控件
        /// </summary>
        private bool IsSelectControl;
        /// <summary>
        /// 标记是否正在进行连接操作
        /// </summary>
        //private bool IsConnecting;
        /// <summary>
        /// 标记是否正在拖动控件
        /// </summary>
        private bool IsControlDragging;
        /// <summary>
        /// 标记是否正在拖动画布
        /// </summary>
        private bool IsCanvasDragging;
        private bool IsSelectDragging;

        /// <summary>
        /// 当前选取的控件
        /// </summary>
        private readonly List<NodeControlBase> selectNodeControls  = [];

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
        /// 记录开始连接的文本块
        /// </summary>
        //private NodeControlBase? startConnectNodeControl;
        /// <summary>
        /// 当前正在绘制的连接线
        /// </summary>
        //private Line? currentLine;
        /// <summary>
        /// 当前正在绘制的真假分支属性
        /// </summary>
        //private ConnectionInvokeType currentConnectionType;


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


        public MainWindow()
        {
            ViewModel = new MainWindowViewModel(this);
            this.DataContext = ViewModel;
            InitializeComponent();

            ViewObjectViewer.FlowEnvironment = EnvDecorator; // 设置 节点树视图 的环境为装饰器
            IOCObjectViewer.FlowEnvironment = EnvDecorator;  // 设置 IOC容器视图 的环境为装饰器
            IOCObjectViewer.SelectObj += ViewObjectViewer.LoadObjectInformation; // 使选择 IOC容器视图 的某项（对象）时，可以在 数据视图 呈现数据

            #region 为  NodeControlType 枚举 不同项添加对应的 Control类型 、 ViewModel类型
            NodeMVVMManagement.RegisterUI(NodeControlType.Action, typeof(ActionNodeControl), typeof(ActionNodeControlViewModel));
            NodeMVVMManagement.RegisterUI(NodeControlType.Flipflop, typeof(FlipflopNodeControl), typeof(FlipflopNodeControlViewModel));
            NodeMVVMManagement.RegisterUI(NodeControlType.ExpOp, typeof(ExpOpNodeControl), typeof(ExpOpNodeControlViewModel));
            NodeMVVMManagement.RegisterUI(NodeControlType.ExpCondition, typeof(ConditionNodeControl), typeof(ConditionNodeControlViewModel));
            NodeMVVMManagement.RegisterUI(NodeControlType.ConditionRegion, typeof(ConditionRegionControl), typeof(ConditionRegionNodeControlViewModel));
            NodeMVVMManagement.RegisterUI(NodeControlType.GlobalData, typeof(GlobalDataControl), typeof(GlobalDataNodeControlViewModel));
            #endregion


            #region 缩放平移容器
            canvasTransformGroup = new TransformGroup();
            scaleTransform = new ScaleTransform();
            translateTransform = new TranslateTransform();
            canvasTransformGroup.Children.Add(scaleTransform);
            canvasTransformGroup.Children.Add(translateTransform);
            FlowChartCanvas.RenderTransform = canvasTransformGroup;
            #endregion

            InitFlowEnvironmentEvent(); // 配置环境事件

            
        }



        /// <summary>
        /// 初始化环境事件
        /// </summary>
        private void InitFlowEnvironmentEvent()
        {
            EnvDecorator.OnDllLoad += FlowEnvironment_DllLoadEvent;
            EnvDecorator.OnProjectSaving += EnvDecorator_OnProjectSaving;
            EnvDecorator.OnProjectLoaded += FlowEnvironment_OnProjectLoaded;
            EnvDecorator.OnStartNodeChange += FlowEnvironment_StartNodeChangeEvent;
            EnvDecorator.OnNodeConnectChange += FlowEnvironment_NodeConnectChangeEvemt;
            EnvDecorator.OnNodeCreate += FlowEnvironment_NodeCreateEvent;
            EnvDecorator.OnNodeRemove += FlowEnvironment_NodeRemoteEvent;
            EnvDecorator.OnFlowRunComplete += FlowEnvironment_OnFlowRunComplete;


            EnvDecorator.OnMonitorObjectChange += FlowEnvironment_OnMonitorObjectChange;
            EnvDecorator.OnNodeInterruptStateChange += FlowEnvironment_OnNodeInterruptStateChange;
            EnvDecorator.OnInterruptTrigger += FlowEnvironment_OnInterruptTrigger;

            EnvDecorator.OnIOCMembersChanged += FlowEnvironment_OnIOCMembersChanged;
                   
            EnvDecorator.OnNodeLocated += FlowEnvironment_OnNodeLocate;
            EnvDecorator.OnNodeMoved += FlowEnvironment_OnNodeMoved;
            EnvDecorator.OnEnvOut += FlowEnvironment_OnEnvOut;
        }

        /// <summary>
        /// 移除环境事件
        /// </summary>
        private void ResetFlowEnvironmentEvent()
        {
            EnvDecorator.OnDllLoad -= FlowEnvironment_DllLoadEvent;
            EnvDecorator.OnProjectSaving -= EnvDecorator_OnProjectSaving;
            EnvDecorator.OnProjectLoaded -= FlowEnvironment_OnProjectLoaded;
            EnvDecorator.OnStartNodeChange -= FlowEnvironment_StartNodeChangeEvent;
            EnvDecorator.OnNodeConnectChange -= FlowEnvironment_NodeConnectChangeEvemt;
            EnvDecorator.OnNodeCreate -= FlowEnvironment_NodeCreateEvent;
            EnvDecorator.OnNodeRemove -= FlowEnvironment_NodeRemoteEvent;
            EnvDecorator.OnFlowRunComplete -= FlowEnvironment_OnFlowRunComplete;


            EnvDecorator.OnMonitorObjectChange -= FlowEnvironment_OnMonitorObjectChange;
            EnvDecorator.OnNodeInterruptStateChange -= FlowEnvironment_OnNodeInterruptStateChange;
            EnvDecorator.OnInterruptTrigger -= FlowEnvironment_OnInterruptTrigger;

            EnvDecorator.OnIOCMembersChanged -= FlowEnvironment_OnIOCMembersChanged;
            EnvDecorator.OnNodeLocated -= FlowEnvironment_OnNodeLocate;
            EnvDecorator.OnNodeMoved -= FlowEnvironment_OnNodeMoved;

            EnvDecorator.OnEnvOut -= FlowEnvironment_OnEnvOut;

        }

        #region 窗体加载方法
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (App.FlowProjectData is not null)
            {
                _ = Task.Run(() =>
                {
                    EnvDecorator.LoadProject(new FlowEnvInfo { Project = App.FlowProjectData }, App.FileDataPath); // 加载项目
                });
            }
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            LogOutWindow.Close();
            System.Windows.Application.Current.Shutdown();
        }
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            SereinEnv.WriteLine(InfoType.INFO, "load project...");
            var project = App.FlowProjectData;
            if (project is null)
            {
                return;
            }
            InitializeCanvas(project.Basic.Canvas.Width, project.Basic.Canvas.Height);// 设置画布大小
            //foreach (var connection in Connections)
            //{
            //    connection.RefreshLine(); // 窗体完成加载后试图刷新所有连接线
            //}
            SereinEnv.WriteLine(InfoType.INFO, $"运行环境当前工作目录：{System.IO.Directory.GetCurrentDirectory()}");
            
            var canvasData = project.Basic.Canvas;
            if (canvasData is not null)
            {
                scaleTransform.ScaleX = 1;
                scaleTransform.ScaleY = 1;
                translateTransform.X = 0;
                translateTransform.Y = 0;
                scaleTransform.ScaleX = canvasData.ScaleX;
                scaleTransform.ScaleY = canvasData.ScaleY;
                translateTransform.X += canvasData.ViewX;
                translateTransform.Y += canvasData.ViewY;
                // 应用变换组
                FlowChartCanvas.RenderTransform = canvasTransformGroup;
            }


        }




        #endregion

        #region 运行环境事件

        /// <summary>
        /// 环境内容输出
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        private void FlowEnvironment_OnEnvOut(InfoType type, string value)
        {
            LogOutWindow.AppendText($"{DateTime.UtcNow} [{type}] : {value}{Environment.NewLine}");
        }


        /// <summary>
        /// 需要保存项目
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void EnvDecorator_OnProjectSaving(ProjectSavingEventArgs eventArgs)
        {
            var projectData = EnvDecorator.GetProjectInfoAsync()
                               .GetAwaiter().GetResult(); // 保存项目

            projectData.Basic = new Basic
            {
                Canvas = new FlowCanvas
                {
                    Height = FlowChartCanvas.Height,
                    Width = FlowChartCanvas.Width,
                    ViewX = translateTransform.X,
                    ViewY = translateTransform.Y,
                    ScaleX = scaleTransform.ScaleX,
                    ScaleY = scaleTransform.ScaleY,
                },
                Versions = "1",
            };

            // 创建一个新的保存文件对话框
            SaveFileDialog saveFileDialog = new()
            {
                Filter = "DynamicNodeFlow Files (*.dnf)|*.dnf",
                DefaultExt = "dnf",
                FileName = "project.dnf"
                // FileName = System.IO.Path.GetFileName(App.FileDataPath)
            };

            // 显示保存文件对话框
            bool? result = saveFileDialog.ShowDialog();
            // 如果用户选择了文件并点击了保存按钮
            if (result == false)
            {
                SereinEnv.WriteLine(InfoType.ERROR, "取消保存文件");
                return;
            }

            var savePath = saveFileDialog.FileName;
            string? librarySavePath = System.IO.Path.GetDirectoryName(savePath);
            if (string.IsNullOrEmpty(librarySavePath))
            {
                SereinEnv.WriteLine(InfoType.ERROR, "保存项目DLL时返回了意外的文件保存路径");
                return;
            }


            Uri saveProjectFileUri = new Uri(savePath);
            SereinEnv.WriteLine(InfoType.INFO, "项目文件保存路径：" + savePath);
            for (int index = 0; index < projectData.Librarys.Length; index++)
            {
                NodeLibraryInfo? library = projectData.Librarys[index];
                string sourceFile = new Uri(library.FilePath).LocalPath; // 源文件夹
                string targetPath = System.IO.Path.Combine(librarySavePath, library.FileName); // 目标文件夹
                SereinEnv.WriteLine(InfoType.INFO, $"源路径   : {sourceFile}");
                SereinEnv.WriteLine(InfoType.INFO, $"目标路径 : {targetPath}");

                try
                {
                    File.Copy(sourceFile, targetPath, true);
                }
                catch (IOException ex)
                {
                    
                    SereinEnv.WriteLine(InfoType.ERROR, ex.Message);
                }
                var dirName = System.IO.Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dirName))
                {
                    var tmpUri2 = new Uri(targetPath);
                    var relativePath = saveProjectFileUri.MakeRelativeUri(tmpUri2).ToString(); // 转为类库的相对文件路径

                    


                    //string relativePath = System.IO.Path.GetRelativePath(savePath, targetPath);
                    projectData.Librarys[index].FilePath = relativePath;
                }
                
            }

            JObject projectJsonData = JObject.FromObject(projectData);
            File.WriteAllText(savePath, projectJsonData.ToString());


        }


        /// <summary>
        /// 加载完成
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FlowEnvironment_OnProjectLoaded(ProjectLoadedEventArgs eventArgs)
        {
        }

        /// <summary>
        /// 运行完成
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEnvironment_OnFlowRunComplete(FlowEventArgs eventArgs)
        {
            SereinEnv.WriteLine(InfoType.INFO, "-------运行完成---------\r\n");
            this.Dispatcher.Invoke(() =>
            {
                IOCObjectViewer.ClearObjItem();
            });
        }

        /// <summary>
        /// 加载了DLL文件，dll内容
        /// </summary>
        private void FlowEnvironment_DllLoadEvent(LoadDllEventArgs eventArgs)
        {
            NodeLibraryInfo nodeLibraryInfo = eventArgs.NodeLibraryInfo;
            List<MethodDetailsInfo> methodDetailss = eventArgs.MethodDetailss;

            var dllControl = new DllControl(nodeLibraryInfo);

            foreach (var methodDetailsInfo in methodDetailss)
            {
                if (!EnumHelper.TryConvertEnum<Library.NodeType>(methodDetailsInfo.NodeType, out var nodeType))
                {
                    continue;
                }
                switch (nodeType)
                {
                    case Library.NodeType.Action:
                        dllControl.AddAction(methodDetailsInfo);  // 添加动作类型到控件
                        break;
                    case Library.NodeType.Flipflop:
                        dllControl.AddFlipflop(methodDetailsInfo);  // 添加触发器方法到控件
                        break;
                }

            }
            var menu = new ContextMenu();
            menu.Items.Add(CreateMenuItem("卸载", (s, e) =>
            {
                if (this.EnvDecorator.UnloadLibrary(nodeLibraryInfo.AssemblyName))
                {
                    DllStackPanel.Children.Remove(dllControl);
                }
                else
                {
                    SereinEnv.WriteLine(InfoType.INFO, "卸载失败");
                }
            }));

            dllControl.ContextMenu = menu;

            DllStackPanel.Children.Add(dllControl);  // 将控件添加到界面上显示

        }

        /// <summary>
        /// 节点连接关系变更
        /// </summary>
        /// <param name="eventArgs"></param>
        private  void FlowEnvironment_NodeConnectChangeEvemt(NodeConnectChangeEventArgs eventArgs)
        {
            string fromNodeGuid = eventArgs.FromNodeGuid;
            string toNodeGuid = eventArgs.ToNodeGuid;
            if (!TryGetControl(fromNodeGuid, out var fromNodeControl) 
               || !TryGetControl(toNodeGuid, out var toNodeControl)) 
            {
                return;
            }
            
            


            if (eventArgs.JunctionOfConnectionType == JunctionOfConnectionType.Invoke)
            {
                ConnectionInvokeType connectionType = eventArgs.ConnectionInvokeType;
                #region 创建/删除节点之间的调用关系
                #region 创建连接
                if (eventArgs.ChangeType == NodeConnectChangeEventArgs.ConnectChangeType.Create) // 添加连接
                {
                    if (fromNodeControl is not INodeJunction IFormJunction || toNodeControl is not INodeJunction IToJunction)
                    {
                        SereinEnv.WriteLine(InfoType.INFO, "非预期的连接");
                        return;
                    }
                    JunctionControlBase startJunction = IFormJunction.NextStepJunction;
                    JunctionControlBase endJunction = IToJunction.ExecuteJunction;


                    // 添加连接
                    var connection = new ConnectionControl(
                        FlowChartCanvas,
                        connectionType,
                        startJunction,
                        endJunction
                    );

                    //() => EnvDecorator.RemoveConnectInvokeAsync(fromNodeGuid, toNodeGuid, connectionType)

                    if (toNodeControl is FlipflopNodeControl flipflopControl
                        && flipflopControl?.ViewModel?.NodeModel is NodeModelBase nodeModel) // 某个节点连接到了触发器，尝试从全局触发器视图中移除该触发器
                    {
                        NodeTreeViewer.RemoteGlobalFlipFlop(nodeModel); // 从全局触发器树树视图中移除
                    }
                    //connection.RefreshLine();  // 添加贝塞尔曲线显示
                    Connections.Add(connection);
                    fromNodeControl.AddCnnection(connection);
                    toNodeControl.AddCnnection(connection);
                    EndConnection(); // 环境触发了创建节点连接事件


                }
                #endregion
                #region 移除连接
                else if (eventArgs.ChangeType == NodeConnectChangeEventArgs.ConnectChangeType.Remote) // 移除连接
                {
                    // 需要移除连接
                    var removeConnections = Connections.Where(c => 
                                               c.Start.MyNode.Guid.Equals(fromNodeGuid)
                                            && c.End.MyNode.Guid.Equals(toNodeGuid)
                                            && (c.Start.JunctionType.ToConnectyionType() == JunctionOfConnectionType.Invoke
                                            || c.End.JunctionType.ToConnectyionType() == JunctionOfConnectionType.Invoke))
                                            .ToList();


                    foreach (var connection in removeConnections)
                    {
                        Connections.Remove(connection);
                        fromNodeControl.RemoveConnection(connection); // 移除连接
                        toNodeControl.RemoveConnection(connection); // 移除连接
                        if (NodeControls.TryGetValue(connection.End.MyNode.Guid, out var control))
                        {
                            JudgmentFlipFlopNode(control); // 连接关系变更时判断
                        }
                    }
                } 
                #endregion
                #endregion
            }
            else
            {
                ConnectionArgSourceType connectionArgSourceType = eventArgs.ConnectionArgSourceType;
                #region 创建/删除节点之间的参数传递关系
                #region 创建连接
                if (eventArgs.ChangeType == NodeConnectChangeEventArgs.ConnectChangeType.Create) // 添加连接
                {
                    if (fromNodeControl is not INodeJunction IFormJunction || toNodeControl is not INodeJunction IToJunction)
                    {
                        SereinEnv.WriteLine(InfoType.INFO, "非预期的情况");
                        return;
                    }

                    JunctionControlBase startJunction = eventArgs.ConnectionArgSourceType switch
                    {
                        ConnectionArgSourceType.GetPreviousNodeData => IFormJunction.ReturnDataJunction, // 自身节点
                        ConnectionArgSourceType.GetOtherNodeData => IFormJunction.ReturnDataJunction, // 其它节点的返回值控制点
                        ConnectionArgSourceType.GetOtherNodeDataOfInvoke => IFormJunction.ReturnDataJunction, // 其它节点的返回值控制点
                        _ => throw new Exception("窗体事件 FlowEnvironment_NodeConnectChangeEvemt 创建/删除节点之间的参数传递关系 JunctionControlBase 枚举值错误 。非预期的枚举值。") // 应该不会触发
                    };

                    if(IToJunction.ArgDataJunction.Length == 0)
                    {

                    }
                    JunctionControlBase endJunction = IToJunction.ArgDataJunction[eventArgs.ArgIndex];
                    LineType lineType = LineType.Bezier;
                    // 添加连接
                    var connection = new ConnectionControl(
                        lineType,
                        FlowChartCanvas,
                        eventArgs.ArgIndex,
                        eventArgs.ConnectionArgSourceType,
                        startJunction,
                        endJunction,
                        IToJunction
                    );
                    Connections.Add(connection);
                    fromNodeControl.AddCnnection(connection);
                    toNodeControl.AddCnnection(connection);
                    EndConnection(); // 环境触发了创建节点连接事件


                }
                #endregion
                #region 移除连接
                else if (eventArgs.ChangeType == NodeConnectChangeEventArgs.ConnectChangeType.Remote) // 移除连接
                {
                    // 需要移除连接
                    var removeConnections = Connections.Where(c => c.Start.MyNode.Guid.Equals(fromNodeGuid)
                                                                    && c.End.MyNode.Guid.Equals(toNodeGuid))
                                                                .ToList(); // 获取这两个节点之间的所有连接关系



                    foreach (var connection in removeConnections)
                    {
                        if (connection.End is ArgJunctionControl junctionControl && junctionControl.ArgIndex == eventArgs.ArgIndex)
                        {
                            // 找到符合删除条件的连接线
                            Connections.Remove(connection); // 从本地记录中移除
                            fromNodeControl.RemoveConnection(connection); // 从节点持有的记录移除
                            toNodeControl.RemoveConnection(connection); // 从节点持有的记录移除
                        }


                        //if (NodeControls.TryGetValue(connection.End.MyNode.Guid, out var control))
                        //{
                        //    JudgmentFlipFlopNode(control); // 连接关系变更时判断
                        //}
                    }
                } 
                #endregion
                #endregion
            }
        }

        /// <summary>
        /// 节点移除事件
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FlowEnvironment_NodeRemoteEvent(NodeRemoveEventArgs eventArgs)
        {
            var nodeGuid = eventArgs.NodeGuid;
            if (!TryGetControl(nodeGuid, out var nodeControl))
            {
                return;
            }
           
            if (nodeControl is null) return;
            if (selectNodeControls.Count > 0)
            {
                if (selectNodeControls.Contains(nodeControl))
                {
                    selectNodeControls.Remove(nodeControl);
                }
            }

            if (nodeControl is FlipflopNodeControl flipflopControl) // 判断是否为触发器
            {
                var node = flipflopControl?.ViewModel?.NodeModel;
                if (node is not null)
                {
                    NodeTreeViewer.RemoteGlobalFlipFlop(node); // 从全局触发器树树视图中移除
                }
            }



            FlowChartCanvas.Children.Remove(nodeControl);
            nodeControl.RemoveAllConection();
            NodeControls.Remove(nodeControl.ViewModel.NodeModel.Guid);
        }

        /// <summary>
        /// 添加节点事件
        /// </summary>
        /// <param name="eventArgs">添加节点事件参数</param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEnvironment_NodeCreateEvent(NodeCreateEventArgs eventArgs)
        {
            if (eventArgs.NodeModel is not NodeModelBase nodeModelBase)
            {
                return;
            }

            if(nodeModelBase is null)
            {
                SereinEnv.WriteLine(InfoType.WARN, "OnNodeCreateEvent事件接收到意外的返回值");
                return;
            }
            // MethodDetails methodDetailss = eventArgs.MethodDetailss;
            PositionOfUI position = eventArgs.Position;

            if(!NodeMVVMManagement.TryGetType(nodeModelBase.ControlType, out var nodeMVVM))
            {
                SereinEnv.WriteLine(InfoType.INFO, $"无法创建{nodeModelBase.ControlType}节点，节点类型尚未注册。");
                return;
            }
            if(nodeMVVM.ControlType == null
                || nodeMVVM.ViewModelType == null)
            {
                SereinEnv.WriteLine(InfoType.INFO, $"无法创建{nodeModelBase.ControlType}节点，UI类型尚未注册（请通过 NodeMVVMManagement.RegisterUI() 方法进行注册）。");
                return;
            }

            NodeControlBase nodeControl = CreateNodeControl(nodeMVVM.ControlType, nodeMVVM.ViewModelType, nodeModelBase);

            if (nodeControl is null)
            {
                return;
            }
            NodeControls.TryAdd(nodeModelBase.Guid, nodeControl);
            if (eventArgs.IsAddInRegion && NodeControls.TryGetValue(eventArgs.RegeionGuid, out NodeControlBase? regionControl))
            {
                // 这里的条件是用于加载项目文件时，直接加载在区域中，而不用再判断控件
                if (regionControl is not null)
                {
                    TryPlaceNodeInRegion(regionControl, nodeControl);
                }
                return;
            }
            else
            {
                // 这里是正常的编辑流程
                // 判断是否为区域
                if (TryPlaceNodeInRegion(nodeControl, position, out var targetNodeControl))
                {
                    // 需要将节点放置在区域中
                    TryPlaceNodeInRegion(targetNodeControl, nodeControl);
                }
                else
                {
                    // 并非区域，需要手动添加
                    PlaceNodeOnCanvas(nodeControl, position.X, position.Y); // 将节点放置在画布上
                }
            }


            #region 节点树视图
            if (nodeModelBase.ControlType == NodeControlType.Flipflop)
            {
                var node = nodeControl?.ViewModel?.NodeModel;
                if (node is not null)
                {
                    NodeTreeViewer.AddGlobalFlipFlop(EnvDecorator, node); // 新增的触发器节点添加到全局触发器
                }
            }

            GC.Collect();
            #endregion

        }

        /// <summary>
        /// 设置了流程起始控件
        /// </summary>
        /// <param name="oldNodeGuid"></param>
        /// <param name="newNodeGuid"></param>
        private void FlowEnvironment_StartNodeChangeEvent(StartNodeChangeEventArgs eventArgs)
        {
            string oldNodeGuid = eventArgs.OldNodeGuid;
            string newNodeGuid = eventArgs.NewNodeGuid;
            if (!TryGetControl(newNodeGuid, out var newStartNodeControl)) return;
            if (!string.IsNullOrEmpty(oldNodeGuid))
            {
                if (!TryGetControl(oldNodeGuid, out var oldStartNodeControl)) return;
                oldStartNodeControl.BorderBrush = Brushes.Black;
                oldStartNodeControl.BorderThickness = new Thickness(0);
            }

            newStartNodeControl.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#04FC10"));
            newStartNodeControl.BorderThickness = new Thickness(2);
            var node = newStartNodeControl?.ViewModel?.NodeModel;
            if (node is not null)
            {
                NodeTreeViewer.LoadNodeTreeOfStartNode(EnvDecorator, node);
            }

        }

        /// <summary>
        /// 被监视的对象发生改变
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FlowEnvironment_OnMonitorObjectChange(MonitorObjectEventArgs eventArgs)
        {
            string nodeGuid = eventArgs.NodeGuid;

            string monitorKey =  MonitorObjectEventArgs.ObjSourceType.NodeFlowData switch
            {
                MonitorObjectEventArgs.ObjSourceType.NodeFlowData => nodeGuid,
                _ => eventArgs.NewData.GetType().FullName,
            };

            //NodeControlBase nodeControl = GuidToControl(nodeGuid);
            if (ViewObjectViewer.MonitorObj is null) // 如果没有加载过对象
            {
                ViewObjectViewer.LoadObjectInformation(monitorKey, eventArgs.NewData); // 加载对象 ViewObjectViewerControl.MonitorType.Obj
            }
            else
            {
                if (monitorKey.Equals(ViewObjectViewer.MonitorKey)) // 相同对象
                {
                    ViewObjectViewer.RefreshObjectTree(eventArgs.NewData); // 刷新
                }
                else
                {
                    ViewObjectViewer.LoadObjectInformation(monitorKey, eventArgs.NewData); // 加载对象
                }
            }

        }

        /// <summary>
        /// 节点中断状态改变。
        /// </summary>
        /// <param name="eventArgs"></param>
        private  void FlowEnvironment_OnNodeInterruptStateChange(NodeInterruptStateChangeEventArgs eventArgs)
        {
            string nodeGuid = eventArgs.NodeGuid;
            if (!TryGetControl(nodeGuid, out var nodeControl)) return;

            //if (eventArgs.Class == InterruptClass.None)
            //{
            //    nodeControl.ViewModel.IsInterrupt = false;
            //}
            //else
            //{
            //    nodeControl.ViewModel.IsInterrupt = true;
            //}
            if(nodeControl.ContextMenu == null)
            {
                return;
            }
            foreach (var menuItem in nodeControl.ContextMenu.Items)
            {
                if (menuItem is MenuItem menu)
                {
                    if ("取消中断".Equals(menu.Header))
                    {
                        menu.Header = "在此中断";
                    }
                    else if ("在此中断".Equals(menu.Header))
                    {
                        menu.Header = "取消中断";
                    }

                }
            }

        }

        /// <summary>
        /// 节点触发了中断
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEnvironment_OnInterruptTrigger(InterruptTriggerEventArgs eventArgs)
        {
            string nodeGuid = eventArgs.NodeGuid;
            if (!TryGetControl(nodeGuid, out var nodeControl)) return;
            if(eventArgs.Type == InterruptTriggerEventArgs.InterruptTriggerType.Exp)
            {
                SereinEnv.WriteLine(InfoType.INFO, $"表达式触发了中断:{eventArgs.Expression}");
            }
            else
            {
                SereinEnv.WriteLine(InfoType.INFO, $"节点触发了中断:{nodeGuid}");
            }
        }

        /// <summary>
        /// IOC变更
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEnvironment_OnIOCMembersChanged(IOCMembersChangedEventArgs eventArgs)
        {
            IOCObjectViewer.AddDependenciesInstance(eventArgs.Key, eventArgs.Instance);

        }

        /// <summary>
        /// 节点需要定位
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEnvironment_OnNodeLocate(NodeLocatedEventArgs eventArgs)
        {
            if (!TryGetControl(eventArgs.NodeGuid, out var nodeControl)) return;
            //scaleTransform.ScaleX = 1;
            //scaleTransform.ScaleY = 1;
            // 获取控件在 FlowChartCanvas 上的相对位置
            Rect controlBounds = VisualTreeHelper.GetDescendantBounds(nodeControl);
            Point controlPosition = nodeControl.TransformToAncestor(FlowChartCanvas).Transform(new Point(0, 0));

            // 获取控件在画布上的中心点
            double controlCenterX = controlPosition.X + controlBounds.Width / 2;
            double controlCenterY = controlPosition.Y + controlBounds.Height / 2;

            // 考虑缩放因素计算目标位置的中心点
            double scaledCenterX = controlCenterX * scaleTransform.ScaleX;
            double scaledCenterY = controlCenterY * scaleTransform.ScaleY;


            //// 计算画布的可视区域大小
            //double visibleAreaLeft = scaledCenterX;
            //double visibleAreaTop = scaledCenterY;
            //double visibleAreaRight = scaledCenterX + FlowChartStackGrid.ActualWidth;
            //double visibleAreaBottom = scaledCenterY + FlowChartStackGrid.ActualHeight;
            //// 检查控件中心点是否在可视区域内
            //bool isInView = scaledCenterX >= visibleAreaLeft && scaledCenterX <= visibleAreaRight &&
            //                scaledCenterY >= visibleAreaTop && scaledCenterY <= visibleAreaBottom;

            //Console.WriteLine($"isInView :{isInView}");

            //if (!isInView)
            //{
            //} 
            // 计算平移偏移量，使得控件在可视区域的中心
            double translateX = scaledCenterX - FlowChartStackGrid.ActualWidth / 2;
            double translateY = scaledCenterY - FlowChartStackGrid.ActualHeight / 2;

            var translate = this.translateTransform;
            // 应用平移变换
            translate.X = 0;
            translate.Y = 0;
            translate.X -= translateX;
            translate.Y -= translateY;

            // 设置RenderTransform以实现移动效果
            TranslateTransform translateTransform = new TranslateTransform();
            nodeControl.RenderTransform = translateTransform;
            ElasticAnimation(nodeControl, translateTransform, 4, 1, 0.5);

        }

        /// <summary>
        /// 控件抖动
        /// 来源：https://www.cnblogs.com/RedSky/p/17705411.html
        /// 作者：HotSky
        /// （……太好用了）
        /// </summary>
        /// <param name="translate"></param>
        /// <param name="nodeControl">需要抖动的控件</param>
        /// <param name="power">抖动第一下偏移量</param>
        /// <param name="range">减弱幅度（小于等于power，大于0）</param>
        /// <param name="speed">持续系数(大于0)，越大时间越长，</param>
        private static void ElasticAnimation(NodeControlBase nodeControl, TranslateTransform translate, int power, int range = 1, double speed = 1)
        {
            DoubleAnimationUsingKeyFrames animation1 = new DoubleAnimationUsingKeyFrames();
            for (int i = power, j = 1; i >= 0; i -= range)
            {
                animation1.KeyFrames.Add(new LinearDoubleKeyFrame(-i, TimeSpan.FromMilliseconds(j++ * 100 * speed)));
                animation1.KeyFrames.Add(new LinearDoubleKeyFrame(i, TimeSpan.FromMilliseconds(j++ * 100 * speed)));
            }
            translate.BeginAnimation(TranslateTransform.YProperty, animation1);
            DoubleAnimationUsingKeyFrames animation2 = new DoubleAnimationUsingKeyFrames();
            for (int i = power, j = 1; i >= 0; i -= range)
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
        /// 节点移动
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FlowEnvironment_OnNodeMoved(NodeMovedEventArgs eventArgs)
        {
            if (!TryGetControl(eventArgs.NodeGuid, out var nodeControl)) return;
            nodeControl.UpdateLocationConnections();

            //var newLeft = eventArgs.X;
            //var newTop = eventArgs.Y;
            //// 限制控件不超出FlowChartCanvas的边界
            //if (newLeft >= 0 && newLeft + nodeControl.ActualWidth <= FlowChartCanvas.ActualWidth)
            //{
            //    Canvas.SetLeft(nodeControl, newLeft);

            //}
            //if (newTop >= 0 && newTop + nodeControl.ActualHeight <= FlowChartCanvas.ActualHeight)
            //{
            //    Canvas.SetTop(nodeControl, newTop);
            //}


        }

        /// <summary>
        /// Guid 转 NodeControl
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="nodeControl"></param>
        /// <returns></returns>
        private bool TryGetControl(string nodeGuid,out NodeControlBase nodeControl)
        {
            if (string.IsNullOrEmpty(nodeGuid))
            {
                nodeControl = null;
                return false;
            }
            if (!NodeControls.TryGetValue(nodeGuid, out nodeControl))
            {
                nodeControl = null;
                return false;
            }
            if(nodeControl is null)
            {
                return false;
            }
            return true;
        }

        #endregion

        #region 加载项目文件后触发事件相关方法

        /// <summary>
        /// 运行环节加载了项目文件，需要创建节点控件
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <param name="methodDetailss"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        //private NodeControlBase? CreateNodeControlOfNodeInfo(NodeInfo nodeInfo, MethodDetails methodDetailss)
        //{
        //    // 创建控件实例
        //    NodeControlBase nodeControl = nodeInfo.Type switch
        //    {
        //        $"{NodeStaticConfig.NodeSpaceName}.{nameof(SingleActionNode)}" =>
        //            CreateNodeControl<SingleActionNode, ActionNodeControl, ActionNodeControlViewModel>(methodDetailss),// 动作节点控件
        //        $"{NodeStaticConfig.NodeSpaceName}.{nameof(SingleFlipflopNode)}" =>
        //            CreateNodeControl<SingleFlipflopNode, FlipflopNodeControl, FlipflopNodeControlViewModel>(methodDetailss), // 触发器节点控件

        //        $"{NodeStaticConfig.NodeSpaceName}.{nameof(SingleConditionNode)}" =>
        //            CreateNodeControl<SingleConditionNode, ConditionNodeControl, ConditionNodeControlViewModel>(), // 条件表达式控件
        //        $"{NodeStaticConfig.NodeSpaceName}.{nameof(SingleExpOpNode)}" =>
        //            CreateNodeControl<SingleExpOpNode, ExpOpNodeControl, ExpOpNodeViewModel>(), // 操作表达式控件

        //        $"{NodeStaticConfig.NodeSpaceName}.{nameof(CompositeConditionNode)}" =>
        //            CreateNodeControl<CompositeConditionNode, ConditionRegionControl, ConditionRegionNodeControlViewModel>(), // 条件区域控件
        //        _ => throw new NotImplementedException($"非预期的节点类型{nodeInfo.Type}"),
        //    };
        //    return nodeControl;
        //}

        /// <summary>
        /// 加载文件时，添加节点到区域中
        /// </summary>
        /// <param name="regionControl"></param>
        /// <param name="childNodes"></param>
        //private void AddNodeControlInRegeionControl(NodeControlBase regionControl, NodeInfo[] childNodes)
        //{
        //    foreach (var childNode in childNodes)
        //    {
        //        if (FlowEnvironment.TryGetMethodDetails(childNode.MethodName, out MethodDetails md))
        //        {
        //            var childNodeControl = CreateNodeControlOfNodeInfo(childNode, md);
        //            if (childNodeControl is null)
        //            {
        //                Console.WriteLine($"无法为节点类型创建节点控件: {childNode.MethodName}\r\n");
        //                continue;
        //            }

        //            if (regionControl is ConditionRegionControl conditionRegion)
        //            {
        //                conditionRegion.AddCondition(childNodeControl);
        //            }
        //        }
        //    }
        //}

        #endregion

        #region 节点控件的创建


        /// <summary>
        /// 创建了节点，添加到画布。配置默认事件
        /// </summary>
        /// <param name="nodeControl"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        private void PlaceNodeOnCanvas(NodeControlBase nodeControl, double x, double y)
        {
            // 添加控件到画布
            FlowChartCanvas.Children.Add(nodeControl);
            Canvas.SetLeft(nodeControl, x);
            Canvas.SetTop(nodeControl, y);

            ConfigureContextMenu(nodeControl); // 配置节点右键菜单
            ConfigureNodeEvents(nodeControl); // 配置节点事件
        }

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

       
        #endregion

        #region 配置右键菜单

        /// <summary>
        /// 配置节点右键菜单
        /// </summary>
        /// <param name="nodeControl"><para> 任何情景下都尽量避免直接操作 ViewModel 中的 NodeModel 节点，而是应该调用 FlowEnvironment 提供接口进行操作。</para> 因为 Workbench 应该更加关注UI视觉效果，而非直接干扰流程环境运行的逻辑。<para> 之所以暴露 NodeModel 属性，因为有些场景下不可避免的需要直接获取节点的属性。</para> </param>
        private void ConfigureContextMenu(NodeControlBase nodeControl)
        {
            
            var contextMenu = new ContextMenu();
            var nodeGuid = nodeControl.ViewModel?.NodeModel?.Guid;
            #region 触发器节点
            
            if(nodeControl.ViewModel?.NodeModel.ControlType == NodeControlType.Flipflop)
            {
                contextMenu.Items.Add(CreateMenuItem("启动触发器", (s, e) =>
                {
                    if (s is MenuItem menuItem)
                    {
                        if (menuItem.Header.ToString() == "启动触发器")
                        {
                            EnvDecorator.ActivateFlipflopNode(nodeGuid);

                            menuItem.Header = "终结触发器";
                        }
                        else
                        {
                            EnvDecorator.TerminateFlipflopNode(nodeGuid);
                            menuItem.Header = "启动触发器";

                        }
                    }
                }));
            }
                
            #endregion

            if (nodeControl.ViewModel?.NodeModel?.MethodDetails?.ReturnType is Type returnType && returnType != typeof(void))
            {
                contextMenu.Items.Add(CreateMenuItem("查看返回类型", (s, e) =>
                {
                    DisplayReturnTypeTreeViewer(returnType);
                }));
            }

            #region 右键菜单功能 - 中断

            contextMenu.Items.Add(CreateMenuItem("在此中断", async (s, e) =>
            {
                if ((s is MenuItem menuItem) && menuItem is not null)
                {
                    if (nodeControl?.ViewModel?.NodeModel?.DebugSetting?.IsInterrupt == true)
                    {
                        await EnvDecorator.SetNodeInterruptAsync(nodeGuid,false);
                        nodeControl.ViewModel.IsInterrupt = false;

                        menuItem.Header = "取消中断";
                    }
                    else
                    {
                        nodeControl!.ViewModel!.IsInterrupt = true;
                        await EnvDecorator.SetNodeInterruptAsync(nodeGuid, true);
                        menuItem.Header = "在此中断";

                    }
                }
            }));

            #endregion

           
            contextMenu.Items.Add(CreateMenuItem("设为起点", (s, e) => EnvDecorator.SetStartNode(nodeGuid)));
            contextMenu.Items.Add(CreateMenuItem("删除", (s, e) => EnvDecorator.RemoveNodeAsync(nodeGuid)));

            //contextMenu.Items.Add(CreateMenuItem("添加 真分支", (s, e) => StartConnection(nodeControl, ConnectionInvokeType.IsSucceed)));
            //contextMenu.Items.Add(CreateMenuItem("添加 假分支", (s, e) => StartConnection(nodeControl, ConnectionInvokeType.IsFail)));
            //contextMenu.Items.Add(CreateMenuItem("添加 异常分支", (s, e) => StartConnection(nodeControl, ConnectionInvokeType.IsError)));
            //contextMenu.Items.Add(CreateMenuItem("添加 上游分支", (s, e) => StartConnection(nodeControl, ConnectionInvokeType.Upstream)));


          
            #region 右键菜单功能 - 控件对齐

            var AvoidMenu = new MenuItem();
            AvoidMenu.Items.Add(CreateMenuItem("群组对齐", (s, e) =>
            {
                AlignControlsWithGrouping(selectNodeControls, AlignMode.Grouping);
            }));
            AvoidMenu.Items.Add(CreateMenuItem("规划对齐", (s, e) =>
            {
                AlignControlsWithGrouping(selectNodeControls, AlignMode.Planning);
            }));
            AvoidMenu.Items.Add(CreateMenuItem("水平中心对齐", (s, e) =>
            {
                AlignControlsWithGrouping(selectNodeControls, AlignMode.HorizontalCenter);
            }));
            AvoidMenu.Items.Add(CreateMenuItem("垂直中心对齐 ", (s, e) =>
            {
                AlignControlsWithGrouping(selectNodeControls, AlignMode.VerticalCenter);
            }));
            
            AvoidMenu.Items.Add(CreateMenuItem("垂直对齐时水平斜分布", (s, e) =>
            {
                AlignControlsWithGrouping(selectNodeControls, AlignMode.Vertical);
            }));
            AvoidMenu.Items.Add(CreateMenuItem("水平对齐时垂直斜分布", (s, e) =>
            {
                AlignControlsWithGrouping(selectNodeControls, AlignMode.Horizontal);
            }));

            AvoidMenu.Header = "对齐";
            contextMenu.Items.Add(AvoidMenu); 


            #endregion

            nodeControl.ContextMenu = contextMenu;
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
                SereinEnv.WriteLine(InfoType.ERROR, ex.ToString());
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
                        EnvDecorator.LoadLibrary(file);
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

        #region 与流程图/节点相关

        /// <summary>
        /// 鼠标在画布移动。
        /// 选择控件状态下，调整选择框大小
        /// 连接状态下，实时更新连接线的终点位置。
        /// 移动画布状态下，移动画布。
        /// </summary>
        private void FlowChartCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var myData = GlobalJunctionData.MyGlobalConnectingData;
            if (myData.IsCreateing && e.LeftButton == MouseButtonState.Pressed)
            {
                
                if (myData.Type == JunctionOfConnectionType.Invoke)
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
                myData.UpdatePoint(currentPoint);
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

                foreach (var line in Connections)
                {
                    line.RefreshLine(); // 画布移动时刷新所有连接线
                }
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
            try
            {
                var canvasDropPosition = e.GetPosition(FlowChartCanvas); // 更新画布落点
                PositionOfUI position = new PositionOfUI(canvasDropPosition.X, canvasDropPosition.Y);
                if (e.Data.GetDataPresent(MouseNodeType.CreateDllNodeInCanvas))
                {
                    if (e.Data.GetData(MouseNodeType.CreateDllNodeInCanvas) is MoveNodeData nodeData) 
                    {
                        Task.Run(async () =>
                        {
                            await EnvDecorator.CreateNodeAsync(nodeData.NodeControlType, position, nodeData.MethodDetailsInfo); // 创建DLL文件的节点对象
                        });
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
                            Type when typeof(GlobalDataControl).IsAssignableFrom(droppedType) => NodeControlType.GlobalData,
                            _ => NodeControlType.None,
                        };
                        if (nodeControlType != NodeControlType.None)
                        {
                            Task.Run(async () =>
                            {
                                await EnvDecorator.CreateNodeAsync(nodeControlType, position); // 创建基础节点对象
                            });
                        }
                    }
                }
                e.Handled = true;
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(InfoType.ERROR, ex.ToString());
            }
        }

        /// <summary>
        ///  尝试判断是否为区域，如果是，将节点放置在区域中
        /// </summary>
        /// <param name="nodeControl"></param>
        /// <param name="position"></param>
        /// <param name="targetNodeControl">目标节点控件</param>
        /// <returns></returns>
        private bool TryPlaceNodeInRegion(NodeControlBase nodeControl, PositionOfUI position, out NodeControlBase targetNodeControl)
        {
            var point = new Point(position.X, position.Y);
            HitTestResult hitTestResult = VisualTreeHelper.HitTest(FlowChartCanvas, point);
            if (hitTestResult != null && hitTestResult.VisualHit is UIElement hitElement)
            {
                // 准备放置条件表达式控件
                if (nodeControl.ViewModel.NodeModel.ControlType == NodeControlType.ExpCondition)
                {
                    ConditionRegionControl? conditionRegion = GetParentOfType<ConditionRegionControl>(hitElement);
                    if (conditionRegion is not null)
                    {
                        targetNodeControl = conditionRegion;
                        //// 如果存在条件区域容器
                        //conditionRegion.AddCondition(nodeControl);
                        return true;
                    }
                }
                // 准备放置全局数据控件
                else
                {
                    GlobalDataControl? globalDataControl = GetParentOfType<GlobalDataControl>(hitElement);
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

        /// <summary>
        /// 将节点放在目标区域中
        /// </summary>
        /// <param name="regionControl">区域容器</param>
        /// <param name="nodeControl">节点控件</param>
        private void TryPlaceNodeInRegion(NodeControlBase regionControl, NodeControlBase nodeControl)
        {
            // 准备放置条件表达式控件
            if (nodeControl.ViewModel.NodeModel.ControlType == NodeControlType.ExpCondition)
            {
                if (regionControl is ConditionRegionControl conditionRegion)
                {
                    conditionRegion.AddCondition(nodeControl); // 条件区域容器
                }
            }
            else if(regionControl.ViewModel.NodeModel.ControlType == NodeControlType.GlobalData)
            {
                if (regionControl is GlobalDataControl globalDataControl)
                {
                    // 全局数据节点容器
                    globalDataControl.SetDataNodeControl(nodeControl);
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
        /// 控件的鼠标左键按下事件，启动拖动操作。同时显示当前正在传递的数据。
        /// </summary>
        private void Block_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //if (GlobalJunctionData.IsCreatingConnection)
            //{
            //    return;
            //}
            if(sender is NodeControlBase nodeControl)
            {
                ChangeViewerObjOfNode(nodeControl);
                if (nodeControl?.ViewModel?.NodeModel?.MethodDetails?.IsProtectionParameter == true) return;
                IsControlDragging = true;
                startControlDragPoint = e.GetPosition(FlowChartCanvas); // 记录鼠标按下时的位置
                ((UIElement)sender).CaptureMouse(); // 捕获鼠标
                e.Handled = true; // 防止事件传播影响其他控件
            }
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

            if (IsControlDragging) // 如果正在拖动控件
            {
                Point currentPosition = e.GetPosition(FlowChartCanvas); // 获取当前鼠标位置 
                
                if (selectNodeControls.Count > 0 && sender is NodeControlBase nodeControlMain && selectNodeControls.Contains(nodeControlMain))
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

                    this.EnvDecorator.MoveNode(nodeControlMain.ViewModel.NodeModel.Guid, newLeft, newTop); // 移动节点

                    // 计算控件实际移动的距离
                    var actualDeltaX = newLeft - oldLeft;
                    var actualDeltaY = newTop - oldTop;

                    // 移动其它选中的控件
                    foreach (var nodeControl in selectNodeControls)
                    {
                        if (nodeControl != nodeControlMain) // 跳过已经移动的控件
                        {
                            var otherNewLeft = Canvas.GetLeft(nodeControl) + actualDeltaX;
                            var otherNewTop = Canvas.GetTop(nodeControl) + actualDeltaY;
                            this.EnvDecorator.MoveNode(nodeControl.ViewModel.NodeModel.Guid, otherNewLeft, otherNewTop); // 移动节点
                        }
                    }

                    // 更新节点之间线的连接位置
                    foreach (var nodeControl in selectNodeControls)
                    {
                        nodeControl.UpdateLocationConnections();
                    }
                }
                else
                {   // 单个节点移动
                    if (sender is not NodeControlBase nodeControl)
                    {
                        return;
                    }
                    double deltaX = currentPosition.X - startControlDragPoint.X; // 计算X轴方向的偏移量
                    double deltaY = currentPosition.Y - startControlDragPoint.Y; // 计算Y轴方向的偏移量
                    double newLeft = Canvas.GetLeft(nodeControl) + deltaX; // 新的左边距
                    double newTop = Canvas.GetTop(nodeControl) + deltaY; // 新的上边距
                    this.EnvDecorator.MoveNode(nodeControl.ViewModel.NodeModel.Guid, newLeft, newTop); // 移动节点
                    nodeControl.UpdateLocationConnections();
                }
                startControlDragPoint = currentPosition; // 更新起始点位置
            }

        }
        

        // 改变对象树？
        private void ChangeViewerObjOfNode(NodeControlBase nodeControl)
        {
            var node = nodeControl.ViewModel.NodeModel;
            //if (node is not null && (node.MethodDetails is null || node.MethodDetails.ReturnType != typeof(void))
            if (node is not null && node.MethodDetails?.ReturnType != typeof(void))
            {
                var key = node.Guid;
                object instance = null;
                //Console.WriteLine("WindowXaml 后台代码中 ChangeViewerObjOfNode 需要重新设计");
                //var instance = node.GetFlowData(); // 对象预览树视图获取（后期更改）
                if(instance is not null)
                {
                    ViewObjectViewer.LoadObjectInformation(key, instance);
                    ChangeViewerObj(key, instance);
                }
            }
        }
        public void ChangeViewerObj(string key, object instance)
        {
            if (ViewObjectViewer.MonitorObj is null)
            {
                EnvDecorator.SetMonitorObjState(key, true); // 通知环境，该节点的数据更新后需要传到UI
                return;
            }
            if (instance is null)
            {
                return;
            }
            if (key.Equals(ViewObjectViewer.MonitorKey) == true)
            {
                ViewObjectViewer.RefreshObjectTree(instance);
                return;
            }
            else
            {
                EnvDecorator.SetMonitorObjState(ViewObjectViewer.MonitorKey,false); // 取消对旧节点的监视
                EnvDecorator.SetMonitorObjState(key, true); // 通知环境，该节点的数据更新后需要传到UI
            }
        }
        #endregion

        #region UI连接控件操作

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
            //    EnvDecorator.ConnectNodeAsync(formNodeGuid, toNodeGuid,0,0, currentConnectionType);
            //}
            //GlobalJunctionData.OK();
        }


        /// <summary>
        /// 结束连接操作，清理状态并移除虚线。
        /// </summary>
        private void EndConnection()
        {
            Mouse.OverrideCursor = null; // 恢复视觉效果
            ViewModel.IsConnectionArgSourceNode = false;
            ViewModel.IsConnectionInvokeNode = false;
            GlobalJunctionData.OK();
        }

        #region 拖动画布实现缩放平移效果
        private void FlowChartCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            IsCanvasDragging = true;
            startCanvasDragPoint = e.GetPosition(this);
            FlowChartCanvas.CaptureMouse();
            e.Handled = true; // 防止事件传播影响其他控件
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
            // if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (e.Delta  < 0 && scaleTransform.ScaleX < 0.05) return;
                if (e.Delta  > 0 && scaleTransform.ScaleY > 2.0) return;
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


        private void Test(double deltaX, double deltaY)
        {
            //Console.WriteLine((translateTransform.X, translateTransform.Y));
            //translateTransform.X += deltaX;
            //translateTransform.Y += deltaY;
        }

        #endregion
        #endregion

        

        #endregion

        #region 画布中框选节点控件动作

        /// <summary>
        /// 在画布中尝试选取控件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlowChartCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (GlobalJunctionData.MyGlobalConnectingData.IsCreateing)
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
            if (GlobalJunctionData.MyGlobalConnectingData is ConnectingData myData && myData.IsCreateing)
            {
                
                if (myData.IsCanConnected)
                {
                    var canvas = this.FlowChartCanvas;
                    var currentendPoint = e.GetPosition(canvas); // 当前鼠标落点
                    var changingJunctionPosition = myData.CurrentJunction.TranslatePoint(new Point(0, 0), canvas);
                    var changingJunctionRect = new Rect(changingJunctionPosition, new Size(myData.CurrentJunction.Width, myData.CurrentJunction.Height));
                    
                    if (changingJunctionRect.Contains(currentendPoint)) // 可以创建连接
                    {
                        #region 方法调用关系创建
                        if (myData.Type == JunctionOfConnectionType.Invoke)
                        {
                            this.EnvDecorator.ConnectInvokeNodeAsync(myData.StartJunction.MyNode.Guid, myData.CurrentJunction.MyNode.Guid,
                                        myData.StartJunction.JunctionType,
                                        myData.CurrentJunction.JunctionType,
                                        myData.ConnectionInvokeType);
                        } 
                        #endregion
                        #region 参数来源关系创建
                        else if (myData.Type == JunctionOfConnectionType.Arg)
                        {
                            var argIndex = 0;
                            if (myData.StartJunction is ArgJunctionControl argJunction1)
                            {
                                argIndex = argJunction1.ArgIndex;
                            }
                            else if (myData.CurrentJunction is ArgJunctionControl argJunction2)
                            {
                                argIndex = argJunction2.ArgIndex;
                            }

                            this.EnvDecorator.ConnectArgSourceNodeAsync(myData.StartJunction.MyNode.Guid, myData.CurrentJunction.MyNode.Guid,
                                    myData.StartJunction.JunctionType,
                                    myData.CurrentJunction.JunctionType,
                                    myData.ConnectionArgSourceType,
                                    argIndex);
                        } 
                        #endregion

                    }
                    EndConnection();
                }

            }
            e.Handled = true;

        }

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
        private ContextMenu ConfiguerSelectionRectangle()
        {
            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(CreateMenuItem("删除", (s, e) =>
            {
                if (selectNodeControls.Count > 0)
                {
                    foreach (var node in selectNodeControls.ToArray())
                    {
                        var guid = node?.ViewModel?.NodeModel?.Guid;
                        if (!string.IsNullOrEmpty(guid))
                        {
                            EnvDecorator.RemoveNodeAsync(guid);
                        }
                    }
                }
                SelectionRectangle.Visibility = Visibility.Collapsed;
            }));
            return contextMenu;
            // nodeControl.ContextMenu = contextMenu;
        }
        private void SelectedNode()
        {

            if (selectNodeControls.Count == 0)
            {
                //Console.WriteLine($"没有选择控件");
                SelectionRectangle.Visibility = Visibility.Collapsed;
                return;
            }
            if(selectNodeControls.Count == 1)
            {
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
                if (nodeControl.ViewModel.NodeModel.IsStart)
                {
                    nodeControl.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#04FC10"));
                    nodeControl.BorderThickness = new Thickness(2);
                }
            }
            selectNodeControls.Clear();
        }
        #endregion

        #region 节点对齐 （有些小瑕疵）

        //public void UpdateConnectedLines()
        //{
        //    //foreach (var nodeControl in selectNodeControls)
        //    //{
        //    //    UpdateConnections(nodeControl);
        //    //}
        //    this.Dispatcher.Invoke(() =>
        //    {
        //        foreach (var line in Connections)
        //        {
        //            line.AddOrRefreshLine(); // 节点完成对齐
        //        }
        //    });
           
        //}


        #region Plan A 群组对齐

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

        #region 静态方法：创建节点，创建菜单子项，获取区域

        /// <summary>
        /// 创建节点控件
        /// </summary>
        /// <param name="controlType">节点控件视图控件类型</param>
        /// <param name="viewModelType">节点控件ViewModel类型</param>
        /// <param name="model">节点Model实例</param>
        /// <returns></returns>
        /// <exception cref="Exception">无法创建节点控件</exception>
        private static NodeControlBase CreateNodeControl(Type controlType, Type viewModelType, NodeModelBase model)
        {
            if ((controlType is null)
                || viewModelType is null 
                || model is null)
            {
                throw new Exception("无法创建节点控件");
            }
            if (typeof(NodeControlBase).IsSubclassOf(controlType) || typeof(NodeControlViewModelBase).IsSubclassOf(viewModelType))
            {
                throw new Exception("无法创建节点控件");
            }

            if (string.IsNullOrEmpty(model.Guid))
            {
                model.Guid = Guid.NewGuid().ToString();
            }

            // Convert.ChangeType(model, targetType);

            var viewModel = Activator.CreateInstance(viewModelType, [model]);
            var controlObj = Activator.CreateInstance(controlType, [viewModel]);
            if (controlObj is NodeControlBase nodeControl)
            {
                return nodeControl;
            }
            else
            {
                throw new Exception("无法创建节点控件");
            }
        }


        /// <summary>
        /// 创建菜单子项
        /// </summary>
        /// <param name="header"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
        public static MenuItem CreateMenuItem(string header, RoutedEventHandler handler)
        {
            var menuItem = new MenuItem { Header = header };
            menuItem.Click += handler;
            return menuItem;
        }



        /// <summary>
        /// 穿透元素获取区域容器
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="element"></param>
        /// <returns></returns>
        public static T? GetParentOfType<T>(DependencyObject element) where T : DependencyObject
        {
            while (element != null)
            {
                if (element is T e)
                {
                    return e;
                }
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }

        #endregion

        #region 节点树、IOC视图管理

        private void JudgmentFlipFlopNode(NodeControlBase nodeControl)
        {
            if (nodeControl is FlipflopNodeControl flipflopControl
                && flipflopControl?.ViewModel?.NodeModel is NodeModelBase nodeModel) // 判断是否为触发器
            {
                int count = 0;
                foreach (var ct in NodeStaticConfig.ConnectionTypes)
                {
                    count += nodeModel.PreviousNodes[ct].Count;
                }
                if (count == 0)
                {
                    NodeTreeViewer.AddGlobalFlipFlop(EnvDecorator, nodeModel); // 添加到全局触发器树树视图
                }
                else
                {
                    NodeTreeViewer.RemoteGlobalFlipFlop(nodeModel); // 从全局触发器树树视图中移除
                }
            }
        }
        void LoadIOCObjectViewer()
        {

        }
        #endregion


        
        #region 顶部菜单栏 - 调试功能区

        /// <summary>
        /// 运行测试
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonDebugRun_Click(object sender, RoutedEventArgs e)
        {
            LogOutWindow?.Show();



#if WINDOWS
            //Dispatcher uiDispatcher = Application.Current.MainWindow.Dispatcher;
            //SynchronizationContext? uiContext = SynchronizationContext.Current;
            //EnvDecorator.IOC.CustomRegisterInstance(typeof(SynchronizationContextk).FullName, uiContext,  false);
#endif

            // 获取主线程的 SynchronizationContext
            Action<SynchronizationContext, Action> uiInvoke = (uiContext, action) => uiContext?.Post(state => action?.Invoke(), null);

            
            
            Task.Run(async () =>
            {
                await EnvDecorator.StartAsync();
            }); 

            // await EnvDecorator.StartAsync(); 
            //await Task.Factory.StartNew(FlowEnvironment.StartAsync); 
        }

        /// <summary>
        /// 退出
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonDebugFlipflopNode_Click(object sender, RoutedEventArgs e)
        {
            EnvDecorator?.ExitFlow(); // 在运行平台上点击了退出
        }

        /// <summary>
        /// 从选定的节点开始运行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ButtonStartFlowInSelectNode_Click(object sender, RoutedEventArgs e)
        {
            if (selectNodeControls.Count == 0)
            {
                SereinEnv.WriteLine(InfoType.INFO, "请至少选择一个节点");
            }
            else if (selectNodeControls.Count > 1)
            {
                SereinEnv.WriteLine(InfoType.INFO, "请只选择一个节点");
            }
            else
            {
                await this.EnvDecorator.StartAsyncInSelectNode(selectNodeControls[0].ViewModel.NodeModel.Guid);
            }

        }




        #endregion

        #region 顶部菜单栏 - 项目文件菜单


        /// <summary>
        /// 保存为项目文件 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ButtonSaveFile_Click(object sender, RoutedEventArgs e)
        {
            EnvDecorator.SaveProject();
        }
        

        /// <summary>
        /// 打开本地项目文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonOpenLocalProject_Click(object sender, RoutedEventArgs e)
        {

        }

       

#endregion

        #region 顶部菜单栏 - 视图管理
        /// <summary>
        /// 重置画布
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonResetCanvas_Click(object sender, RoutedEventArgs e)
        {
            translateTransform.X = 0;
            translateTransform.Y = 0;
            scaleTransform.ScaleX = 1;
            scaleTransform.ScaleY = 1;
        }
        /// <summary>
        /// 查看输出日志窗口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonOpenConsoleOutWindow_Click(object sender, RoutedEventArgs e)
        {
            LogOutWindow?.Show();
        }
        /// <summary>
        /// 定位节点
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonLocationNode_Click(object sender, RoutedEventArgs e)
        {
            InputDialog inputDialog = new InputDialog();
            inputDialog.Closed += (s, e) =>
            {
                var nodeGuid = inputDialog.InputValue;
                EnvDecorator.NodeLocated(nodeGuid);
            };
            inputDialog.ShowDialog();
        }

        #endregion

        #region 顶部菜单栏 - 远程管理
        private async void ButtonStartRemoteServer_Click(object sender, RoutedEventArgs e)
        {

             await this.EnvDecorator.StartRemoteServerAsync();
        }

        /// <summary>
        /// 连接远程运行环境
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonConnectionRemoteEnv_Click(object sender, RoutedEventArgs e)
        {
            var windowEnvRemoteLoginView = new WindowEnvRemoteLoginView(async (addres, port, token) =>
            {
                ResetFlowEnvironmentEvent();// 移除事件
                (var isConnect, var _) = await this.EnvDecorator.ConnectRemoteEnv(addres, port, token);
                InitFlowEnvironmentEvent(); // 重新添加事件（如果没有连接成功，那么依然是原本的环境）
                if (isConnect)
                {
                    // 连接成功，加载远程项目
                    _ = Task.Run(async () =>
                    {
                        var flowEnvInfo = await EnvDecorator.GetEnvInfoAsync();
                        EnvDecorator.LoadProject(flowEnvInfo, string.Empty);// 加载远程环境的项目

                    });
                   
                }
            });
            windowEnvRemoteLoginView.Show();

        }
        #endregion



        /// <summary>
        /// 窗体按键监听。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                e.Handled = true; // 禁止默认的Tab键行为
            }

            #region 复制粘贴选择的节点
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.C && selectNodeControls.Count > 0)
                {
                    CpoyNodeInfo();
                }
                else if (e.Key == Key.V)
                {
                    PasteNodeInfo();
                }
            }
            #endregion
            if (e.KeyStates == Keyboard.GetKeyStates(Key.Escape))
            {
                IsControlDragging = false;
                IsCanvasDragging = false;
                SelectionRectangle.Visibility = Visibility.Collapsed;
                CancelSelectNode();
                EndConnection();
            }

            if(GlobalJunctionData.MyGlobalConnectingData is ConnectingData myData && myData.IsCreateing)
            {
                if(myData.Type == JunctionOfConnectionType.Invoke)
                {
                    ConnectionInvokeType connectionInvokeType = e.KeyStates switch
                    {
                        KeyStates k when k == Keyboard.GetKeyStates(Key.D1) => ConnectionInvokeType.Upstream,
                        KeyStates k when k == Keyboard.GetKeyStates(Key.D2) => ConnectionInvokeType.IsSucceed,
                        KeyStates k when k == Keyboard.GetKeyStates(Key.D3) => ConnectionInvokeType.IsFail,
                        KeyStates k when k == Keyboard.GetKeyStates(Key.D4) => ConnectionInvokeType.IsError,
                        _ => ConnectionInvokeType.None,
                    };
                    
                    if (connectionInvokeType != ConnectionInvokeType.None)
                    {
                        myData.ConnectionInvokeType = connectionInvokeType;
                        myData.MyLine.Line.UpdateLineColor(connectionInvokeType.ToLineColor());
                    }
                }
                else if (myData.Type == JunctionOfConnectionType.Arg)
                {
                    ConnectionArgSourceType connectionArgSourceType = e.KeyStates switch
                    {
                        KeyStates k when k == Keyboard.GetKeyStates(Key.D1) => ConnectionArgSourceType.GetOtherNodeData,
                        KeyStates k when k == Keyboard.GetKeyStates(Key.D2) => ConnectionArgSourceType.GetOtherNodeDataOfInvoke,
                        _ => ConnectionArgSourceType.GetPreviousNodeData,
                    };
                    
                    if (connectionArgSourceType != ConnectionArgSourceType.GetPreviousNodeData)
                    {
                        myData.ConnectionArgSourceType = connectionArgSourceType;
                        myData.MyLine.Line.UpdateLineColor(connectionArgSourceType.ToLineColor());
                    }
                }
                myData.CurrentJunction.InvalidateVisual(); // 刷新目标节点控制点样式

            }
            

        }

        #region 复制节点，粘贴节点

        /// <summary>
        /// 复制节点
        /// </summary>
        private void CpoyNodeInfo()
        {
            // 处理复制操作
            var dictSelection = selectNodeControls
                .Select(control => control.ViewModel.NodeModel.ToInfo())
                .ToDictionary(kvp => kvp.Guid, kvp => kvp);

            // 遍历当前已选节点
            foreach (var node in dictSelection.Values.ToArray())
            {
                if(node.ChildNodeGuids is null)
                {
                    continue;
                }
                // 遍历这些节点的子节点，获得完整的已选节点信息
                foreach (var childNodeGuid in node.ChildNodeGuids)
                {
                    if(!dictSelection.ContainsKey(childNodeGuid) &&  NodeControls.TryGetValue(childNodeGuid,out var childNode))
                    {
                        dictSelection.Add(childNodeGuid, childNode.ViewModel.NodeModel.ToInfo());
                    }
                }
            }
            

            JObject json = new JObject()
            {
                ["nodes"] = JArray.FromObject(dictSelection.Values)
            };

            var jsonText = json.ToString();


            try
            {
                //Clipboard.SetDataObject(result, true); // 持久性设置
                Clipboard.SetDataObject(jsonText, true); // 持久性设置
                    SereinEnv.WriteLine(InfoType.INFO, $"复制已选节点（{dictSelection.Count}个）");
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(InfoType.ERROR, $"复制失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 粘贴节点
        /// </summary>
        private void PasteNodeInfo()
        {
            if (Clipboard.ContainsText())
            {
                try
                {

                    string clipboardText = Clipboard.GetText(TextDataFormat.Text);
                    string jsonText = JObject.Parse(clipboardText)["nodes"].ToString();
                    List<NodeInfo> nodes = JsonConvert.DeserializeObject<List<NodeInfo>>(jsonText);
                    if (nodes is null || nodes.Count < 0)
                    {
                        return;
                    }

                    #region 节点去重
                    Dictionary<string, string> guids = new Dictionary<string, string>(); // 记录 Guid
                    // 遍历当前已选节点
                    foreach (var node in nodes.ToArray())
                    {
                        if (NodeControls.ContainsKey(node.Guid) && !guids.ContainsKey(node.Guid))
                        {
                            // 如果是没出现过、且在当前记录中重复的Guid，则记录并新增对应的映射。
                            guids.TryAdd(node.Guid, Guid.NewGuid().ToString());
                        }
                        else
                        {
                            // 出现过的Guid，说明重复添加了。应该不会走到这。
                            continue;
                        }

                        if (node.ChildNodeGuids is null)
                        {
                            continue; // 跳过没有子节点的节点
                        }

                        // 遍历这些节点的子节点，获得完整的已选节点信息
                        foreach (var childNodeGuid in node.ChildNodeGuids)
                        {
                            if (NodeControls.ContainsKey(node.Guid) && !NodeControls.ContainsKey(node.Guid))
                            {
                                // 当前Guid并不重复，跳过替换
                                continue;
                            }
                            if (!guids.ContainsKey(childNodeGuid))
                            {
                                // 如果是没出现过的Guid，则记录并新增对应的映射。
                                guids.TryAdd(node.Guid, Guid.NewGuid().ToString());
                            }

                            if (!string.IsNullOrEmpty(childNodeGuid)
                                && NodeControls.TryGetValue(childNodeGuid, out var nodeControl))
                            {

                                var newNodeInfo = nodeControl.ViewModel.NodeModel.ToInfo();
                                nodes.Add(newNodeInfo);
                            }
                        }
                    }

                    //var flashText = new FlashText.NET.TextReplacer();

                    //var t = guids.Select(kvp => (kvp.Key, kvp.Value)).ToArray();
                    //var result = flashText.ReplaceWords(jsonText, t);

                    



                    StringBuilder sb = new StringBuilder(jsonText);
                     foreach (var kv in guids)
                     {
                         sb.Replace(kv.Key, kv.Value);
                     }
                     string result = sb.ToString();


                    /*var replacer = new GuidReplacer();
                    foreach (var kv in guids)
                    {
                       replacer.AddReplacement(kv.Key, kv.Value);
                    }
                    string result = replacer.Replace(jsonText);*/


                    //SereinEnv.WriteLine(InfoType.ERROR, result);
                    nodes = JsonConvert.DeserializeObject<List<NodeInfo>>(result);

                    if (nodes is null || nodes.Count < 0)
                    {
                        return;
                    }
                    #endregion

                    Point mousePosition = Mouse.GetPosition(FlowChartCanvas);
                    PositionOfUI positionOfUI = new PositionOfUI(mousePosition.X, mousePosition.Y); // 坐标数据

                    // 获取第一个节点的原始位置
                    var index0NodeX = nodes[0].Position.X;
                    var index0NodeY = nodes[0].Position.Y;

                    // 计算所有节点相对于第一个节点的偏移量
                    foreach (var node in nodes)
                    {

                        var offsetX = node.Position.X - index0NodeX;
                        var offsetY = node.Position.Y - index0NodeY;

                        // 根据鼠标位置平移节点
                        node.Position = new PositionOfUI(positionOfUI.X + offsetX, positionOfUI.Y + offsetY);
                    }

                    _ = EnvDecorator.LoadNodeInfosAsync(nodes);
                }
                catch (Exception ex)
                {

                    SereinEnv.WriteLine(InfoType.ERROR, $"粘贴节点时发生异常：{ex}");
                }


                // SereinEnv.WriteLine(InfoType.INFO, $"剪贴板文本内容: {clipboardText}");
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

        #endregion


        /// <summary>
        /// 卸载DLL文件，清空当前项目
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UnloadAllButton_Click(object sender, RoutedEventArgs e)
        {
            EnvDecorator.ClearAll();
        }

        /// <summary>
        /// 卸载DLL文件，清空当前项目
        /// </summary>
        private void UnloadAllAssemblies()
        {
            DllStackPanel.Children.Clear();
            FlowChartCanvas.Children.Clear();
            Connections.Clear();
            NodeControls.Clear();
            //currentLine = null;
            //startConnectNodeControl = null;
            MessageBox.Show("所有DLL已卸载。", "信息", MessageBoxButton.OK, MessageBoxImage.Information);
        }




        /* /// <summary>
        /// 对象装箱测试
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonTestExpObj_Click(object sender, RoutedEventArgs e)
        {
            //string jsonString = 
            //"""
            //{
            //    "Name": "张三",
            //    "Age": 24,
            //    "Address": {
            //        "City": "北京",
            //        "PostalCode": "10000"
            //    }
            //}
            //""";

           var externalData = new Dictionary<string, object>
            {
                { "Name", "John" },
                { "Age", 30 },
                { "Addresses", new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            { "Street", "123 Main St" },
                            { "City", "New York" }
                        },
                        new Dictionary<string, object>
                        {
                            { "Street", "456 Another St" },
                            { "City", "Los Angeles" }
                        }
                    }
                }
            };

            if (!ObjDynamicCreateHelper.TryResolve(externalData, "RootType",out var result))
            {
                SereinEnv.WriteLine(InfoType.ERROR, "赋值过程中有错误，请检查属性名和类型！");
                return;
            }
            ObjDynamicCreateHelper.PrintObjectProperties(result!);
            var exp = "@set .Addresses[1].Street = 233";
            var data = SerinExpressionEvaluator.Evaluate(exp, result!, out bool isChange);
            exp = "@get .Addresses[1].Street";
            data = SerinExpressionEvaluator.Evaluate(exp,result!, out isChange);
            SereinEnv.WriteLine(InfoType.INFO, $"{exp} => {data}");
        }
*/
    }
}