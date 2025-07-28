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
    public static class FlowBaseLibrary
    {
        
        [NodeAction(NodeType.Action, "对象透传")]
        public static object TransmissionObject(object value) => value;

        [NodeAction(NodeType.Action, "键值对组装")]
        public static Dictionary<string, object> DictSet(string argNames, params object[] value)
        {
            var names = argNames.Split(';');
            if(value.Length != names.Length)
            {
                throw new ArgumentException("参数名称数量与入参数量不一致");
            }
            var count = value.Length;
            var dict = new Dictionary<string, object>();
            for (int i = 0; i < count; i++)
            {
                dict[names[i]] = value[i]; 
            }
            return dict;
        }

        [NodeAction(NodeType.Action, "数组组装")]
        public static object[] ArraySet(params object[] value)
        {
            return value;
        }

        [NodeAction(NodeType.Action, "输出")]
        public static object[] Console(params object[] value)
        {
            foreach (var item in value)
            {
                SereinEnv.WriteLine(InfoType.INFO, item.ToString());
            }
            return value;
        }

        [NodeAction(NodeType.Action, "逻辑分支")]
        public static object LogicalBranch([NodeParam(IsExplicit = false)]bool @bool, 
                                            object t_value,
                                            object f_value)
        {
            return @bool ? t_value : f_value;
        }

        [NodeAction(NodeType.Action, "文本拼接")]
        public static string TextJoin(params object[] value)
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
        public static object CreateDynamicObjectOfDict(Dictionary<string, object> dict, 
                                            string classTypeName = "newClass_dynamic",
                                            bool IsPrint = false)
        {
            if (!DynamicObjectHelper.TryResolve(dict, classTypeName, out var result))
            {
                System.Console.WriteLine("赋值过程中有错误，请检查属性名和类型！");
            }
            else
            {
                if (IsPrint)
                {
                    System.Console.WriteLine("创建完成，正在打印结果");
                    DynamicObjectHelper.PrintObjectProperties(result);
                }
            }
            return result;
        }


        [NodeAction(NodeType.Action, "设置或更新全局数据")]
        public static object AddOrUpdateFlowGlobalData(string name, object data)
        {
            SereinEnv.AddOrUpdateFlowGlobalData(name, data);
            return data;
        }

    }
}
