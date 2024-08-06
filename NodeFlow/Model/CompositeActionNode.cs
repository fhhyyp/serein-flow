namespace Serein.NodeFlow.Model
{

    /// <summary>
    /// 组合动作节点（用于动作区域）
    /// </summary>
    public class CompositeActionNode : NodeBase
    {
        public List<SingleActionNode> ActionNodes;
        /// <summary>
        /// 组合动作节点（用于动作区域）
        /// </summary>
        public CompositeActionNode(List<SingleActionNode> actionNodes)
        {
            ActionNodes = actionNodes;
        }
        public void AddNode(SingleActionNode node)
        {
            ActionNodes.Add(node);
            MethodDetails ??= node.MethodDetails;
        }

    }

}
