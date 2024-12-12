using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.Library.Utils.SereinExpression;
using System.Dynamic;
using System.Reactive;
using System.Reflection.Metadata;

namespace Serein.NodeFlow.Model
{
    /// <summary>
    /// Expression Operation - 表达式操作
    /// </summary>
    [NodeProperty(ValuePath = NodeValuePath.Node)]
    public partial class SingleExpOpNode : NodeModelBase
    {
        /// <summary>
        /// 表达式
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private string _expression;

    }



    public partial class SingleExpOpNode : NodeModelBase
    {
        /// <summary>
        /// 表达式参数索引
        /// </summary>
        private const int INDEX_EXPRESSION = 0;

        public SingleExpOpNode(IFlowEnvironment environment) : base(environment)
        {

        }

        /// <summary>
        /// 加载完成后调用的方法
        /// </summary>
        public override void OnCreating()
        {
            // 这里的这个参数是为了方便使用入参控制点，参数无意义
            var pd = new ParameterDetails[1];
            pd[INDEX_EXPRESSION] = new ParameterDetails
            {
                Index = INDEX_EXPRESSION,
                Name = nameof(Expression),
                IsExplicitData = false,
                DataValue = string.Empty,
                DataType = typeof(string),
                ExplicitType = typeof(string),
                ArgDataSourceNodeGuid = string.Empty,
                ArgDataSourceType = ConnectionArgSourceType.GetPreviousNodeData,
                NodeModel = this,
                Convertor = null,
                ExplicitTypeName = "Value",
                Items = null,
            };
            this.MethodDetails.ParameterDetailss = [.. pd];
        }

        /// <summary>
        /// 导出方法信息
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        public override NodeInfo SaveCustomData(NodeInfo nodeInfo)
        {
            dynamic data = new ExpandoObject();
            data.Expression = Expression ?? "";
            nodeInfo.CustomData = data;
            return nodeInfo;
        }

        /// <summary>
        /// 加载自定义数据
        /// </summary>
        /// <param name="nodeInfo"></param>
        public override void LoadCustomData(NodeInfo nodeInfo)
        {
            this.Expression = nodeInfo.CustomData?.Expression ?? "";
        }


        public override async Task<object?> ExecutingAsync(IDynamicContext context)
        {
            object? parameter = null;// context.TransmissionData(this); // 表达式节点使用上一节点数据
            var pd = MethodDetails.ParameterDetailss[0];

            if (pd.ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeData)
            {
                // 使用自定义节点的参数
                parameter = context.GetFlowData(pd.ArgDataSourceNodeGuid);
            }
            else if (pd.ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeDataOfInvoke)
            {
                // 立刻调用目标节点，然后使用其返回值
                parameter = await Env.InvokeNodeAsync(context, pd.ArgDataSourceNodeGuid);
            }
            else
            {
                // 条件节点透传上一节点的数据
                parameter = context.TransmissionData(this);
            }

            try
            {
                var newData = SerinExpressionEvaluator.Evaluate(Expression, parameter, out bool isChange);
                object? result = null;
                if (isChange)
                {
                    result = newData;
                }
                else
                {
                    result = parameter;
                }

                context.NextOrientation = ConnectionInvokeType.IsSucceed;
                return result;
            }
            catch (Exception ex)
            {
                context.NextOrientation = ConnectionInvokeType.IsError;
                context.ExceptionOfRuning = ex;
                return parameter;
            }

        }

    }
}
