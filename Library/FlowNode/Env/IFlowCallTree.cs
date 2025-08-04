using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Serein.Library
{
    /// <summary>
    /// 流程调用树接口，提供获取CallNode的方法。
    /// </summary>
    public interface IFlowCallTree
    {
        /// <summary>
        /// 起始节点
        /// </summary>
        List<CallNode> StartNodes { get; }

        /// <summary>
        /// 全局触发器节点列表
        /// </summary>
        List<CallNode> GlobalFlipflopNodes { get; }

        /// <summary>
        /// 初始化并启动流程调用树，异步执行。
        /// </summary>
        /// <returns></returns>
        Task InitAndStartAsync(CancellationToken token);

        /// <summary>
        /// 获取指定Key的CallNode，如果不存在则返回null。
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        CallNode Get(string key);
    }
}
