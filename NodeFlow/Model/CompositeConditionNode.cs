namespace Serein.NodeFlow.Model
{
    /// <summary>
    /// 组合条件节点（用于条件区域）
    /// </summary>
    public class CompositeConditionNode : NodeBase
    {
        public List<SingleConditionNode> ConditionNodes { get; } = [];


        public void AddNode(SingleConditionNode node)
        {
            ConditionNodes.Add(node);
            MethodDetails ??= node.MethodDetails;
        }

        /// <summary>
        /// 条件节点重写执行方法
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object? Execute(DynamicContext context)
        {
            // bool allTrue = ConditionNodes.All(condition => Judge(context,condition.MethodDetails));
            // bool IsAllTrue = true; // 初始化为 true
            FlowState = FlowStateType.Succeed;
            foreach (SingleConditionNode? node in ConditionNodes)
            {
                var state = Judge(context, node);
                if (state == FlowStateType.Fail || FlowStateType.Fail == FlowStateType.Error)
                {
                    FlowState = state;
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
        private FlowStateType Judge(DynamicContext context, SingleConditionNode node)
        {
            try
            {
                node.Execute(context);
                return node.FlowState;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return FlowStateType.Error;
            }
        }



    }

}
