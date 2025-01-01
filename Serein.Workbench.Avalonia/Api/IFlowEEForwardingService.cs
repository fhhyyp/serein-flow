using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Avalonia.Api
{
    /// <summary>
    /// 流程事件管理，转发流程运行环境中触发的事件到工作台各个订阅者
    /// </summary>
    internal interface IFlowEEForwardingService : IFlowEnvironmentEvent
    {

    }
}
