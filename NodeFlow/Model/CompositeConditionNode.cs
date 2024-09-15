using Serein.Library.Api;
using Serein.Library.Entity;
using Serein.Library.Enums;
using Serein.NodeFlow.Base;

namespace Serein.NodeFlow.Model
{
    /// <summary>
    /// 组合条件节点（用于条件区域）
    /// </summary>
    public class CompositeConditionNode : NodeModelBase
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
        public override object? Execute(IDynamicContext context)
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

        


        private FlowStateType Judge(IDynamicContext context, SingleConditionNode node)
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

        public override Parameterdata[] GetParameterdatas()
        {
            return [];
        }

        public override NodeInfo ToInfo()
        {
            if (MethodDetails == null) return null;

            //var trueNodes = SucceedBranch.Select(item => item.Guid); // 真分支
            //var falseNodes = FailBranch.Select(item => item.Guid);// 假分支
            //var upstreamNodes = UpstreamBranch.Select(item => item.Guid);// 上游分支
            //var errorNodes = ErrorBranch.Select(item => item.Guid);// 异常分支
            var trueNodes = SuccessorNodes[ConnectionType.IsSucceed].Select(item => item.Guid); // 真分支
            var falseNodes = SuccessorNodes[ConnectionType.IsFail].Select(item => item.Guid);// 假分支
            var upstreamNodes = SuccessorNodes[ConnectionType.IsError].Select(item => item.Guid);// 上游分支
            var errorNodes = SuccessorNodes[ConnectionType.Upstream].Select(item => item.Guid);// 异常分支

            // 生成参数列表
            Parameterdata[] parameterData = GetParameterdatas();

            return new NodeInfo
            {
                Guid = Guid,
                MethodName = MethodDetails?.MethodName,
                Label = DisplayName ?? "",
                Type = this.GetType().ToString(),
                TrueNodes = trueNodes.ToArray(),
                FalseNodes = falseNodes.ToArray(),
                UpstreamNodes = upstreamNodes.ToArray(),
                ParameterData = parameterData.ToArray(),
                ErrorNodes = errorNodes.ToArray(),
                ChildNodes = ConditionNodes.Select(node => node.ToInfo()).ToArray(),
            };
        }

    }

}
