using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Api
{
    /// <summary>
    /// 脚本代码中关于流程运行的API
    /// </summary>
    public interface IScriptFlowApi
    {
        /// <summary>
        /// 当前流程运行环境
        /// </summary>
        IFlowEnvironment Env { get; }
        /// <summary>
        /// 对应的节点
        /// </summary>
        NodeModelBase NodeModel { get; }

        /// <summary>
        /// 根据索引从入参数据获取数据 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        object GetArgData(IDynamicContext context, int index);
        /// <summary>
        /// 获取流程当前传递的数据
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        object GetFlowData(IDynamicContext context);

        /// <summary>
        /// 获取全局数据
        /// </summary>
        /// <param name="keyName"></param>
        /// <returns></returns>
        object GetGlobalData(string keyName);


        /// <summary>
        /// 立即调用某个节点并获取其返回值
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <returns></returns>
        Task<object> CallNode(string nodeGuid);
    }
}
