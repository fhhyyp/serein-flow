using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils.SereinExpression;
using System.ComponentModel;

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
        private bool _isCustomData;

        /// <summary>
        /// 自定义参数值
        /// </summary>
        [PropertyInfo(IsNotification = true)]

        private object? _customData;
        /// <summary>
        /// 条件表达式
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private string _expression;

    }

    public partial class SingleConditionNode : NodeModelBase
    {
        public SingleConditionNode(IFlowEnvironment environment):base(environment)
        {
            this.IsCustomData = false;
            this.CustomData = null;
            this.Expression = "PASS";
        }

        /// <summary>
        /// 重写节点的方法执行
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override Task<object?> ExecutingAsync(IDynamicContext context)
        {
            // 接收上一节点参数or自定义参数内容
            object? parameter;
            object? result = PreviousNode?.GetFlowData();   // 条件节点透传上一节点的数据
            if (IsCustomData) // 是否使用自定义参数
            {
                // 表达式获取上一节点数据
                var getObjExp = CustomData?.ToString();
                if (!string.IsNullOrEmpty(getObjExp) && getObjExp.Length >= 4 && getObjExp[..4].Equals("@get", StringComparison.CurrentCultureIgnoreCase))
                {
                    parameter = result;
                    if (parameter is not null)
                    {
                        parameter = SerinExpressionEvaluator.Evaluate(getObjExp, parameter, out _);
                    }
                }
                else
                {
                    parameter = CustomData;
                }
            }
            else
            {
                parameter = result;
            }
            try
            {
                
                var isPass = SereinConditionParser.To(parameter, Expression);
                NextOrientation = isPass ? ConnectionType.IsSucceed : ConnectionType.IsFail;
            }
            catch (Exception ex)
            {
                NextOrientation = ConnectionType.IsError;
                RuningException = ex;
            }
            
            Console.WriteLine($"{result} {Expression}  -> " + NextOrientation);
            return Task.FromResult(result);
        }

        public override Parameterdata[] GetParameterdatas()
        {
            var value = CustomData switch
            {
                Type when CustomData.GetType() == typeof(int)
                           && CustomData.GetType() == typeof(double)
                           && CustomData.GetType() == typeof(float)
                                => ((double)CustomData).ToString(),
                Type when CustomData.GetType() == typeof(bool) => ((bool)CustomData).ToString(),
                _ => CustomData?.ToString()!,
            };
            return [new Parameterdata
            {
                State = IsCustomData,
                Expression = Expression,
                Value = value,
            }];
        }

        public override NodeModelBase LoadInfo(NodeInfo nodeInfo)
        {
            var node = this;
            node.Guid = nodeInfo.Guid;
            this.Position = nodeInfo.Position;// 加载位置信息
            for (int i = 0; i < nodeInfo.ParameterData.Length; i++)
            {
                Parameterdata? pd = nodeInfo.ParameterData[i];
                node.IsCustomData = pd.State;
                node.CustomData = pd.Value;
                node.Expression = pd.Expression;

            }
            return this;
        }

       
    }


}
