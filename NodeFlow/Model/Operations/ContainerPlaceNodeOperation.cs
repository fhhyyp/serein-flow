using Microsoft.VisualBasic.FileIO;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Model.Operations
{
    /// <summary>
    /// 放置节点操作
    /// </summary>
    internal class ContainerPlaceNodeOperation : OperationBase
    {
        public override string Theme => nameof(ContainerPlaceNodeOperation);

        /// <summary>
        /// 所在画布
        /// </summary>
        public string CanvasGuid { get; set; }

        /// <summary>
        /// 子节点，该数据为此次事件的主节点
        /// </summary>
        public string NodeGuid { get;  set; }
        /// <summary>
        /// 父节点
        /// </summary>
        public string ContainerNodeGuid { get; set; }


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
                return false;
            }
            // 获取目标节点与容器节点
            if (!flowModelService.TryGetNodeModel(NodeGuid, out var nodeModel))
            {
                return false;
            }
            if (!flowModelService.TryGetNodeModel(ContainerNodeGuid, out var containerNode))
            {
                return false;
            }
            if (nodeModel.ContainerNode is INodeContainer tmpContainer)
            {
                //SereinEnv.WriteLine(InfoType.WARN, $"节点放置失败，节点[{nodeGuid}]已经放置于容器节点[{((IFlowNode)tmpContainer).Guid}]");
                return false;
            }
            if(containerNode is not INodeContainer containerNode2)
            {
                return false;
            }

            Node = nodeModel;
            ContainerNode = containerNode2;
            return true;
        }

        public override async Task<bool> ExecuteAsync()
        {
            if (!ValidationParameter()) return false;
            var isSuccess  = ContainerNode.PlaceNode(Node);
            if(isSuccess is true)
            {
                await TriggerEvent(() =>
                {
                    flowEnvironmentEvent.OnNodePlace(new NodePlaceEventArgs(CanvasGuid, NodeGuid, ContainerNodeGuid)); // 通知UI更改节点放置位置
                });
            }
            return isSuccess;
        }

        public override bool Undo()
        {
            var isSuccess = ContainerNode.TakeOutNode(Node);
            if (isSuccess is true)
            {
                _ = TriggerEvent(() =>
                {
                    // 取出节点，重新放置在画布上
                    flowEnvironmentEvent.OnNodeTakeOut(new NodeTakeOutEventArgs(CanvasGuid, ContainerNode.Guid, NodeGuid));
                });
            }
            return isSuccess;
        }


        public override void ToInfo()
        {
        }

      
    }
}
