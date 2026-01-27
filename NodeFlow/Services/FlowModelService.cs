using Serein.Library;
using Serein.Library.Api;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Model.Nodes;
using System.Diagnostics.CodeAnalysis;

namespace Serein.NodeFlow.Services
{
    /// <summary>
    /// 流程画布/节点数据实体服务
    /// </summary>
    public class FlowModelService
    {
        private readonly IFlowEnvironment environment;
        private readonly IFlowLibraryService flowLibraryService;

        /// <summary>
        /// 流程模型服务构造函数
        /// </summary>
        /// <param name="environment"></param>
        /// <param name="flowLibraryService"></param>
        public FlowModelService(IFlowEnvironment environment, IFlowLibraryService flowLibraryService)
        {
            this.environment = environment;
            this.flowLibraryService = flowLibraryService;
        }

        /// <summary>
        /// 环境加载的节点集合
        /// Node Guid - Node Model
        /// </summary>
        private Dictionary<string, IFlowNode> NodeModels { get; } = [];

        /// <summary>
        /// 运行环境加载的画布集合
        /// </summary>
        private Dictionary<string, FlowCanvasDetails> FlowCanvass { get; } = [];

        /// <summary>
        /// 存放触发器节点（运行时全部调用）
        /// </summary>
        private List<SingleFlipflopNode> FlipflopNodes { get; } = [];

        /// <summary>
        /// 获取节点模型
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public IFlowNode? GetNodeModel(string guid)
        {
            NodeModels.TryGetValue(guid, out var nodeModel);
            return nodeModel;
        }

        /// <summary>
        /// 获取画布模型
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public FlowCanvasDetails? GetCanvasModel(string guid)
        {
            FlowCanvass.TryGetValue(guid, out var nodeModel);
            return nodeModel;
        }

        /// <summary>
        /// 尝试获取节点模型
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="flowNode"></param>
        /// <returns></returns>
        public bool TryGetNodeModel(string guid, [NotNullWhen(true)] out IFlowNode? flowNode)
        {
            return NodeModels.TryGetValue(guid, out flowNode!);
        }

        /// <summary>
        /// 尝试获取画布模型
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="flowCanvas"></param>
        /// <returns></returns>
        public bool TryGetCanvasModel(string guid, [NotNullWhen(true)] out FlowCanvasDetails? flowCanvas)
        {
            if(FlowCanvass.TryGetValue(guid, out var details))
            {
                flowCanvas = details;
                return true;
            }
            flowCanvas = details;
            return false;
        }

        /// <summary>
        /// 检查是否包含节点模型
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public bool ContainsNodeModel(string guid)
        {
            return NodeModels.ContainsKey(guid);
        }

        /// <summary>
        /// 检查是否包含画布模型
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public bool ContainsCanvasModel(string guid)
        {
            return FlowCanvass.ContainsKey(guid);
        }

        /// <summary>
        /// 添加节点模型
        /// </summary>
        /// <param name="flowNode"></param>
        /// <returns></returns>
        public bool AddNodeModel(IFlowNode flowNode)
        {
            ArgumentNullException.ThrowIfNull(flowNode);
            ArgumentNullException.ThrowIfNull(flowNode.Guid);
            return NodeModels.TryAdd(flowNode.Guid, flowNode);
        }

        /// <summary>
        /// 添加画布模型
        /// </summary>
        /// <param name="flowCanvasDetails"></param>
        /// <returns></returns>
        public bool AddCanvasModel(FlowCanvasDetails flowCanvasDetails)
        {
            ArgumentNullException.ThrowIfNull(flowCanvasDetails);
            ArgumentNullException.ThrowIfNull(flowCanvasDetails.Guid);
            return FlowCanvass.TryAdd(flowCanvasDetails.Guid, flowCanvasDetails);
        }

        /// <summary>
        /// 移除节点模型
        /// </summary>
        /// <param name="flowNode"></param>
        /// <returns></returns>
        public bool RemoveNodeModel(IFlowNode flowNode)
        {
            ArgumentNullException.ThrowIfNull(flowNode.Guid);
            return NodeModels.Remove(flowNode.Guid);
        }

        /// <summary>
        /// 移除画布模型
        /// </summary>
        /// <param name="flowCanvasDetails"></param>
        /// <returns></returns>
        public bool RemoveCanvasModel(FlowCanvasDetails flowCanvasDetails)
        {
            ArgumentNullException.ThrowIfNull(flowCanvasDetails.Guid);
            return FlowCanvass.Remove(flowCanvasDetails.Guid);
        }

        /// <summary>
        /// 获取所有节点模型
        /// </summary>
        /// <returns></returns>
        public List<IFlowNode> GetAllNodeModel() => [.. NodeModels.Values];

        /// <summary>
        /// 获取指定画布上的所有节点模型
        /// </summary>
        /// <param name="canvasGuid"></param>
        /// <returns></returns>
        public List<IFlowNode> GetAllNodeModel(string canvasGuid) =>
            NodeModels.Values.Where(x => x.CanvasDetails.Guid == canvasGuid).ToList();

        /// <summary>
        /// 获取所有画布模型
        /// </summary>
        /// <returns></returns>
        public List<FlowCanvasDetails> GetAllCanvasModel() => [.. FlowCanvass.Values];

        /// <summary>
        /// 检查是否存在画布模型
        /// </summary>
        /// <returns></returns>
        public bool IsExsitCanvas()
        {
            return FlowCanvass.Count > 0;
        }

        /// <summary>
        /// 检查指定画布上是否存在节点模型
        /// </summary>
        /// <param name="canvasGuid"></param>
        /// <returns></returns>
        public bool IsExsitNodeOnCanvas(string canvasGuid)
        {
            if (!FlowCanvass.TryGetValue(canvasGuid, out var flowCanvasDetails))
            {
                return false;
            }
            return flowCanvasDetails.Nodes.Count > 0;
        }

    }
}



/*        /// <summary>
        /// 生成方法名称
        /// </summary>
        /// <param name="flowNode"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private string GetNodeMethodName(IFlowNode flowNode)
        {
            return $"FlowMethod_{flowNode.Guid.Remove('-')}";

            if (flowNode.ControlType == NodeControlType.Action)
            {
                if (!flowLibraryService.TryGetMethodInfo(flowNode.MethodDetails.AssemblyName,
                                                           flowNode.MethodDetails.MethodName,
                                                           out var methodInfo))
                {
                    throw new Exception();
                }
                return $"FlowMethod_{nameof(NodeControlType.Action)}_{methodInfo.Name}";
            }
            else if (flowNode.ControlType == NodeControlType.Flipflop)
            {
                if (!flowLibraryService.TryGetMethodInfo(flowNode.MethodDetails.AssemblyName,
                                                           flowNode.MethodDetails.MethodName,
                                                           out var methodInfo))
                {
                    throw new Exception();
                }
                return $"FlowMethod_{nameof(NodeControlType.Flipflop)}_{methodInfo.Name}";

            }
            else if (flowNode.ControlType == NodeControlType.Script)
            {
                return $"FlowMethod_{flowNode.Guid.Remove('-')}";
            } 
            else if (flowNode.ControlType == NodeControlType.UI)
            {

            } 
            else if (flowNode.ControlType == NodeControlType.ExpCondition)
            {

            } 
            else if (flowNode.ControlType == NodeControlType.ExpOp)
            {

            }
            else 
            {
                throw new Exception("无法为该节点生成方法名称");
            }
        }*/