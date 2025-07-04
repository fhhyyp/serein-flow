using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.CodeAnalysis.CSharp.SyntaxTokenParser;

namespace Serein.NodeFlow.Services
{
    /// <summary>
    /// 流程API服务，用于外部调用流程接口
    /// </summary>
    public class FlowApiService
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



        /*    object result = flowApiService.Invoke("", params);
    TResult result = flowApiService.Invoke<TResult>("", params);
    object result = await flowApiService.InvokeAsync("", params);
    TResult result = await flowApiService.InvokeAsync<TResult>("", params);*/


        public object Invoke(string apiName, object[] param)
        {
            return null;
        }

        public TResult Invoke<TResult>(string apiName, object[] param)
        {
            return default(TResult);
        }

        public async Task<object> InvokeAsync(string apiName, object[] param)
        {
            return null;
        }

        public async Task<TResult> InvokeAsync<TResult>(string apiName, object[] param)
        {
            return default(TResult);

        }


    }
}
