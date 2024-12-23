using Serein.Library.Api;
using Serein.Library;
using System.Security.AccessControl;

namespace Serein.NodeFlow.Model
{
    /// <summary>
    /// 单动作节点（用于动作控件)
    /// </summary>
    public class SingleActionNode : NodeModelBase
    {
        public SingleActionNode(IFlowEnvironment environment):base(environment)
        {
            
        }

        /// <summary>
        /// 执行方法
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override Task<object> ExecutingAsync(IDynamicContext context)
        {
            return base.ExecutingAsync(context);
        }
    }
}
