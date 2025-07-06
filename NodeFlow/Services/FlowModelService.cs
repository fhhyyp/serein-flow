using Serein.Library;
using Serein.Library.Api;
using Serein.NodeFlow.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Serein.NodeFlow.Services
{
    /// <summary>
    /// 流程模型服务
    /// </summary>
    public class FlowModelService
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


        public void ToCsharpCoreFile()
        {
            // TODO: 实现将流程模型转换为C# Core文件的逻辑
            // 遍历每个画布
            int canvas_index = 0;

            HashSet<Type> assemblyFlowClasss = new HashSet<Type>(); // 用于创建依赖注入项
            StringBuilder stringBuilder = new StringBuilder();

            foreach (var canvas in FlowCanvass.Values)
            {
                int flowTemplateId = canvas_index++;
                string flowTemplateClassName = $"FlowTemplate{flowTemplateId}";

                HashSet<Type> flowClasss = new HashSet<Type>();

                // 收集程序集信息
                foreach (var node in canvas.Nodes)
                {
                    var instanceType = node.MethodDetails.ActingInstanceType;
                    if(instanceType is not null) 
                    { 
                        flowClasss.Add(instanceType); 
                        assemblyFlowClasss.Add(instanceType); 
                    }
                }

                // 生成方法信息
                foreach (var node in canvas.Nodes)
                {
                    var instanceType = node.MethodDetails.ActingInstanceType;
                    var returnType = node.MethodDetails.ReturnType;
                    var methodName = node.MethodDetails.MethodAnotherName;
                    


                    var instanceTypeFullName = instanceType.FullName;
                    var returnTypeFullName = returnType == typeof(void) ? "void" : returnType.FullName;
                    
                    string methodContext = $"private {returnTypeFullName} NodeMethod_{methodName}({nameof(IDynamicContext)} context)";
                    SereinEnv.WriteLine(InfoType.INFO, methodContext);
                }
            }
        }



    }
}
