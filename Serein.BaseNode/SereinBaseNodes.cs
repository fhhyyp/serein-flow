using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils.SereinExpression;

namespace Serein.BaseNode
{

    public enum ExpType
    {
        Get,
        Set
    }
    [DynamicFlow(Name ="基础节点")]
    internal class SereinBaseNodes
    {
        [NodeAction(NodeType.Action,"条件节点")]
        private bool SereinConditionNode(IDynamicContext context,
                                         object targetObject,
                                         string exp = "ISPASS")
        {
            var isPass = SereinConditionParser.To(targetObject, exp);
            context.NextOrientation = isPass ? ConnectionInvokeType.IsSucceed : ConnectionInvokeType.IsFail;
            return isPass;
        }

      


        [NodeAction(NodeType.Action, "表达式节点")]
        private object SereinExpNode(IDynamicContext context,
                                         object targetObject,
                                         string exp)
        {

            exp = "@" + exp;
            var newData = SerinExpressionEvaluator.Evaluate(exp, targetObject, out bool isChange);
            object result;
            if (isChange || exp.StartsWith("@GET",System.StringComparison.OrdinalIgnoreCase))
            {
                result = newData;
            }
            else
            {
                result = targetObject;
            }
            context.NextOrientation = ConnectionInvokeType.IsSucceed;
            return result;
        }


    }
}
