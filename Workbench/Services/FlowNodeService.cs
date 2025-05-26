using Serein.Library;
using Serein.Library.Api;
using Serein.Workbench.Api;
using Serein.Workbench.Node;
using Serein.Workbench.Node.View;
using Serein.Workbench.Node.ViewModel;
using Serein.Workbench.ViewModels;
using Serein.Workbench.Views;
using System.Windows.Controls;

namespace Serein.Workbench.Services
{
    /// <summary>
    /// 流程节点管理
    /// </summary>
    public class FlowNodeService
    {


        #region 流程节点操作的相关事件
        /// <summary>
        /// 添加了画布
        /// </summary>
        public Action<FlowCanvasView> OnCreateFlowCanvasView { get; set; }
        /// <summary>
        /// 移除了画布
        /// </summary>
        public Action<FlowCanvasView> OnRemoveFlowCanvasView { get; set; }

        /// <summary>
        /// 添加了节点
        /// </summary>
        public Action<NodeControlBase> OnCreateNode { get; set; }
        
        #endregion


        #region 创建节点相关的属性
        /// <summary>
        /// 当前查看的画布
        /// </summary>
        public FlowCanvasView CurrentSelectCanvas { get; set; }

        /// <summary>
        /// 当前拖动的方法信息
        /// </summary>
        public MethodDetailsInfo? CurrentDragMdInfo { get; set; }

        /// <summary>
        /// 当前需要创建的节点类型
        /// </summary>
        public NodeControlType CurrentNodeControlType { get; set; } = NodeControlType.None;


        /// <summary>
        /// 当前鼠标位置
        /// </summary>
        public PositionOfUI? CurrentMouseLocation { get; set; }

        /// <summary>
        /// 当前选中的节点
        /// </summary>
        public NodeControlBase? CurrentSelectNodeControl { get; set; }
        #endregion

        /// <summary>
        /// 连接开始节点
        /// </summary>
        public NodeControlBase? ConnectionStartNode { get; set; }
        /// <summary>
        /// 连接最终落点节点
        /// </summary>
        public NodeControlBase? ConnectionEndNode { get; set; }

        /// <summary>
        /// 记录流程画布
        /// </summary>
        private readonly Dictionary<string, FlowCanvasView> Canvass = [];

        /// <summary>
        /// 记录加载的节点
        /// </summary>
        private readonly Dictionary<string, NodeControlBase> NodeControls = [];

        /// <summary>
        /// 运行环境接口
        /// </summary>
        private readonly IFlowEnvironment flowEnvironment;

        /// <summary>
        /// 运行环境事件转发器
        /// </summary>
        private readonly IFlowEEForwardingService flowEEForwardingService;


        #region 初始化
        public FlowNodeService(IFlowEnvironment flowEnvironment,
                               IFlowEEForwardingService flowEEForwardingService)
        {
            this.flowEnvironment = flowEnvironment;
            this.flowEEForwardingService = flowEEForwardingService;
            InitFlowEvent();
            InitNodeType();
        }


        /// <summary>
        /// 注册节点类型
        /// </summary>
        private void InitNodeType()
        {
           flowEnvironment.NodeMVVMManagement.RegisterUI(NodeControlType.UI, typeof(UINodeControl), typeof(UINodeControlViewModel));
           flowEnvironment.NodeMVVMManagement.RegisterUI(NodeControlType.Action, typeof(ActionNodeControl), typeof(ActionNodeControlViewModel));
           flowEnvironment.NodeMVVMManagement.RegisterUI(NodeControlType.Flipflop, typeof(FlipflopNodeControl), typeof(FlipflopNodeControlViewModel));
           flowEnvironment.NodeMVVMManagement.RegisterUI(NodeControlType.ExpOp, typeof(ExpOpNodeControl), typeof(ExpOpNodeControlViewModel));
           flowEnvironment.NodeMVVMManagement.RegisterUI(NodeControlType.ExpCondition, typeof(ConditionNodeControl), typeof(ConditionNodeControlViewModel));
           flowEnvironment.NodeMVVMManagement.RegisterUI(NodeControlType.ConditionRegion, typeof(ConditionRegionControl), typeof(ConditionRegionNodeControlViewModel));
           flowEnvironment.NodeMVVMManagement.RegisterUI(NodeControlType.GlobalData, typeof(GlobalDataControl), typeof(GlobalDataNodeControlViewModel));
           flowEnvironment.NodeMVVMManagement.RegisterUI(NodeControlType.Script, typeof(ScriptNodeControl), typeof(ScriptNodeControlViewModel));
           flowEnvironment.NodeMVVMManagement.RegisterUI(NodeControlType.NetScript, typeof(NetScriptNodeControl), typeof(NetScriptNodeControlViewModel));
        }

