using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library
{
    /// <summary>
    /// 脚本代码中关于流程运行的API
    /// </summary>
    public class ScriptFlowApi : IScriptFlowApi
    {
        /// <summary>
        /// 流程环境
        /// </summary>
        public IFlowEnvironment Env { get; private set; }

        /// <summary>
        /// 创建流程脚本接口
        /// </summary>
        /// <param name="environment"></param>
        public ScriptFlowApi(IFlowEnvironment environment)
        {
            Env = environment;
        }

        Task<object> IScriptFlowApi.CallNode(string nodeGuid)
        {
            throw new NotImplementedException();
        }

        object IScriptFlowApi.GetDataOfParams(int index)
        {
            throw new NotImplementedException();
        }

        object IScriptFlowApi.GetDataOfParams(string name)
        {
            throw new NotImplementedException();
        }

        object IScriptFlowApi.GetFlowData()
        {
            throw new NotImplementedException();
        }

        object IScriptFlowApi.GetGlobalData(string keyName)
        {
            throw new NotImplementedException();
        }
    }


}
