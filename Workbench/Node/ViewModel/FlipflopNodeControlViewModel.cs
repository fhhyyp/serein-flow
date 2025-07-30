using Serein.NodeFlow.Model;
using Serein.NodeFlow.Model.Nodes;
using Serein.Workbench.Node.View;

namespace Serein.Workbench.Node.ViewModel
{
    /// <summary>
    /// 单触发器节点控制视图模型
    /// </summary>
    public class FlipflopNodeControlViewModel : NodeControlViewModelBase
    {
        /// <summary>
        /// 单触发器节点模型
        /// </summary>
        public SingleFlipflopNode NodelModel { get;}

        /// <summary>
        /// 构造一个新的单触发器节点控制视图模型实例。
        /// </summary>
        /// <param name="node"></param>
        public FlipflopNodeControlViewModel(SingleFlipflopNode node) : base(node)
        {
            this.NodelModel = node;
        }
    }
}
