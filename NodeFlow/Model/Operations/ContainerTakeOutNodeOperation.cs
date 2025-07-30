using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Model.Operations
{

    /// <summary>
    /// 取出节点操作
    /// </summary>
    internal class ContainerTakeOutNodeOperation : OperationBase
    {
        public override string Theme => nameof(ContainerTakeOutNodeOperation);

        /// <summary>
        /// 所在画布
        /// </summary>
        public string CanvasGuid { get; set; }

        /// <summary>
        /// 子节点，该数据为此次事件的主节点
        /// </summary>
        public string NodeGuid { get; set; }


        /// <summary>
        /// 父节点
        /// </summary>
        private INodeContainer ContainerNode;

        /// <summary>
        /// 子节点，该数据为此次事件的主节点
        /// </summary>
        private IFlowNode Node;



        public override bool ValidationParameter()
        {
            if (!flowModelService.ContainsCanvasModel(CanvasGuid))
            {
                flowEnvironment.WriteLine(Serein.Library.InfoType.WARN, $"节点取出失败，目标画布不存在[{NodeGuid}]");
                return false;
            }
            // 获取目标节点与容器节点
            if (!flowModelService.TryGetNodeModel(NodeGuid, out var nodeModel))
            {
                flowEnvironment.WriteLine(Serein.Library.InfoType.WARN, $"节点取出失败，目标节点不存在[{NodeGuid}]");
                return false;
            }
            if (nodeModel.ContainerNode is not INodeContainer containerNode)
            {
                flowEnvironment.WriteLine(Serein.Library.InfoType.WARN, $"节点取出失败，节点并非容器节点[{nodeModel.Guid}]");
                return false;
            }
            Node = nodeModel;
            ContainerNode = containerNode;
            return true;
        }

        public override async Task<bool> ExecuteAsync()
        {
            if (!ValidationParameter()) return false;

            var isSuccess = ContainerNode.TakeOutNode(Node);
            if (isSuccess is true)
            {
                await TriggerEvent(() =>
                {
                    // 取出节点，重新放置在画布上
                    flowEnvironmentEvent.OnNodeTakeOut(new NodeTakeOutEventArgs(CanvasGuid, ContainerNode.Guid, NodeGuid));
                });
            }
            return isSuccess;
        }

        public override bool Undo()
        {
            var isSuccess = ContainerNode.PlaceNode(Node);
            if (isSuccess is true)
            {
                if (ContainerNode is IFlowNode containerFlowNode)
                {
                    flowEnvironmentEvent.OnNodePlace(new NodePlaceEventArgs(CanvasGuid, NodeGuid, containerFlowNode.Guid)); // 通知UI更改节点放置位置
                }
            }
            return isSuccess;
        }


        public override void ToInfo()
        {
        }
    }
}
