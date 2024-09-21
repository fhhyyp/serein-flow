using Serein.Library.Utils;
using System;
using System.Threading.Tasks;

namespace Serein.Library.Api
{
    public interface IDynamicContext
    {
        IFlowEnvironment FlowEnvironment { get; }
        ISereinIOC SereinIoc { get; }
        Task CreateTimingTask(Action action, int time = 100, int count = -1);
    }
}
