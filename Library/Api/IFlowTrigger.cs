using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Api
{
    /// <summary>
    /// 触发器接口
    /// </summary>
    /// <typeparam name="TSignal"></typeparam>
    public interface IFlowTrigger<TSignal>
    {
        /// <summary>
        /// 等待信号触发并指定超时时间
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="signal"></param>
        /// <param name="outTime"></param>
        /// <returns></returns>
        Task<TriggerResult<TResult>> WaitTriggerWithTimeoutAsync<TResult>(TSignal signal, TimeSpan outTime);
        /// <summary>
        /// 等待信号触发
        /// </summary>
        /// <typeparam name="TResult">预期的返回值类型</typeparam>
        /// <param name="signal"></param>
        /// <returns></returns>
        Task<TriggerResult<TResult>> WaitTriggerAsync<TResult>(TSignal signal);
        /// <summary>
        /// 调用触发器
        /// </summary>
        /// <typeparam name="TResult">预期的返回值类型</typeparam>
        /// <param name="signal">信号</param>
        /// <param name="value">返回值</param>
        /// <returns></returns>
        Task<bool> InvokeTriggerAsync<TResult>(TSignal signal, TResult value);
        /// <summary>
        /// 取消所有触发器
        /// </summary>
        void CancelAllTrigger();
    }

}
