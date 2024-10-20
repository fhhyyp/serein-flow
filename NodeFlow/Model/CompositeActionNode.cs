using Serein.Library;
using Serein.Library.Api;

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
        public CompositeActionNode(IFlowEnvironment environment, List<SingleActionNode> actionNodes):base(environment)
        {
            ActionNodes = actionNodes;
        }

        //public override async Task<object?> Executing(IDynamicContext context)
        public override Task<object?> ExecutingAsync(IDynamicContext context)
        {
            throw new NotImplementedException("动作区域暂未实现");
        }

        public override Parameterdata[] GetParameterdatas()
        {
            return [];
        }

        public override NodeInfo? ToInfo()
        {
            if (MethodDetails is null) return null;

            var trueNodes = SuccessorNodes[ConnectionType.IsSucceed].Select(item => item.Guid); // 真分支
            var falseNodes = SuccessorNodes[ConnectionType.IsFail].Select(item => item.Guid);// 假分支
            var errorNodes = SuccessorNodes[ConnectionType.IsError].Select(item => item.Guid);// 异常分支
            var upstreamNodes = SuccessorNodes[ConnectionType.Upstream].Select(item => item.Guid);// 上游分支
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
                ChildNodeGuids = ActionNodes.Select(node => node.Guid).ToArray(),
            };
        }
    }

}
