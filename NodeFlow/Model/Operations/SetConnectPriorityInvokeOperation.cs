using Serein.Library;
using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Model.Operations
{

    /// <summary>
    /// 将调用顺序置为优先
    /// </summary>
    internal class SetConnectPriorityInvokeOperation : OperationBase
    {
        public override string Theme => nameof(SetConnectPriorityInvokeOperation);

        public string FromNodeGuid { get; set; }
        public string ToNodeGuid { get; set; }
        public ConnectionInvokeType ConnectionType { get; set; }  

        private IFlowNode FromNode;
        private IFlowNode ToNode;
        private int lastIdx = -1;

        public override bool ValidationParameter()
        {
            if (ConnectionType == ConnectionInvokeType.None) 
            { 
                return false;
            }
            // 获取起始节点与目标节点
            if (!flowModelService.TryGetNodeModel(FromNodeGuid, out var fromNode) || !flowModelService.TryGetNodeModel(ToNodeGuid, out var toNode))
            {
                return false;
            }
            if (fromNode is null || toNode is null) return false;

            FromNode = fromNode;
            ToNode = toNode;
            return true;

        }

        /// <summary>
        /// 成为首项
        /// </summary>
        public override Task<bool> ExecuteAsync()
        {
            if(!ValidationParameter()) return Task.FromResult(false);

            if (FromNode.SuccessorNodes.TryGetValue(ConnectionType, out var nodes))
            {
                var idx = nodes.IndexOf(ToNode);
                if (idx > -1)
                {
                    lastIdx = idx;
                    nodes.RemoveAt(idx);
                    nodes.Insert(0, ToNode);
                }
            }
            return Task.FromResult(true);
        }

        /// <summary>
        /// 恢复原来的位置
        /// </summary>
        public override bool Undo()
        {
            if (FromNode.SuccessorNodes.TryGetValue(ConnectionType, out var nodes))
            {
                var idx = nodes.IndexOf(ToNode);
                if (idx > -1)
                {
                    nodes.RemoveAt(idx);
                    nodes.Insert(lastIdx, ToNode);
                    lastIdx = 0;
                }
            }
            return true;
        }

        public override void ToInfo()
        {
            throw new NotImplementedException();
        }

       
    }
}
