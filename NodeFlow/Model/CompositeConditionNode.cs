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

        /// <summary>
        /// 设置区域子项
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        public override NodeInfo SaveCustomData(NodeInfo nodeInfo)
        {
            nodeInfo.ChildNodeGuids = ConditionNodes.Select(node => node.Guid).ToArray();
            return nodeInfo;
        }

        /// <summary>
        /// 添加条件子项
        /// </summary>
        /// <param name="node"></param>
        public void AddNode(SingleConditionNode node)
        {
            if (ConditionNodes is null)
            {
                ConditionNodes = new List<SingleConditionNode>();
            }
            ConditionNodes.Add(node);
            MethodDetails ??= node.MethodDetails;
        }


      
    }

}
