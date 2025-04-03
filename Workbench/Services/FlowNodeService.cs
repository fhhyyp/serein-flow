using Serein.Library;
using Serein.Library.Api;
using Serein.Workbench.Api;
using Serein.Workbench.Node.View;
using Serein.Workbench.Node.ViewModel;
using Serein.Workbench.Views;

namespace Serein.Workbench.Services
{
    /// <summary>
    /// 流程节点管理
    /// </summary>
    public class FlowNodeService
    {
        #region 流程节点操作的相关事件
        public Action<FlowCanvasView> OnCreateFlowCanvasView { get;  set; }
        public Action<string> OnRemoveFlowCanvasView { get;  set; }
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
        public NodeControlType? CurrentNodeControlType { get; set; }


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


        /*
       
*/

        /// <summary>
        /// 记录流程画布
        /// </summary>
        private readonly Dictionary<string, FlowCanvasView> FlowCanvasViews = [];
        /// <summary>
        /// 记录加载的节点
        /// </summary>
        private readonly Dictionary<string, NodeControlViewModelBase> NodeControls = [];

        private readonly IFlowEnvironment flowEnvironment;
        private readonly IFlowEEForwardingService flowEEForwardingService;

        #region 初始化
        public FlowNodeService(IFlowEnvironment flowEnvironment,
                               IFlowEEForwardingService flowEEForwardingService)
        {
            this.flowEnvironment = flowEnvironment;
            this.flowEEForwardingService = flowEEForwardingService;
            InitFlowEvent();
        }

        public void InitFlowEvent()
        {
            flowEEForwardingService.OnCanvasCreate += FlowEEForwardingService_OnCanvasCreate;
            flowEEForwardingService.OnCanvasRemove += FlowEEForwardingService_OnCanvasRemove;
            flowEEForwardingService.OnNodeCreate += FlowEEForwardingService_OnNodeCreate;
            flowEEForwardingService.OnNodeRemove += FlowEEForwardingService_OnNodeRemove;
        }

        private void FlowEEForwardingService_OnNodeRemove(NodeRemoveEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        private void FlowEEForwardingService_OnNodeCreate(NodeCreateEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        private void FlowEEForwardingService_OnCanvasRemove(CanvasRemoveEventArgs eventArgs)
        {
            OnRemoveFlowCanvasView.Invoke(eventArgs.CanvasGuid);
        }

        private void FlowEEForwardingService_OnCanvasCreate(CanvasCreateEventArgs eventArgs)
        {
            var info = eventArgs.Model;
            var model = eventArgs.Model;
            FlowCanvasView canvasView = new FlowCanvasView();
            canvasView.ViewModel.CanvasGuid = info.Guid;
            canvasView.ViewModel.Model = model;
            FlowCanvasViews.Add(info.Guid, canvasView);
            OnCreateFlowCanvasView.Invoke(canvasView); // 传递给订阅者
        }

        #endregion




        #region 向运行环境发出请求

        /// <summary>
        /// 向运行环境发出请求：添加画布
        /// </summary>
        /// <returns></returns>
        public void CreateFlowCanvas()
        {
            int height = 1000;
            int width = 600;
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
            _ = flowEnvironment.RemoveCanvasAsync(CurrentSelectCanvas.ViewModel.CanvasGuid);
        }

        /// <summary>
        /// 向运行环境发出请求：创建节点
        /// </summary>
        public void CreateNode()
        {
            string canvasGuid = CurrentSelectCanvas.ViewModel.CanvasGuid;
            NodeControlType? nodeType = CurrentNodeControlType;
            PositionOfUI? position = CurrentMouseLocation;
            MethodDetailsInfo? methodDetailsInfo = CurrentDragMdInfo;
            if (nodeType is null)
            {
                return;
            }
            if (position is null)
            {
                return;
            }
            _ = flowEnvironment.CreateNodeAsync(canvasGuid, (NodeControlType)nodeType, position, methodDetailsInfo);
        }

        /// <summary>
        /// 向运行环境发出请求：移除节点
        /// </summary>
        public void RemoteNode()
        {
            NodeControlBase? node = CurrentSelectNodeControl;
            if (node is null)
            {
                return;
            }
            var model = node.ViewModel.NodeModel;
            if (model is null)
            {
                return;
            }

            _ = flowEnvironment.RemoveNodeAsync(model.CanvasGuid, model.Guid);
        }

        #endregion

       
    }
}
