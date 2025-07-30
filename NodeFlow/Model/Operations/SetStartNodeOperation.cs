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
    /// 设置起始节点
    /// </summary>
    internal class SetStartNodeOperation : OperationBase
    {
        public override string Theme => nameof(SetStartNodeOperation);


        public string CanvasGuid { get; set; }
        public string NewNodeGuid { get; set; }


        private FlowCanvasDetails CanvasModel;
        private IFlowNode NewStartNodeModel;
        private IFlowNode? OldStartNodeModel;


        public override bool ValidationParameter()
        {
            if (!flowModelService.TryGetCanvasModel(CanvasGuid, out CanvasModel)
                || !flowModelService.TryGetNodeModel(NewNodeGuid, out NewStartNodeModel))
            {
                return false;
            }
            return true;
        }
        public override async Task<bool> ExecuteAsync()
        {
            if (!ValidationParameter()) return false;

            if (CanvasModel.StartNode is not null
                && flowModelService.TryGetNodeModel(CanvasModel.StartNode.Guid, out var flowNode)) 
            {
                OldStartNodeModel = flowNode;
            }

            CanvasModel.StartNode = NewStartNodeModel;

            await TriggerEvent(() =>
            {
                flowEnvironmentEvent.OnStartNodeChanged(new StartNodeChangeEventArgs(CanvasModel.Guid, OldStartNodeModel?.Guid, NewStartNodeModel.Guid));
            });
            return true;
        }

        public override bool Undo()
        {
            if(OldStartNodeModel is null)
            {
                return false;
            }
            var newStartNode = OldStartNodeModel;
            var oldStartNode = NewStartNodeModel;

            NewStartNodeModel = newStartNode;
            OldStartNodeModel = oldStartNode;
            CanvasModel.StartNode = oldStartNode;

            flowEnvironmentEvent.OnStartNodeChanged(new StartNodeChangeEventArgs(CanvasModel.Guid, oldStartNode?.Guid, newStartNode.Guid));
            return true;

        }


        public override void ToInfo()
        {
            throw new NotImplementedException();
        }

       
    }
}
