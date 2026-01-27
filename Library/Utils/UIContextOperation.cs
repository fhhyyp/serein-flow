using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{
    /// <summary>
    /// 为类库提供了在UI线程上下文操作的方法（使用Serein.Workbench时不用处理）
    /// 在WPF、Winform项目中，多线程中直接操作UI线程可能发生非预期的异常
    /// 所以当你设置自己的平台时，需要手动实例化这个工具类
    /// </summary>
    public class UIContextOperation
    {
        private SynchronizationContext context;
        public Func<SynchronizationContext> GetUiContext = null;

        static UIContextOperation()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {

            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {

            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {

            }
        }
        
        


        /// <summary>
        /// 传入UI线程上下文
        /// </summary>
        /// <param name="synchronizationContext">线程上下文</param>
        [SereinIOCCtor(IsIgnore =  true)]
        public UIContextOperation(SynchronizationContext synchronizationContext)
        {
            this.context = synchronizationContext;
        }

        /// <summary>
        /// 传入获取UI线程上下文的闭包创建
        /// </summary>
        /// <param name="getUiContext">获取线程上下文的闭包函数</param>
        [SereinIOCCtor(IsIgnore =  true)]
        public UIContextOperation(Func<SynchronizationContext> getUiContext)
        {
            this.GetUiContext = getUiContext;
        }

        /// <summary>
        /// 同步方式在UI线程上进行调用方法
        /// </summary>
        /// <param name="uiAction">要执行的UI操作</param>
        /// <param name="onException">异常发生时的回调</param>
        public void Invoke(Action uiAction, Action<Exception> onException = null)
        {
           
            if(context is null && GetUiContext != null)
            {
                while (GetUiContext is null)
                {
                    continue;
                }
                context = GetUiContext.Invoke();
            }
            context?.Post(state =>
            {
                try
                {
                    uiAction?.Invoke();
                }
                catch (Exception ex)
                {
                    if(onException != null) 
                        onException(ex);
                    Debug.WriteLine(ex);
                }
            }, null);
        }

        /// <summary>
        /// 异步方式进行调用
        /// </summary>
        /// <param name="uiAction">要执行的UI操作</param>
        /// <param name="onException">异常发生时的回调</param>
        /// <returns></returns>
        public Task InvokeAsync(Action uiAction, Action<Exception> onException = null)
        {
            if (context is null )
            {
                while (GetUiContext is null)
                {
                    continue;
                }
                context = GetUiContext.Invoke();
            }
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
                    if (onException != null) onException(ex);
                    Debug.WriteLine(ex);
                }
            }, null);

            return tcs.Task;
        }

    }
}
