using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
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
        public IFlowNode NodeModel { get; private set; }

      

        /// <summary>
        /// 创建流程脚本接口
        /// </summary>
        /// <param name="environment">运行环境</param>
        /// <param name="nodeModel">节点</param>
        public ScriptFlowApi(IFlowEnvironment environment, IFlowNode nodeModel)
        {
            Env = environment;
            NodeModel = nodeModel;
        }

        public Task<object> CallNode(string nodeGuid)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 获取全局数据
        /// </summary>
        /// <param name="keyName"></param>
        /// <returns></returns>
        public object? GetGlobalData(string keyName)
        {
            return SereinEnv.GetFlowGlobalData(keyName);
        }
    }


}
