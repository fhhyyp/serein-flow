using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serein.Library;
using Serein.Library.Api;
using Serein.Workbench.Api;
using Serein.Workbench.Node.View;
using Serein.Workbench.Node.ViewModel;
using Serein.Workbench.ViewModels;
using Serein.Workbench.Views;
using System.Text;
using Serein.NodeFlow.Model.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Serein.Workbench.Services
{
    /// <summary>
    /// 流程节点管理
    /// </summary>
    internal class FlowNodeService 
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
        /// 查看的画布发生改变
        /// </summary>
        public Action<FlowCanvasView> OnViewCanvasChanged{ get; set; }

        /// <summary>
        /// 查看的节点发生变化
        /// </summary>
        public Action<NodeControlBase> OnViewNodeControlChanged{ get; set; }

        /// <summary>
        /// 查看方法发生变化
        /// </summary>
        public Action<MethodDetailsInfo> OnViewMethodDetailsInfoChanged { get; set; }

        /// <summary>
        /// FlowCanvasView 监听，需要移除连接线（控件）
        /// </summary>
        public Action<NodeConnectChangeEventArgs> OnRemoveConnectionLine { get; set; }

        #endregion

        #region 创建节点相关的属性


        private FlowCanvasView currentSelectCanvas;
        /// <summary>
        /// 当前查看的画布
        /// </summary>
        public FlowCanvasView CurrentSelectCanvas { get => currentSelectCanvas; set
            {
                if (value == null || value.Equals(currentSelectCanvas))
                {
                    return;
                }
                currentSelectCanvas = value;
                OnViewCanvasChanged?.Invoke(value);
            }
        }


        private NodeControlBase? currentSelectNodeControl;

        /// <summary>
        /// 当前选中的节点
        /// </summary>
        public NodeControlBase? CurrentSelectNodeControl { get => currentSelectNodeControl; set
            {
                if (value == null || value.Equals(currentSelectNodeControl))
                {
                    return;
                }
                currentSelectNodeControl = value;
                OnViewNodeControlChanged?.Invoke(value);
            }
        }

        private MethodDetailsInfo? currentMethodDetailsInfo;

        /// <summary>
        /// 当前拖动的方法信息
        /// </summary>
        public MethodDetailsInfo? CurrentMethodDetailsInfo { get => currentMethodDetailsInfo; set
            {
                if (value == null || value.Equals(currentMethodDetailsInfo))
                {
                    return;
                }
                currentMethodDetailsInfo = value;
                OnViewMethodDetailsInfoChanged?.Invoke(value);
            }
        }


        /// <summary>
        /// 当前需要创建的节点类型
        /// </summary>
        public NodeControlType CurrentNodeControlType { get; set; } = NodeControlType.None;

        /// <summary>
        /// 当前鼠标位置
        /// </summary>
        public PositionOfUI? CurrentMouseLocation { get; set; }


        /// <summary>
        /// 连接数据
        /// </summary>
        internal ConnectingData ConnectingData { get; } = new ConnectingData();

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
        /// 当前所有画布
        /// </summary>
        public FlowCanvasView[] FlowCanvass => Canvass.Select(c => c.Value).ToArray();
        public NodeControlBase[] FlowNodeControls => NodeControls.Select(c => c.Value).ToArray();

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
           flowEnvironment.FlowEdit.NodeMVVMManagement.RegisterUI(NodeControlType.UI, typeof(UINodeControl), typeof(UINodeControlViewModel));
           flowEnvironment.FlowEdit.NodeMVVMManagement.RegisterUI(NodeControlType.Action, typeof(ActionNodeControl), typeof(ActionNodeControlViewModel));
           flowEnvironment.FlowEdit.NodeMVVMManagement.RegisterUI(NodeControlType.Flipflop, typeof(FlipflopNodeControl), typeof(FlipflopNodeControlViewModel));
           flowEnvironment.FlowEdit.NodeMVVMManagement.RegisterUI(NodeControlType.ExpOp, typeof(ExpOpNodeControl), typeof(ExpOpNodeControlViewModel));
           flowEnvironment.FlowEdit.NodeMVVMManagement.RegisterUI(NodeControlType.ExpCondition, typeof(ConditionNodeControl), typeof(ConditionNodeControlViewModel));
           flowEnvironment.FlowEdit.NodeMVVMManagement.RegisterUI(NodeControlType.GlobalData, typeof(GlobalDataControl), typeof(GlobalDataNodeControlViewModel));
           flowEnvironment.FlowEdit.NodeMVVMManagement.RegisterUI(NodeControlType.Script, typeof(ScriptNodeControl), typeof(ScriptNodeControlViewModel));
           flowEnvironment.FlowEdit.NodeMVVMManagement.RegisterUI(NodeControlType.NetScript, typeof(NetScriptNodeControl), typeof(NetScriptNodeControlViewModel));
           flowEnvironment.FlowEdit.NodeMVVMManagement.RegisterUI(NodeControlType.FlowCall, typeof(FlowCallNodeControl), typeof(FlowCallNodeControlViewModel));
        }

        /// <summary>
        /// 注册节点事件
        /// </summary>
        private void InitFlowEvent()
        {
            flowEEForwardingService.CanvasCreated += FlowEEForwardingService_OnCanvasCreate;  // 创建了画布
            flowEEForwardingService.CanvasRemoved += FlowEEForwardingService_OnCanvasRemove; // 移除了画布
            flowEEForwardingService.NodeCreated += FlowEEForwardingService_OnNodeCreate; // 创建了节点
            flowEEForwardingService.NodeRemoved += FlowEEForwardingService_OnNodeRemove; // 移除了节点

            flowEEForwardingService.NodePlace += FlowEEForwardingService_OnNodePlace; // 节点放置在容器中
            flowEEForwardingService.NodeTakeOut += FlowEEForwardingService_OnNodeTakeOut; ; // 节点从容器中取出

            flowEEForwardingService.NodeConnectChanged += FlowEEForwardingService_OnNodeConnectChange; // 节点连接状态改变事件

            flowEEForwardingService.StartNodeChanged += FlowEEForwardingService_OnStartNodeChange; // 画布起始节点改变


        }

        private void FlowEEForwardingService_OnStartNodeChange(StartNodeChangeEventArgs eventArgs)
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

            /*if(e.ChangeType == NodeConnectChangeEventArgs.ConnectChangeType.Remove)
            {
                OnRemoveConnectionLine.Invoke(e);  // 删除连线
            }*/
            action?.Invoke();
            return;

        }

        private void FlowEEForwardingService_OnNodeTakeOut(NodeTakeOutEventArgs eventArgs)
        {
            string nodeGuid = eventArgs.NodeGuid;
            string containerNodeGuid = eventArgs.ContainerNodeGuid;
            if (!TryGetControl(containerNodeGuid, out var containerNodeControl) || !TryGetControl(nodeGuid, out var nodeControl))
            {
                return;
            }
            nodeControl.TakeOutContainer(); // 从容器节点中取出
            (double x, double y) = (Canvas.GetLeft(containerNodeControl), Canvas.GetRight(containerNodeControl));
            Canvas.SetLeft(nodeControl, x + 400);
            Canvas.SetRight(nodeControl, y +  200);
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

            if (!flowEnvironment.FlowEdit.NodeMVVMManagement.TryGetType(nodeModel.ControlType, out var nodeMVVM))
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

                if(nodeCanvas is IFlowCanvas flowCanvas)
                {
                    flowCanvas.Add(nodeControl);  // 创建节点
                }
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
            Canvass.Remove(eventArgs.CanvasGuid);
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
        private static NodeControlBase CreateNodeControl(Type controlType, Type viewModelType, IFlowNode model, IFlowCanvas nodeCanvas)
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
        /// <param name="flowCanvas"></param>
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

        #region 节点的复制与粘贴
        /// <summary>
        /// 从节点信息转换为Json文本数据
        /// </summary>
        public string CpoyNodeInfo(List<IFlowNode> dictSelection)
        {

            // 遍历当前已选节点
            foreach (var node in dictSelection.ToArray())
            {
                if (node.ChildrenNode.Count == 0)
                {
                    continue;
                }
                // 遍历这些节点的子节点，添加过来
                foreach (var childNode in node.ChildrenNode)
                {
                    dictSelection.Add(childNode);
                }
            }

            var nodeInfos = dictSelection.Select(item => item.ToInfo());

            JObject json = new JObject()
            {
                ["nodes"] = JArray.FromObject(nodeInfos)
            };

            var jsonText = json.ToString();


            try
            {
                SereinEnv.WriteLine(InfoType.INFO, $"复制已选节点（{dictSelection.Count}个）");
                return jsonText;
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(InfoType.ERROR, $"复制失败：{ex.Message}");
                return string.Empty;
            }
        }


        /// <summary>
        /// 从Json中加载节点
        /// </summary>
        /// <param name="canvasGuid">需要加载在哪个画布上</param>
        /// <param name="jsonText">文本内容</param>
        /// <param name="positionOfUI">需要加载的位置</param>
        public void PasteNodeInfo(string canvasGuid, string jsonText, PositionOfUI positionOfUI)
        {
            try
            {
                List<NodeInfo>? nodes = JsonConvert.DeserializeObject<List<NodeInfo>>(jsonText);
                if (nodes is not null && nodes.Count != 0)
                {

                }
                if (nodes is null || nodes.Count < 0)
                {
                    return;
                }

                #region 节点去重
                Dictionary<string, string> guids = new Dictionary<string, string>(); // 记录 Guid
                
                // 遍历当前节点
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

                // Guid去重
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

                // 获取第一个节点的原始位置
                var index0NodeX = nodes[0].Position.X;
                var index0NodeY = nodes[0].Position.Y;

                // 计算所有节点相对于第一个节点的偏移量
                foreach (var node in nodes)
                {
                    node.CanvasGuid = canvasGuid; // 替换画布Guid

                    var offsetX = node.Position.X - index0NodeX;
                    var offsetY = node.Position.Y - index0NodeY;

                    // 根据鼠标位置平移节点
                    node.Position = new PositionOfUI(positionOfUI.X + offsetX, positionOfUI.Y + offsetY);
                }

                _ = flowEnvironment.FlowEdit.LoadNodeInfosAsync(nodes);
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(InfoType.ERROR, $"粘贴节点时发生异常：{ex}");
            }
            // SereinEnv.WriteLine(InfoType.INFO, $"剪贴板文本内容: {clipboardText}");
        }
        #endregion

        #region 向运行环境发出请求

        /// <summary>
        /// 向运行环境发出请求：添加画布
        /// </summary>
        /// <returns></returns>
        public void CreateFlowCanvas()
        {
            int width = 1200;
            int height = 780;
            flowEnvironment.FlowEdit.CreateCanvas("", width, height);
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
           flowEnvironment.FlowEdit.RemoveCanvas(model.Guid);
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
            MethodDetailsInfo? methodDetailsInfo = CurrentMethodDetailsInfo;

            if (position is null)
            {
                return;
            }
            flowEnvironment.FlowEdit.CreateNode(canvasGuid, nodeType, position, methodDetailsInfo);
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

           flowEnvironment.FlowEdit.RemoveNode(model.CanvasDetails.Guid, model.Guid);
        }

        #endregion

       
    }
}
