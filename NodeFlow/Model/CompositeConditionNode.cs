using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;


namespace Serein.NodeFlow.Model
{

    /// <summary>
    /// 组合条件节点（用于条件区域）
    /// </summary>
    [NodeProperty(ValuePath = NodeValuePath.Node)]
    public partial class CompositeConditionNode : NodeModelBase
    {
        /// <summary>
        /// 条件节点集合
        /// </summary>
        [PropertyInfo]
        private List<SingleConditionNode> _conditionNodes;
    }


    /// <summary>
    /// 组合条件节点（用于条件区域）
    /// </summary>
    public partial class CompositeConditionNode : NodeModelBase
    {
        public CompositeConditionNode(IFlowEnvironment environment):base(environment) 
        {
            
        }

        /// <summary>
        /// 加载完成后调用的方法
        /// </summary>
        public override void OnCreating()
        {
            SereinEnv.WriteLine(InfoType.WARN, "CompositeConditionNode 暂未实现 OnLoading");
        }

        public void AddNode(SingleConditionNode node)
        {
            if(ConditionNodes is null)
            {
                ConditionNodes = new List<SingleConditionNode>();
            }
            ConditionNodes.Add(node);
            MethodDetails ??= node.MethodDetails;
        }

        /// <summary>
        /// 条件节点重写执行方法
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override async Task<object?> ExecutingAsync(IDynamicContext context)
        {
            try
            {
                // 条件区域中遍历每个条件节点
                foreach (SingleConditionNode? node in ConditionNodes)
                {
                    var state = await node.ExecutingAsync(context);
                    if (context.NextOrientation != ConnectionInvokeType.IsSucceed)
                    {
                        // 如果条件不通过，立刻推出循环
                        break;
                    }
                }

                //var previousNode = context.GetPreviousNode()
                return context.TransmissionData(this); // 条件区域透传上一节点的数据
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(InfoType.WARN, ex.Message);
                context.NextOrientation = ConnectionInvokeType.IsError;
                context.ExceptionOfRuning = ex;
                return context.TransmissionData(this); // 条件区域透传上一节点的数据
            }

           
        }

        

        public override ParameterData[] GetParameterdatas()
        {
            return [];
        }

        public override NodeInfo ToInfo()
        {
            //if (MethodDetails == null) return null;

            //var trueNodes = SucceedBranch.Select(item => item.Guid); // 真分支
            //var falseNodes = FailBranch.Select(item => item.Guid);// 假分支
            //var upstreamNodes = UpstreamBranch.Select(item => item.Guid);// 上游分支
            //var errorNodes = ErrorBranch.Select(item => item.Guid);// 异常分支
            var trueNodes = SuccessorNodes[ConnectionInvokeType.IsSucceed].Select(item => item.Guid); // 真分支
            var falseNodes = SuccessorNodes[ConnectionInvokeType.IsFail].Select(item => item.Guid);// 假分支
            var errorNodes = SuccessorNodes[ConnectionInvokeType.IsError].Select(item => item.Guid);// 异常分支
            var upstreamNodes = SuccessorNodes[ConnectionInvokeType.Upstream].Select(item => item.Guid);// 上游分支

            // 生成参数列表
            ParameterData[] parameterData = GetParameterdatas();

            return new NodeInfo
            {
                Guid = Guid,
                AssemblyName = MethodDetails.AssemblyName,
                MethodName = MethodDetails.MethodName,
                Label = MethodDetails?.MethodAnotherName,
                Type = this.GetType().ToString(),
                TrueNodes = trueNodes.ToArray(),
                FalseNodes = falseNodes.ToArray(),
                UpstreamNodes = upstreamNodes.ToArray(),
                ParameterData = parameterData.ToArray(),
                ErrorNodes = errorNodes.ToArray(),
                ChildNodeGuids = ConditionNodes.Select(node => node.Guid).ToArray(),
                Position = Position,
            };
        }

    }

}
