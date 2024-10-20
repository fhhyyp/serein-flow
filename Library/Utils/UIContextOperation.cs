using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{
    /// <summary>
    /// 为类库提供了在UI线程上下文操作的方法
    /// </summary>
    public class UIContextOperation
    {
        private readonly SynchronizationContext context;

        /// <summary>
        /// 传入UI线程上下文
        /// </summary>
        /// <param name="synchronizationContext">线程上下文</param>
        public UIContextOperation(SynchronizationContext synchronizationContext)
        {
            this.context = synchronizationContext;
        }

        /// <summary>
        /// 同步方式进行调用方法
        /// </summary>
        /// <param name="uiAction">要执行的UI操作</param>
        public void Invoke(Action uiAction)
        {
            context?.Post(state =>
            {
                uiAction?.Invoke();
            }, null);
        }

        /// <summary>
        /// 异步方式进行调用
        /// </summary>
        /// <param name="uiAction">要执行的UI操作</param>
        /// <returns></returns>
        public Task InvokeAsync(Action uiAction)
        {
            var tcs = new TaskCompletionSource<bool>();

            context?.Post(state =>
            {
                try
                {
                    uiAction?.Invoke();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, null);

            return tcs.Task;
        }

    }
}
