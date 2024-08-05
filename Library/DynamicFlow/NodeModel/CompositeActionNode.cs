using Serein.DynamicFlow;
using System.Diagnostics;

namespace Serein.DynamicFlow.NodeModel
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

        //public override void Execute(DynamicContext context)
        //{
        //    //Dictionary<int,object> dict = new Dictionary<int,object>();
        //    for (int i = 0; i < ActionNodes.Count; i++)
        //    {
        //        SingleActionNode? action = ActionNodes[i];
        //        try
        //        {
        //            action.Execute(context);
        //        }
        //        catch (Exception ex)
        //        {
        //            Debug.Write(ex.Message);
        //            return;
        //        }
        //    }

        //    CurrentState = true;
        //    return;


        //    /*foreach (var nextNode in TrueBranchNextNodes)
        //    {
        //        nextNode.ExecuteStack(context);
        //    }*/
        //}
    }

}