        /// <summary>
        /// 注册节点事件
        /// </summary>
        private void InitFlowEvent()
        {
            flowEEForwardingService.OnCanvasCreate += FlowEEForwardingService_OnCanvasCreate;  // 创建了画布
            flowEEForwardingService.OnCanvasRemove += FlowEEForwardingService_OnCanvasRemove; // 移除了画布
            flowEEForwardingService.OnNodeCreate += FlowEEForwardingService_OnNodeCreate; // 创建了节点
            flowEEForwardingService.OnNodeRemove += FlowEEForwardingService_OnNodeRemove; // 移除了节点

            flowEEForwardingService.OnNodePlace += FlowEEForwardingService_OnNodePlace; // 节点放置在容器中
            flowEEForwardingService.OnNodeTakeOut += FlowEEForwardingService_OnNodeTakeOut; ; // 节点从容器中取出

            flowEEForwardingService.OnNodeConnectChange += FlowEEForwardingService_OnNodeConnectChange; // 节点连接状态改变事件
            
        }

        private void FlowEEForwardingService_OnNodeConnectChange(NodeConnectChangeEventArgs e)
        {
            var canvasGuid = e.CanvasGuid;
            string fromNodeGuid = e.FromNodeGuid;
            string toNodeGuid = e.ToNodeGuid;
            if (!TryGetCanvas(canvasGuid, out var flowCanvas) 
                || flowCanvas is not IFlowCanvas flow
                || !TryGetControl(fromNodeGuid, out var fromNode)
                || !TryGetControl(toNodeGuid, out var toNode))
            {
                return;
            }
            
            Action? action = (e.JunctionOfConnectionType, e.ChangeType) switch
            {
                (JunctionOfConnectionType.Invoke, NodeConnectChangeEventArgs.ConnectChangeType.Create) => () => flow.CreateInvokeConnection(fromNode, toNode, e.ConnectionInvokeType), // 创建节点之间的调用关系
                (JunctionOfConnectionType.Invoke, NodeConnectChangeEventArgs.ConnectChangeType.Remove) => () => flow.RemoveInvokeConnection(fromNode, toNode), // 移除节点之间的调用关系
                (JunctionOfConnectionType.Arg, NodeConnectChangeEventArgs.ConnectChangeType.Create) => () => flow.CreateArgConnection(fromNode, toNode, e.ConnectionArgSourceType, e.ArgIndex), // 创建节点之间的参数传递关系
                (JunctionOfConnectionType.Arg, NodeConnectChangeEventArgs.ConnectChangeType.Remove) => () => flow.RemoveArgConnection(fromNode, toNode, e.ArgIndex), // 移除节点之间的参数传递关系
                _ => null
            };
            action?.Invoke();
            return;

        }

        private void FlowEEForwardingService_OnNodeTakeOut(NodeTakeOutEventArgs eventArgs)
        {
            string nodeGuid = eventArgs.NodeGuid;
            if (!TryGetControl(nodeGuid, out var nodeControl))
            {
                return;
            }
            nodeControl.TakeOutContainer(); // 从容器节点中取出
        }

        private void FlowEEForwardingService_OnNodePlace(NodePlaceEventArgs eventArgs)
        {
            string nodeGuid = eventArgs.NodeGuid;
            string containerNodeGuid = eventArgs.ContainerNodeGuid;
            if (!TryGetControl(nodeGuid, out var nodeControl)
               || !TryGetControl(containerNodeGuid, out var containerNodeControl))
            {
                return;
            }
            if (containerNodeControl is not INodeContainerControl containerControl)
            {
                SereinEnv.WriteLine(InfoType.WARN,
                    $"节点[{nodeGuid}]无法放置于节点[{containerNodeGuid}]，" +
                    $"因为后者并不实现 INodeContainerControl 接口");
                return;
            }
            nodeControl.PlaceToContainer(containerControl); // 放置在容器节点中
        }
        #endregion

