using Serein.Library.Api;
using Serein.Library.Enums;
using Serein.Library.Core.NodeFlow;
using Serein.NodeFlow.Tool.SerinExpression;

namespace Serein.NodeFlow.Model
{
    /// <summary>
    /// Expression Operation - 表达式操作
    /// </summary>
    public class SingleExpOpNode : NodeBase
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
    }
}
