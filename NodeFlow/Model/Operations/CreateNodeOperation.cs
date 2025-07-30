using Serein.Library;
using Serein.Library.Api;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Services;
using Serein.NodeFlow.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Model.Operations
{
    internal class CreateNodeOperation : OperationBase
    {
        public override string Theme => nameof(CreateNodeOperation);

        public required string CanvasGuid { get; set; }
        public required NodeControlType NodeControlType { get; set; }
        public required PositionOfUI Position { get; set; }
        public required MethodDetailsInfo? MethodDetailsInfo { get; set; }


        /// <summary>
        /// 是否为基础节点
        /// </summary>
        private bool IsBaseNode => NodeControlType.IsBaseNode();

        /// <summary>
        /// 执行成功后所创建的节点
        /// </summary>
        private IFlowNode? flowNode;

        /// <summary>
        /// 节点所在画布
        /// </summary>
        private FlowCanvasDetails flowCanvasDetails;



        public override bool ValidationParameter()
        {
            // 检查是否存在画布

            var canvasModel = flowModelService.GetCanvasModel(CanvasGuid);
            if(canvasModel is null)  
                return false;

            // 检查类型（防非预期的调用）
            if (NodeControlType == NodeControlType.None)  
                return false;

            // 检查放置位置是否超限（防非预期的调用）
            if (Position.X < 0 || Position.Y < 0 
                || Position.X > canvasModel.Width 
                || Position.Y > canvasModel.Height) 
                return false;

            // 所创建的节点并非基础节点，却没有传入方法信息，将会导致创建失败
            if (!IsBaseNode && MethodDetailsInfo is null) 
                return false;

            // 缓存画布model，提高性能
            this.flowCanvasDetails = canvasModel; 

            return true;
        }

        public override async Task<bool> ExecuteAsync()
        {
            if (!ValidationParameter()) return false; // 执行时验证
            
            IFlowNode? nodeModel;
            if (IsBaseNode)
            {
                nodeModel = FlowNodeExtension.CreateNode(flowEnvironment.IOC, NodeControlType); // 加载基础节点
            }
            else
            {
                if(MethodDetailsInfo is null)
                {
                    return false;
                    //throw new InvalidOperationException($"无法创建节点，因为MethodDetailsInfo属性为null");
                }
                if (!flowLibraryManagement.TryGetMethodDetails(MethodDetailsInfo.AssemblyName,  // 创建节点
                                                              MethodDetailsInfo.MethodName,
                                                              out var methodDetails))
                {
                    return false;
                    //throw new InvalidOperationException($"无法创建节点，因为没有找到{MethodDetailsInfo.AssemblyName}.{MethodDetailsInfo.MethodName}方法，请检查是否已加载对应程序集");
                }
                nodeModel = FlowNodeExtension.CreateNode(flowEnvironment.IOC, NodeControlType, methodDetails); // 一般的加载节点方法
            }

            nodeModel.Guid ??= Guid.NewGuid().ToString();
            nodeModel.Position = Position; // 设置位置

            // 节点与画布互相绑定
            nodeModel.CanvasDetails = flowCanvasDetails;
            flowCanvasDetails.Nodes = [..flowCanvasDetails.Nodes, nodeModel]; 

            flowModelService.AddNodeModel(nodeModel);
            this.flowNode = nodeModel;

            await TriggerEvent(() =>
            {
                flowEnvironmentEvent.OnNodeCreated(new NodeCreateEventArgs(flowCanvasDetails.Guid, nodeModel, Position));
            });
            return true;
        }


        public override bool Undo()
        {
            if (!ValidationParameter()) return false; // 撤销时验证
            if(flowNode is null) return false; // 没有创建过节点
            var canvasGuid = flowCanvasDetails.Guid;
            var nodeGuid = flowNode.Guid;
            flowEnvironment.FlowEdit.RemoveNode(canvasGuid, nodeGuid);
            return true;
        }


        public override void ToInfo()
        {
            throw new NotImplementedException();
        }


        /*private bool TryAddNode(IFlowNode nodeModel)
        {
            nodeModel.Guid ??= Guid.NewGuid().ToString();
            NodeModels.TryAdd(nodeModel.Guid, nodeModel);


            // 如果是触发器，则需要添加到专属集合中
            if (nodeModel is SingleFlipflopNode flipflopNode)
            {
                var guid = flipflopNode.Guid;
                if (!FlipflopNodes.Exists(it => it.Guid.Equals(guid)))
                {
                    FlipflopNodes.Add(flipflopNode);
                }
            }
            return true;
        }*/
    }
}