        #region 节点、画布相关的事件

        /// <summary>
        /// 节点移除
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FlowEEForwardingService_OnNodeRemove(NodeRemoveEventArgs eventArgs)
        {
            if (!TryGetCanvas(eventArgs.CanvasGuid, out var nodeCanvas) || nodeCanvas is not IFlowCanvas api)
            {
                SereinEnv.WriteLine(InfoType.INFO, $"无法移除节点，画布不存在。");
                return;
            }
            if (!TryGetControl(eventArgs.NodeGuid, out var nodeControl))
            {
                SereinEnv.WriteLine(InfoType.INFO, $"无法移除节点，节点不存在。");
                return;
            }
            api.Remove(nodeControl);

        }

        /// <summary>
        /// 节点创建
        /// </summary>
        /// <param name="eventArgs"></param>

        private void FlowEEForwardingService_OnNodeCreate(NodeCreateEventArgs eventArgs)
        {
            #region 校验事件传入值
            var position = eventArgs.Position;
            var cavnasGuid = eventArgs.CanvasGuid;
            var nodeModel = eventArgs.NodeModel;
            if (NodeControls.ContainsKey(nodeModel.Guid))
            {
                SereinEnv.WriteLine(InfoType.WARN, $"创建节点时发生意外：节点Guid重复 - {nodeModel.Guid}");
                return;
            }

            if (!flowEnvironment.NodeMVVMManagement.TryGetType(nodeModel.ControlType, out var nodeMVVM))
            {
                SereinEnv.WriteLine(InfoType.INFO, $"无法创建{nodeModel.ControlType}节点，节点类型尚未注册。");
                return;
            }
            if (nodeMVVM.ControlType == null|| nodeMVVM.ViewModelType == null)
            {
                SereinEnv.WriteLine(InfoType.INFO, $"无法创建{nodeModel.ControlType}节点，UI类型尚未注册（请通过 NodeMVVMManagement.RegisterUI() 方法进行注册）。");
                return;
            }

            if (!TryGetCanvas(cavnasGuid, out var nodeCanvas))
            {
                SereinEnv.WriteLine(InfoType.INFO, $"无法创建{nodeModel.ControlType}节点，不存在画布【{cavnasGuid}】。");
                return;
            }
            #endregion

            #region 创建控件

            NodeControlBase nodeControl;
            try
            {
                nodeControl = CreateNodeControl(nodeMVVM.ControlType, // 控件UI类型
                                                 nodeMVVM.ViewModelType, // 控件VIewModel类型
                                                 nodeModel,  // 控件数据实体
                                                 nodeCanvas); // 所在画布
                OnCreateNode.Invoke(nodeControl); // 创建节点
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(ex);
                return;
            }

            NodeControls.TryAdd(nodeControl.ViewModel.NodeModel.Guid, nodeControl); // 记录创建了的节点控件

            #endregion

        }

        /// <summary>
        /// 画布移除
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FlowEEForwardingService_OnCanvasRemove(CanvasRemoveEventArgs eventArgs)
        {
            if (!TryGetCanvas(eventArgs.CanvasGuid, out var nodeCanvas))
            {
                SereinEnv.WriteLine(InfoType.INFO, $"无法移除画布，画布不存在。");
                return;
            }
            OnRemoveFlowCanvasView.Invoke(nodeCanvas);
        }

        /// <summary>
        /// 画布创建
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FlowEEForwardingService_OnCanvasCreate(CanvasCreateEventArgs eventArgs)
        {
            var model = eventArgs.Model;
            var guid = model.Guid;
            if (Canvass.ContainsKey(guid))
            {
                SereinEnv.WriteLine(InfoType.WARN, $"创建画布时发生意外：节点Guid重复 - {guid}");
                return;
            }

            FlowCanvasView canvasView = new FlowCanvasView(model);
            //canvasView.ViewModel.Model = model;
            //canvasView.ViewModel.CanvasGuid = model.Guid;
            //canvasView.ViewModel.Name = model.Name;
            //canvasView.SetBinding(model);
            Canvass.Add(model.Guid, canvasView);
            OnCreateFlowCanvasView.Invoke(canvasView); // 传递给订阅者
        }



