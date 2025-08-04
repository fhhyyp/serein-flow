using Serein.Library;
using Serein.Library.Api;
using Serein.Script;
using System.Dynamic;

namespace Serein.NodeFlow.Model.Nodes
{
    /// <summary>
    /// Expression Operation - 表达式操作
    /// </summary>
    [FlowDataProperty(ValuePath = NodeValuePath.Node, IsNodeImp = true)]
    public partial class SingleExpOpNode : NodeModelBase
    {
        /// <summary>
        /// 表达式
        /// </summary>
        [DataInfo(IsNotification = true)]
        private string _expression;

    }


    /// <summary>
    /// 表达式节点模型基类
    /// </summary>
    public partial class SingleExpOpNode : NodeModelBase
    {
        /// <summary>
        /// 表达式节点是基础节点
        /// </summary>
        public override bool IsBase => true;

        /// <summary>
        /// 表达式参数索引
        /// </summary>
        private const int INDEX_EXPRESSION = 0;

        /// <summary>
        /// 表达式节点构造函数
        /// </summary>
        /// <param name="environment"></param>
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
                //Convertor = null,
                InputType = ParameterValueInputType.Input,
                Items = null,
                Description = "表达式节点入参控制点"

            };
            this.MethodDetails.ParameterDetailss = [.. pd];
            this.MethodDetails.ReturnType = typeof(object);
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

        /// <summary>
        /// 执行节点操作
        /// </summary>
        /// <param name="context"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public override async Task<FlowResult> ExecutingAsync(IFlowContext context, CancellationToken token)
        {
            if(token.IsCancellationRequested) return FlowResult.Fail(this.Guid, context, "流程已通过token取消");

            object? parameter = null;// 表达式节点使用上一节点数据
            var pd = MethodDetails.ParameterDetailss[0];

            var hasNode = context.Env.TryGetNodeModel(pd.ArgDataSourceNodeGuid, out var argSourceNode);
            if (!hasNode)
            {
                parameter = null;
            }
            else
            {
                if (pd.ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeData)
                {
                    // 使用自定义节点的参数
                    parameter = context.GetFlowData(argSourceNode.Guid).Value;
                }
                else if (pd.ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeDataOfInvoke)
                {
                    // 立刻调用目标节点，然后使用其返回值
                    var cts = new CancellationTokenSource();
                    var result = await argSourceNode.ExecutingAsync(context, cts.Token);
                    cts?.Cancel();
                    cts?.Dispose();
                    parameter = result.Value;
                }
            }
            try
            {
                var result = await GetValueExpressionAsync(context, parameter, Expression);
                context.NextOrientation = ConnectionInvokeType.IsSucceed;
                return FlowResult.OK(this.Guid, context, result);
            }
            catch (Exception ex)
            {
                context.NextOrientation = ConnectionInvokeType.IsError;
                context.ExceptionOfRuning = ex;
                return FlowResult.Fail(this.Guid, context, ex.Message);
            }

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
                getValueScript  = new SereinScript();
                var dataType = data is null ? typeof(object) : data.GetType();
                if (expression[0..4].Equals("@get", StringComparison.CurrentCultureIgnoreCase))
                {
                    expression = expression[4..];
                    // 表达式默认包含 “.”
                    expression = $"return {dataName}{expression};";
                }
                else
                {
                    // 表达式默认包含 “.”
                    expression = $"return {expression};";
                }
                var resultType = getValueScript.ParserScript(expression, new Dictionary<string, Type>
                {
                    { dataName, dataType},
                });


            }

            IScriptInvokeContext scriptContext = new ScriptInvokeContext();
            scriptContext.SetVarValue(dataName, data);

            var result = await getValueScript.InterpreterAsync(scriptContext);
            return result;
        }

    }
}
