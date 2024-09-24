using Serein.Library.Utils;
using System;
using System.Threading.Tasks;

namespace Serein.Library.Api
{
    /// <summary>
    /// 流程上下文
    /// </summary>
    public interface IDynamicContext
    {
        /// <summary>
        /// 运行环境
        /// </summary>
        IFlowEnvironment Env { get; }
        
        /// <summary>
        /// 定时循环触发
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="time"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        Task CreateTimingTask(Action callback, int time = 100, int count = -1);
    }
}