        #endregion

        /// <summary>
        /// 创建节点控件
        /// </summary>
        /// <param name="controlType">节点控件视图控件类型</param>
        /// <param name="viewModelType">节点控件ViewModel类型</param>
        /// <param name="model">节点Model实例</param>
        /// <param name="nodeCanvas">节点所在画布</param>
        /// <returns></returns>
        /// <exception cref="Exception">无法创建节点控件</exception>
        private static NodeControlBase CreateNodeControl(Type controlType, Type viewModelType, NodeModelBase model, IFlowCanvas nodeCanvas)
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

            var viewModel = Activator.CreateInstance(viewModelType, [model]);
            var controlObj = Activator.CreateInstance(controlType, [viewModel]);
            if (controlObj is NodeControlBase nodeControl)
            {
                nodeControl.FlowCanvas = nodeCanvas;
                return nodeControl;
            }
            else
            {
                throw new Exception("无法创建节点控件");
            }
        }

        /// <summary>
        /// 从Guid获取节点控件
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="nodeControl"></param>
        /// <returns></returns>
        private bool TryGetControl(string nodeGuid, out NodeControlBase nodeControl)
        {
            nodeControl = null;
            if (string.IsNullOrEmpty(nodeGuid))
            {
                return false;
            }
            return NodeControls.TryGetValue(nodeGuid, out nodeControl);
        }

        
        /// <summary>
        /// 从Guid获取画布视图
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="nodeControl"></param>
        /// <returns></returns>
        private bool TryGetCanvas(string nodeGuid, out FlowCanvasView flowCanvas)
        {
            flowCanvas = null;
            if (string.IsNullOrEmpty(nodeGuid))
            {
                return false;
            }
            return Canvass.TryGetValue(nodeGuid, out flowCanvas);
        }



        #region 向运行环境发出请求

        /// <summary>
        /// 向运行环境发出请求：添加画布
        /// </summary>
        /// <returns></returns>
        public void CreateFlowCanvas()
        {
            int width = 1200;
            int height = 780;
            _ = Task.Run(async () =>
            {
               var result = await flowEnvironment.CreateCanvasAsync("", width, height);
                Console.WriteLine(result.Guid);
            });
        }

        /// <summary>
        /// 向运行环境发出请求：移除画布
        /// </summary>
        public void RemoveFlowCanvas()
        {
            if (CurrentSelectCanvas is null)
            {
                return;
            }
            var model = ((FlowCanvasViewModel)CurrentSelectCanvas.DataContext).Model;
            _ = flowEnvironment.RemoveCanvasAsync(model.Guid);
        }

        /// <summary>
        /// 向运行环境发出请求：创建节点
        /// </summary>
        public void CreateNode()
        {
            var model = ((FlowCanvasViewModel)CurrentSelectCanvas.DataContext).Model;

            string canvasGuid = model.Guid;
            NodeControlType nodeType = CurrentNodeControlType;
            PositionOfUI? position = CurrentMouseLocation;
            MethodDetailsInfo? methodDetailsInfo = CurrentDragMdInfo;

            if (position is null)
            {
                return;
            }
            _ = flowEnvironment.CreateNodeAsync(canvasGuid, nodeType, position, methodDetailsInfo);
        }

        /// <summary>
        /// 向运行环境发出请求：移除节点
        /// </summary>
        public void RemoteNode(NodeControlBase nodeControl)
        {
            //NodeControlBase? node = CurrentSelectNodeControl;
            if (nodeControl is null)
            {
                return;
            }
            var model = nodeControl.ViewModel.NodeModel;
            if (model is null)
            {
                return;
            }

            _ = flowEnvironment.RemoveNodeAsync(model.CanvasGuid, model.Guid);
        }

        #endregion

       
    }
}
