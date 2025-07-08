using Microsoft.Extensions.ObjectPool;
using Newtonsoft.Json.Linq;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow;
using Serein.NodeFlow.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Serein.NodeFlow.Services
{
    /// <summary>
    /// 流程模型服务
    /// </summary>
    public class FlowModelService
    {
        private readonly IFlowEnvironment environment;
        private readonly FlowLibraryService flowLibraryService;

        public FlowModelService(IFlowEnvironment environment, FlowLibraryService flowLibraryService)
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

        public bool TryGetNodeModel(string guid, out IFlowNode flowNode)
        {
            return NodeModels.TryGetValue(guid, out flowNode!);
        }

        public bool TryGetCanvasModel(string guid, out FlowCanvasDetails flowCanvas)
        {
            return FlowCanvass.TryGetValue(guid, out flowCanvas!); ;
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
        public List<IFlowNode> GetAllNodeModel(string canvasGuid) =>
            NodeModels.Values.Where(x => x.CanvasDetails.Guid == canvasGuid).ToList();
        public List<FlowCanvasDetails> GetAllCanvasModel() => [.. FlowCanvass.Values];

        public bool IsExsitCanvas()
        {
            return FlowCanvass.Count > 0;
        }

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