using Serein.Library;
using Serein.Library.Api;
using Serein.Script;
using System.Dynamic;
using System.Linq.Expressions;

namespace Serein.NodeFlow.Model.Nodes
{
    /// <summary>
    /// 条件节点（用于条件控件）
    /// </summary>
    [FlowDataProperty(ValuePath = NodeValuePath.Node, IsNodeImp = true)]
    public partial class SingleConditionNode : NodeModelBase
    {
        /// <summary>
        /// 是否为自定义参数
        /// </summary>
        [DataInfo(IsNotification = true)]
        private bool _isExplicitData;

        /// <summary>
        /// 自定义参数值
        /// </summary>
        [DataInfo(IsNotification = true)]
        private string? _explicitData;

        /// <summary>
        /// 条件表达式
        /// </summary>
        [DataInfo(IsNotification = true)]
        private string _expression;

    }

    public partial class SingleConditionNode : NodeModelBase
    {
        /// <summary>
        /// 条件表达式节点是基础节点
        /// </summary>
        public override bool IsBase => true;

        /// <summary>
        /// 表达式参数索引
        /// </summary>
        private const int INDEX_EXPRESSION = 0;

        /// <summary>
        /// 条件节点构造函数
        /// </summary>
        /// <param name="environment"></param>
        public SingleConditionNode(IFlowEnvironment environment):base(environment)
        {
            this.IsExplicitData = false;
            this.ExplicitData = string.Empty;
            this.Expression = "PASS";
        }

        /// <summary>
        /// 创建节点时调用的方法
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
                //Convertor = null,
                InputType = ParameterValueInputType.Input,
                Items = null,
                Description = "条件节点入参控制点"
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
        /// <param name="token"></param>
        /// <returns></returns>
        public override async Task<FlowResult> ExecutingAsync(IFlowContext context, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return FlowResult.Fail(this.Guid, context, "流程已通过token取消");
            }
            // 接收上一节点参数or自定义参数内容
            object? parameter;
            object? result = null;

            if (!IsExplicitData) 
            {
                // 使用自动取参
                var pd = MethodDetails.ParameterDetailss[INDEX_EXPRESSION];
                var hasNode = context.Env.TryGetNodeModel(pd.ArgDataSourceNodeGuid, out var argSourceNode);
                if (!hasNode)
                {
                    /*context.NextOrientation = ConnectionInvokeType.IsError;
                    return new FlowResult(this.Guid, context);*/
                    parameter = null;
                }
                else
                {
                    if (pd.ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeData)
                    {
                        result = context.GetFlowData(argSourceNode.Guid).Value; // 使用自定义节点的参数
                    }
                    else if (pd.ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeDataOfInvoke)
                    {
                        CancellationTokenSource cts = new CancellationTokenSource();
                        var nodeResult = await argSourceNode.ExecutingAsync(context, cts.Token);
                        result = nodeResult.Value;
                        cts?.Cancel();
                        cts?.Dispose();
                    }
                    else
                    {
                        result = context.TransmissionData(this.Guid).Value;    // 条件节点透传上一节点的数据
                    }
                    parameter = result;  // 使用上一节点的参数
                }
                

            }
            else
            {
                var exp = ExplicitData?.ToString();
                if (!string.IsNullOrWhiteSpace(exp) && exp.StartsWith("@Get", StringComparison.OrdinalIgnoreCase))
                {
                    parameter = GetValueExpressionAsync(context, result, exp);
                }
                else
                {
                    parameter = ExplicitData;  // 使用自定义的参数
                }
            }

            bool judgmentResult = false;
            try
            {
                judgmentResult = await ConditionExpressionAsync(context, parameter, Expression);
                //judgmentResult = SereinConditionParser.To(parameter, Expression);
                context.NextOrientation = judgmentResult ? ConnectionInvokeType.IsSucceed : ConnectionInvokeType.IsFail;
            }
            catch (Exception ex)
            {
                context.NextOrientation = ConnectionInvokeType.IsError;
                context.ExceptionOfRuning = ex;
            }

            SereinEnv.WriteLine(InfoType.INFO, $"{Expression}  -> " + context.NextOrientation);
            //return result;
            return FlowResult.OK(this.Guid, context, parameter);
        }




        /// <summary>
        /// 解析取值表达式
        /// </summary>
        private SereinScript getValueScript;
        private string getValueExpression;
        private async Task<object?> GetValueExpressionAsync(IFlowContext flowContext, object? data, string expression)
        {
            var dataName = nameof(data);
            if (!expression.Equals(getValueExpression))
            {
                getValueExpression = expression;
                getValueScript = new SereinScript();
                var dataType = data is null ? typeof(object) : data.GetType();
                if (expression[0..4].Equals("@get", StringComparison.CurrentCultureIgnoreCase))
                {
                    getValueExpression = expression[4..];
                    // 表达式默认包含 “.”
                    getValueExpression = $"return {dataName}{expression};";
                }
                else
                {
                    // 表达式默认包含 “.”
                    getValueExpression = $"return {getValueExpression};";
                }
                var resultType = getValueScript.ParserScript(getValueExpression, new Dictionary<string, Type>
                {
                    { dataName, dataType},
                });

            }

            IScriptInvokeContext scriptContext = new ScriptInvokeContext();
            scriptContext.SetVarValue(dataName, data);

            var result = await getValueScript.InterpreterAsync(scriptContext);
            return result;
        }



        /// <summary>
        /// 解析条件
        /// </summary>
        private SereinScript conditionScript;
        private string conditionExpression;

        private async Task<bool> ConditionExpressionAsync(IFlowContext flowContext, object? data, string expression)
        {
            var dataName = nameof(data);
            if (!expression.Equals(conditionExpression))
            {
                conditionExpression = expression.Trim();
                conditionScript = new SereinScript();
                var dataType = data is null ?  typeof(object) : data.GetType();
                if (expression[0] == '.')
                {
                    // 对象取值
                    conditionExpression = $"return {dataName}{conditionExpression};";
                }
                else
                {
                    // 直接表达式
                    conditionExpression = $"return {dataName}.{conditionExpression};";
                }
                var resultType = conditionScript.ParserScript(conditionExpression, new Dictionary<string, Type>
                {
                    { dataName, dataType},
                });
            }

            IScriptInvokeContext scriptContext = new ScriptInvokeContext();
            scriptContext.SetVarValue(dataName, data);

            var result = await conditionScript.InterpreterAsync(scriptContext);
            if(result is bool @bool) 
            {
                return @bool;
            }
            else
            {
                flowContext.NextOrientation = ConnectionInvokeType.IsError;
                return false;
            }
        }



    }


}
