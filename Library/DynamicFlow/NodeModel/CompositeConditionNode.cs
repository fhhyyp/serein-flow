using Serein.DynamicFlow.Tool;
using System.Diagnostics;

namespace Serein.DynamicFlow.NodeModel
{

    /// <summary>
    /// 组合条件节点（用于条件区域）
    /// </summary>
    public class CompositeConditionNode : NodeBase
    {
        public List<SingleConditionNode> ConditionNodes { get; } =[];


        public void AddNode(SingleConditionNode node)
        {
            ConditionNodes.Add(node);
            MethodDetails ??= node.MethodDetails;
        }

        public override object? Execute(DynamicContext context)
        {
            // bool allTrue = ConditionNodes.All(condition => Judge(context,condition.MethodDetails));
            // bool IsAllTrue = true; // 初始化为 true
            FlowState = true;
            foreach (SingleConditionNode? node in ConditionNodes)
            {
                if (!Judge(context, node))
                {
                    FlowState = false;
                    break;// 一旦发现条件为假，立即退出循环
                }
            }

            return PreviousNode?.FlowData;
            //if (IsAllTrue)
            //{
            //    foreach (var nextNode in TrueBranchNextNodes)
            //    {
            //        nextNode.ExecuteStack(context);
            //    }
            //}
            //else
            //{
            //    foreach (var nextNode in FalseBranchNextNodes)
            //    {
            //        nextNode.ExecuteStack(context);
            //    }
            //}
        }
        private bool Judge(DynamicContext context, SingleConditionNode node)
        {
            try
            {
                node.Execute(context);
                return node.FlowState;
            }
            catch (Exception ex)
            {
                Debug.Write(ex.Message);
            }
            return false;
        }



    }

}
