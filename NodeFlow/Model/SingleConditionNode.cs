using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.Library.Utils.SereinExpression;
using System;
using System.ComponentModel;
using System.Dynamic;
using System.Linq.Expressions;

namespace Serein.NodeFlow.Model
{
    /// <summary>
    /// 条件节点（用于条件控件）
    /// </summary>
    [NodeProperty(ValuePath = NodeValuePath.Node)]
    public partial class SingleConditionNode : NodeModelBase
    {
        /// <summary>
        /// 是否为自定义参数
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private bool _isExplicitData;

        /// <summary>
        /// 自定义参数值
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private string? _explicitData;

        /// <summary>
        /// 条件表达式
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private string _expression;

    }

    public partial class SingleConditionNode : NodeModelBase
    {
        /// <summary>
        /// 表达式参数索引
        /// </summary>
        private const int INDEX_EXPRESSION = 0;


        public SingleConditionNode(IFlowEnvironment environment):base(environment)
        {
            this.IsExplicitData = false;
            this.ExplicitData = string.Empty;
            this.Expression = "PASS";
        }

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
                //Convertor = null,
                ExplicitTypeName = "Value",
                Items = null,
            };
            this.MethodDetails.ParameterDetailss = [..pd];
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
            data.ExplicitData = ExplicitData ?? "";
            data.IsExplicitData = IsExplicitData;
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
            this.ExplicitData = nodeInfo.CustomData?.ExplicitData ?? "";
            this.IsExplicitData = nodeInfo.CustomData?.IsExplicitData ?? false;
        }

        /// <summary>
        /// 重写节点的方法执行
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override async Task<object?> ExecutingAsync(IDynamicContext context)
        {
            // 接收上一节点参数or自定义参数内容
            object? parameter;
            object? result = null;

            if (!IsExplicitData) 
            {
                // 使用自动取参
                var pd = MethodDetails.ParameterDetailss[INDEX_EXPRESSION];
                if (pd.ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeData)
                {
                    result = context.GetFlowData(pd.ArgDataSourceNodeGuid); // 使用自定义节点的参数
                }
                else if (pd.ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeDataOfInvoke)
                {
                    result = await Env.InvokeNodeAsync(context, pd.ArgDataSourceNodeGuid); // 立刻调用目标节点，然后使用其返回值
                }
                else
                {
                    result = context.TransmissionData(this);    // 条件节点透传上一节点的数据
                }
                parameter = result;  // 使用上一节点的参数

            }
            else
            {
                var exp = ExplicitData?.ToString();
                if (!string.IsNullOrEmpty(exp) && exp.StartsWith('@'))
                {
                    parameter = result; // 表达式获取上一节点数据
                    if (parameter is not null)
                    {
                        parameter = SerinExpressionEvaluator.Evaluate(exp, parameter, out _);
                    }
                }
                else
                {
                    parameter = ExplicitData;  // 使用自定义的参数
                }
            }

            bool judgmentResult = false;
            try
            {
                judgmentResult = SereinConditionParser.To(parameter, Expression);
                context.NextOrientation = judgmentResult ? ConnectionInvokeType.IsSucceed : ConnectionInvokeType.IsFail;
            }
            catch (Exception ex)
            {
                context.NextOrientation = ConnectionInvokeType.IsError;
                context.ExceptionOfRuning = ex;
            }

            SereinEnv.WriteLine(InfoType.INFO, $"{result} {Expression}  -> " + context.NextOrientation);
            //return result;
            return judgmentResult;
        }



    }


}
