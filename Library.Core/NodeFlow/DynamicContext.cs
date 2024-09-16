﻿using Serein.Library.Api;
using Serein.Library.Utils;

namespace Serein.Library.Core.NodeFlow
{

    /// <summary>
    /// 动态流程上下文
    /// </summary>
    public class DynamicContext: IDynamicContext
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
            NodeRunCts ??= SereinIoc.GetOrInstantiate<NodeRunCts>();
            return Task.Factory.StartNew(async () =>
            {
                for (int i = 0; i < count; i++)
                {
                    NodeRunCts.Token.ThrowIfCancellationRequested();
                    await Task.Delay(time);
                    action.Invoke();
                }
            });
        }
    }


}