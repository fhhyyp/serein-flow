using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.Library.Utils.SereinExpression;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace Serein.BaseNode
{

    public enum ExpType
    {
        Get,
        Set
    }
    [DynamicFlow(Name ="[基础节点]")]
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



        [NodeAction(NodeType.Action, "KV数据收集节点")]
        private Dictionary<string, object> SereinKvDataCollectionNode(string argName, params object[] value)
        {
            var names = argName.Split(';');
            var count = Math.Min(value.Length, names.Length);
            var dict = new Dictionary<string, object>();
            for (int i = 0; i < count; i++)
            {
                dict[names[i]] = value[i]; 
            }
            return dict;
        }
        [NodeAction(NodeType.Action, "List数据收集节点")]
        private object[] SereinListDataCollectionNode(params object[] value)
        {
            return value;
        }

        /* if (!DynamicObjectHelper.TryResolve(dict, className, out var result))
                    {
                        Console.WriteLine("赋值过程中有错误，请检查属性名和类型！");
                    }
                    else
                    {
                        DynamicObjectHelper.PrintObjectProperties(result);
                    }
                    //if (!ObjDynamicCreateHelper.TryResolve(externalData, "RootType", out var result))
                    //{
                    //    Console.WriteLine("赋值过程中有错误，请检查属性名和类型！");

                    //}
                    //ObjDynamicCreateHelper.PrintObjectProperties(result!);
                    return result;*/
    }
}
