using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.Library.Utils.SereinExpression;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;

namespace Serein.Library
{
    /// <summary>
    /// 基础功能
    /// </summary>

    [DynamicFlow(Name ="[基础功能]")]
    public class SereinBaseFunction
    {
        //[NodeAction(NodeType.Action,"条件节点")]
        //private bool SereinConditionNode(IDynamicContext context,
        //                                 object targetObject,
        //                                 string exp = "ISPASS")
        //{
        //    var isPass = SereinConditionParser.To(targetObject, exp);
        //    context.NextOrientation = isPass ? ConnectionInvokeType.IsSucceed : ConnectionInvokeType.IsFail;
        //    return isPass;
        //}

        //[NodeAction(NodeType.Action, "表达式节点")]
        //private object SereinExpNode(IDynamicContext context,
        //                                 object targetObject,
        //                                 string exp)
        //{

        //    exp = "@" + exp;
        //    var newData = SerinExpressionEvaluator.Evaluate(exp, targetObject, out bool isChange);
        //    object result;
        //    if (isChange || exp.StartsWith("@GET",System.StringComparison.OrdinalIgnoreCase))
        //    {
        //        result = newData;
        //    }
        //    else
        //    {
        //        result = targetObject;
        //    }
        //    context.NextOrientation = ConnectionInvokeType.IsSucceed;
        //    return result;
        //}



        [NodeAction(NodeType.Action, "键值对组装")]
        private Dictionary<string, object> SereinKvDataCollectionNode(/*NodeModelBase nodeModel, */
                                                                      string argName, 
                                                                      params object[] value)
        {
            //var paramsArgIndex = nodeModel.MethodDetails.ParamsArgIndex;
            //var pds = nodeModel.MethodDetails.ParameterDetailss;
            //var length = pds.Length - paramsArgIndex;
            //for(int i = paramsArgIndex; i < pds.Length; i++)
            //{
            //    var pd = pds[i];
            //}

            var names = argName.Split(';');
            var count = Math.Min(value.Length, names.Length);
            var dict = new Dictionary<string, object>();
            for (int i = 0; i < count; i++)
            {
                dict[names[i]] = value[i]; 
            }
            return dict;
        }

        [NodeAction(NodeType.Action, "数组组装")]
        private object[] SereinListDataCollectionNode(params object[] value)
        {
            return value;
        }

        [NodeAction(NodeType.Action, "输出")]
        private object[] SereinConsoleNode(params object[] value)
        {
            foreach (var item in value)
            {
                SereinEnv.WriteLine(InfoType.INFO, item.ToString());
            }
            return value;
        }

        [NodeAction(NodeType.Action, "文本拼接")]
        private string SereinTextJoin(params object[] value)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var item in value)
            {
                var tmp = item.ToString();
                if (tmp == "\\n")
                {
                    sb.Append(Environment.NewLine);
                }
                else if (tmp == "\\t")
                {
                    sb.Append('\t');
                }
                else
                {
                    sb.Append(tmp);
                }
            }
            return sb.ToString();
        }


        [NodeAction(NodeType.Action, "键值对动态构建对象")]
        private object SereinKvDataToObject(Dictionary<string, object> dict, 
                                            string classTypeName = "newClass_dynamic",
                                            bool IsPrint = false)
        {
            if (!DynamicObjectHelper.TryResolve(dict, classTypeName, out var result))
            {
                Console.WriteLine("赋值过程中有错误，请检查属性名和类型！");
            }
            else
            {
                if (IsPrint)
                {
                    Console.WriteLine("创建完成，正在打印结果");
                    DynamicObjectHelper.PrintObjectProperties(result);
                }
            }
            return result;
        }


        [NodeAction(NodeType.Action, "设置/更新全局数据")]
        private object SereinAddOrUpdateFlowGlobalData(string name,object data)
        {
            SereinEnv.AddOrUpdateFlowGlobalData(name, data);
            return data;
        }

    }
}
