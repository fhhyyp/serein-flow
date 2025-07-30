using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Services
{
    /// <summary>
    /// 流程API服务，用于外部调用流程接口
    /// </summary>
    internal class FlowApiService
    {
        private readonly IFlowEnvironment flowEnvironment;
        private readonly FlowModelService flowModelService;

        /// <summary>
        /// 流程API服务构造函数
        /// </summary>
        /// <param name="flowEnvironment"></param>
        /// <param name="flowModelService"></param>
        public FlowApiService(IFlowEnvironment flowEnvironment,
                              FlowModelService  flowModelService)
        {
            this.flowEnvironment = flowEnvironment;
            this.flowModelService = flowModelService;
        }





    }
}
