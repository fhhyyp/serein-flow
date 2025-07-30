using Microsoft.CodeAnalysis;
using Serein.Library.Api;
using Serein.NodeFlow.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Model.Operations
{
    internal class ChangeParameterOperation : OperationBase
    {
        public override string Theme => nameof(ChangeParameterOperation);

        public string NodeGuid { get; set; }
        public bool IsAdd{ get; set; }

        public int ParamIndex { get; set; }


        private IFlowNode nodeModel;


        public override bool ValidationParameter()
        {
            if (!flowModelService.TryGetNodeModel(NodeGuid, out var nodeModel))
            {
                return false;
            }
            this.nodeModel = nodeModel;

            var pds = nodeModel.MethodDetails.ParameterDetailss;
            var parameterCount = pds.Length;
            if (ParamIndex >= parameterCount)
            {
                return false; // 需要被添加的下标索引大于入参数组的长度
            }
            if (IsAdd)
            {
                if (pds[ParamIndex].IsParams == false)
                {
                    return false; // 对应的入参并非可选参数中的一部分
                }
            }
            else
            {

            }
            
            return true;
        }


        public override Task<bool> ExecuteAsync()
        {

            if (!ValidationParameter()) return Task.FromResult(false);

            if (IsAdd)
            {
                if (nodeModel.MethodDetails.AddParamsArg(ParamIndex))
                {
                    return Task.FromResult(true);
                }
                else
                {
                    return Task.FromResult(false);
                }
            }
            else
            {
                if (nodeModel.MethodDetails.RemoveParamsArg(ParamIndex))
                {
                    return Task.FromResult(true);
                }
                else
                {
                    return Task.FromResult(true);
                }
            }
        }

        public override void ToInfo()
        {
            throw new NotImplementedException();
        }

        
    }
}
