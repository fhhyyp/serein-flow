using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.Library.Utils.SereinExpression;
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
        public SingleExpOpNode(IFlowEnvironment environment) : base(environment)
        {

        }

        /// <summary>
        /// 加载完成后调用的方法
        /// </summary>
        public override void OnCreating()
        {
            var pd = new ParameterDetails
            {
                Index = 0,
                Name = nameof(Expression),
                DataType = typeof(string),
                ExplicitType = typeof(string),
                IsExplicitData = false,
                DataValue = string.Empty,
                ArgDataSourceNodeGuid = string.Empty,
                ArgDataSourceType = ConnectionArgSourceType.GetPreviousNodeData,
                NodeModel = this,
                Convertor = null,
                ExplicitTypeName = "Value",
                Items = Array.Empty<string>(),
            };

            this.MethodDetails.ParameterDetailss = new ParameterDetails[] { pd };
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
                Console.WriteLine(newData);
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
                RuningException = ex;
                return parameter;
            }

        }

        public override ParameterData[] GetParameterdatas()
        {
            return [new ParameterData { 
                Value = Expression,
                SourceNodeGuid = this.MethodDetails.ParameterDetailss[0].ArgDataSourceNodeGuid,
                SourceType = this.MethodDetails.ParameterDetailss[0].ArgDataSourceType.ToString(),
            }];
        }



        public override NodeModelBase LoadInfo(NodeInfo nodeInfo)
        {
            var node = this;
            node.Guid = nodeInfo.Guid;
            this.Position = nodeInfo.Position;// 加载位置信息

            var pdInfo1 = nodeInfo.ParameterData[0];
            node.Expression = pdInfo1.Value; // 加载表达式
            
            for (int i = 0; i < nodeInfo.ParameterData.Length; i++)
            {
                ParameterData? pd = nodeInfo.ParameterData[i];
                node.MethodDetails.ParameterDetailss[i].ArgDataSourceNodeGuid = pd.SourceNodeGuid;
                node.MethodDetails.ParameterDetailss[i].ArgDataSourceType = EnumHelper.ConvertEnum<ConnectionArgSourceType>(pd.SourceType);
            }
            return this;
        }
    }
}
