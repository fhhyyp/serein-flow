using Serein.NodeFlow.Model;
using Serein.NodeFlow.Model.Nodes;
using Serein.Workbench.Node.View;

namespace Serein.Workbench.Node.ViewModel
{
    /// <summary>
    /// ActionNodeControlViewModel 类用于表示单动作节点的控制视图模型。
    /// </summary>
    public class ActionNodeControlViewModel : NodeControlViewModelBase
    {
        /// <summary>
        /// 构造一个新的 ActionNodeControlViewModel 实例。
        /// </summary>
        /// <param name="node"></param>
        public ActionNodeControlViewModel(SingleActionNode node) : base(node) 
        {
            // this.NodelModel = node;
        }
    }
}
