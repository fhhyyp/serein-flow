using Serein.Library.Api;
using Serein.Library;
using System.Security.AccessControl;

namespace Serein.NodeFlow.Model.Nodes
{
    /// <summary>
    /// 单动作节点（用于动作控件)
    /// </summary>
    public class SingleActionNode : NodeModelBase
    {
        /// <summary>
        /// 构造一个新的单动作节点实例。
        /// </summary>
        /// <param name="environment"></param>
        public SingleActionNode(IFlowEnvironment environment):base(environment)
        {
            
        }

    }
}
