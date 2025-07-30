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
    internal class CreateCanvasOperation : OperationBase
    {
        public override string Theme => nameof(CreateCanvasOperation);
        public override bool IsCanUndo => false; 

        public required FlowCanvasDetailsInfo CanvasInfo { get; set; }

        

        private FlowCanvasDetails? flowCanvasDetails;

        public override bool ValidationParameter()
        {
            if (CanvasInfo is null) 
                return false; // 没有必须的参数
            if (string.IsNullOrEmpty(CanvasInfo.Guid)) 
                return false; // 不能没有Guid
            if(flowModelService.ContainsCanvasModel(CanvasInfo.Guid)) 
                return false; // 画布已存在
            return true;
        }

        public override async Task<bool> ExecuteAsync()
        {
            if(!ValidationParameter()) return false;

            var cavasnModel = new FlowCanvasDetails(flowEnvironment);
            cavasnModel.LoadInfo(CanvasInfo);
            flowModelService.AddCanvasModel(cavasnModel);
            this.flowCanvasDetails = cavasnModel; ;

            await TriggerEvent(() =>
            {
                flowEnvironmentEvent.OnCanvasCreated(new CanvasCreateEventArgs(cavasnModel));
            });
            return true;
        }

        public override void ToInfo()
        {
            throw new NotImplementedException();
        }

    }
}
