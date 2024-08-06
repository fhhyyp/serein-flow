using Serein.NodeFlow;
using Serein.NodeFlow.SerinExpression;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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


        public override object? Execute(DynamicContext context)
        {
            var data = PreviousNode?.FlowData;

            var newData = SerinExpressionEvaluator.Evaluate(Expression, data, out bool isChange);

            FlowState = true;
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
