using Serein.Library.Entity;
using Serein.Library.Enums;
using Serein.NodeFlow.Base;

namespace Serein.NodeFlow.Model
{

    /// <summary>
    /// 组合动作节点（用于动作区域）
    /// </summary>
    public class CompositeActionNode : NodeModelBase
    {
        public List<SingleActionNode> ActionNodes;
        /// <summary>
        /// 组合动作节点（用于动作区域）
        /// </summary>
        public CompositeActionNode(List<SingleActionNode> actionNodes)
        {
            ActionNodes = actionNodes;
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
                ChildNodes = ActionNodes.Select(node => node.ToInfo()).ToArray(),
            };
        }
    }

}
