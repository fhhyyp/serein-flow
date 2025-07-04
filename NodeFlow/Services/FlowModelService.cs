using Serein.Library;
using Serein.Library.Api;
using Serein.NodeFlow.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Services
{
    internal class FlowModelService
    {
        private readonly IFlowEnvironment environment;

        public FlowModelService(IFlowEnvironment environment)
        {
            this.environment = environment;
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

        public IFlowNode? GetNodeModel(string guid)
        {
            NodeModels.TryGetValue(guid, out var nodeModel);
            return nodeModel;
        }
        
        public FlowCanvasDetails? GetCanvasModel(string guid)
        {
            FlowCanvass.TryGetValue(guid, out var nodeModel);
            return nodeModel;
        }

        public bool TryGetNodeModel(string guid,out IFlowNode flowNode)
        {
            return NodeModels.TryGetValue(guid, out flowNode!);
        }

        public bool TryGetCanvasModel(string guid,out FlowCanvasDetails flowCanvas)
        {
            return FlowCanvass.TryGetValue(guid, out flowCanvas!);;
        }


        public bool ContainsNodeModel(string guid)
        {
            return NodeModels.ContainsKey(guid);
        }
        
        public bool ContainsCanvasModel(string guid)
        {
            return FlowCanvass.ContainsKey(guid);
        }

        public bool AddNodeModel(IFlowNode flowNode)
        {
            ArgumentNullException.ThrowIfNull(flowNode);
            ArgumentNullException.ThrowIfNull(flowNode.Guid);
            return NodeModels.TryAdd(flowNode.Guid, flowNode);
        }
        public bool AddCanvasModel(FlowCanvasDetails flowCanvasDetails)
        {
            ArgumentNullException.ThrowIfNull(flowCanvasDetails);
            ArgumentNullException.ThrowIfNull(flowCanvasDetails.Guid);
            return FlowCanvass.TryAdd(flowCanvasDetails.Guid, flowCanvasDetails);
        }
        public bool RemoveNodeModel(IFlowNode flowNode)
        {
            ArgumentNullException.ThrowIfNull(flowNode.Guid);
            return NodeModels.Remove(flowNode.Guid);
        }
        public bool RemoveCanvasModel(FlowCanvasDetails flowCanvasDetails)
        {
            ArgumentNullException.ThrowIfNull(flowCanvasDetails.Guid);
            return FlowCanvass.Remove(flowCanvasDetails.Guid);
        }

        public List<IFlowNode> GetAllNodeModel() => [.. NodeModels.Values];
        public List<FlowCanvasDetails> GetAllCanvasModel() => [.. FlowCanvass.Values];



    }
}
