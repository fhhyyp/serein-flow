using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Serein.Library.Framework.NodeFlow
{


    /// <summary>
    /// 动态流程上下文
    /// </summary>
    public class DynamicContext : IDynamicContext
    {
        public DynamicContext(ISereinIOC sereinIoc, IFlowEnvironment flowEnvironment)
        {
            SereinIoc = sereinIoc;
            FlowEnvironment = flowEnvironment;
        }

        public NodeRunCts NodeRunCts { get; set; }
        public ISereinIOC SereinIoc { get; }
        public IFlowEnvironment FlowEnvironment { get; }

        public Task CreateTimingTask(Action action, int time = 100, int count = -1)
        {
            if(NodeRunCts == null)
            {
                NodeRunCts = SereinIoc.GetOrRegisterInstantiate<NodeRunCts>();
            }
            // 使用局部变量，避免捕获外部的 `action`
            Action localAction = action;

            return Task.Run(async () =>
            {
                for (int i = 0; i < count && !NodeRunCts.IsCancellationRequested; i++)
                {
                    await Task.Delay(time);
                    if (NodeRunCts.IsCancellationRequested) { break; }
                    if (FlowEnvironment.IsGlobalInterrupt)
                    {
                        await FlowEnvironment.GetOrCreateGlobalInterruptAsync();
                    }
                    // 确保对局部变量的引用
                    localAction?.Invoke();
                }

                // 清理引用，避免闭包导致的内存泄漏
                localAction = null;
            });
        }
    }
}
