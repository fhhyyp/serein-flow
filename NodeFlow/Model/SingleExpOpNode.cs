using Serein.Library.Api;
using Serein.Library.Entity;
using Serein.Library.Enums;
using Serein.NodeFlow.Base;
using Serein.NodeFlow.Tool.SerinExpression;

namespace Serein.NodeFlow.Model
{
    /// <summary>
    /// Expression Operation - 表达式操作
    /// </summary>
    public class SingleExpOpNode : NodeModelBase
    {
        /// <summary>
        /// 表达式
        /// </summary>
        public string Expression { get; set; }


        public override object? Execute(IDynamicContext context)
        {
            var data = PreviousNode?.FlowData;

            var newData = SerinExpressionEvaluator.Evaluate(Expression, data, out bool isChange);

            FlowState = FlowStateType.Succeed;
            Console.WriteLine(newData);
            if (isChange)
            {
                return newData;
            }
            else
            {
                return PreviousNode?.FlowData;
            }

        }

        public override Parameterdata[] GetParameterdatas()
        {
            if (base.MethodDetails.ExplicitDatas.Length > 0)
            {
                return MethodDetails.ExplicitDatas
                                     .Select(it => new Parameterdata
                                     {
                                         state = it.IsExplicitData,
                                         // value = it.DataValue,
                                         expression = Expression,
                                     })
                                     .ToArray();
            }
            else
            {
                return [];
            }
        }
    }
}
