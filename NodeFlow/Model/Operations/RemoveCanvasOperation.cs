using Serein.Library;
using Serein.Library.Api;
using Serein.NodeFlow.Model.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Model.Operations
{
    internal class RemoveCanvasOperation : OperationBase
    {
        public override string Theme => nameof(RemoveCanvasOperation);
        public override bool IsCanUndo => false;
        public required string CanvasGuid { get; set; }

        private FlowCanvasDetailsInfo? flowCanvasDetailsInfo;
        private FlowCanvasDetails? flowCanvasDetails;

        public override bool ValidationParameter()
        {
            var canvasModel = flowModelService.GetCanvasModel(CanvasGuid);
            if (canvasModel is null) return false; // 画布不存在
            var nodeCount = canvasModel.Nodes.Count;
            if (nodeCount > 0)
            {
                SereinEnv.WriteLine(InfoType.WARN, "无法删除具有节点的画布");
                return false;
            }
            this.flowCanvasDetails = canvasModel;
            return true;
        }

        public override async Task<bool> ExecuteAsync()
        {
            if (!ValidationParameter()) return false;

            if (flowCanvasDetails is null)
            {
                // 验证过画布存在，但这时画布不存在了
                // 考虑到多线程操作影响，一般不会进入这个逻辑分支
                var canvasModel = flowModelService.GetCanvasModel(CanvasGuid);
                if (canvasModel is null) return false; // 画布不存在
                flowCanvasDetails = canvasModel;
            }

            flowModelService.RemoveCanvasModel(flowCanvasDetails);
            flowCanvasDetailsInfo = flowCanvasDetails.ToInfo();

            await TriggerEvent(() =>
            {
                flowEnvironmentEvent.OnCanvasRemoved(new CanvasRemoveEventArgs(flowCanvasDetails.Guid));
            });
            return true;
        }

        public override void ToInfo()
        {
            throw new NotImplementedException();
        }

    }
}
