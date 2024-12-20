using Serein.Library;
using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow
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
        /// 对应的节点
        /// </summary>
        public NodeModelBase NodeModel { get; private set; }

        IDynamicContext IScriptFlowApi.Context { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <summary>
        /// 创建流程脚本接口
        /// </summary>
        /// <param name="environment">运行环境</param>
        /// <param name="nodeModel">节点</param>
        public ScriptFlowApi(IFlowEnvironment environment, NodeModelBase nodeModel)
        {
            Env = environment;
            NodeModel = nodeModel;
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
