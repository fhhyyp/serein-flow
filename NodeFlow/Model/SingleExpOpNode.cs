using Serein.Library.Api;
using Serein.Library.Entity;
using Serein.Library.Enums;
using Serein.NodeFlow.Base;
using Serein.NodeFlow.Tool.SereinExpression;
using System.Text;

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

            try
            {
                var newData = SerinExpressionEvaluator.Evaluate(Expression, data, out bool isChange);
                Console.WriteLine(newData);
                object? result = null;
                if (isChange)
                {
                    result =  newData;
                }
                else
                {
                    result =  PreviousNode?.FlowData;
                }

                NextOrientation = ConnectionType.IsSucceed;
                return result;
            }
            catch (Exception ex)
            {
                NextOrientation = ConnectionType.IsError;
                RuningException = ex;
                return PreviousNode?.FlowData;
            }

        }

        internal override Parameterdata[] GetParameterdatas()
        {
            return [new Parameterdata{ Expression = Expression}];
        }



        internal override NodeModelBase LoadInfo(NodeInfo nodeInfo)
        {
            var node = this;
            if (node != null)
            {
                node.Guid = nodeInfo.Guid;
                for (int i = 0; i < nodeInfo.ParameterData.Length; i++)
                {
                    node.Expression = nodeInfo.ParameterData[i].Expression;
                }
            }
            return this;
        }
    }
}
