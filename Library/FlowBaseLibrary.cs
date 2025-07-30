using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Serein.Library
{
    /// <summary>
    /// 基础功能
    /// </summary>
    [DynamicFlow(Name ="[基础功能]")]
    public static class FlowBaseLibrary
    {

        /// <summary>
        /// 对象透传，直接返回入参的值
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [NodeAction(NodeType.Action, "对象透传")]
        public static object TransmissionObject(object value) => value;

        /// <summary>
        /// 键值对组装，将入参的值与名称组装成一个字典对象
        /// </summary>
        /// <param name="argNames"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>

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

        /// <summary>
        /// 数组组装，将入参的值组装成一个数组对象
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [NodeAction(NodeType.Action, "数组组装")]
        public static object[] ArraySet(params object[] value)
        {
            return value;
        }

        /// <summary>
        /// 输出到控制台，使用SereinEnv.WriteLine方法输出信息
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [NodeAction(NodeType.Action, "输出")]
        public static object[] Console(params object[] value)
        {
            foreach (var item in value)
            {
                SereinEnv.WriteLine(InfoType.INFO, item.ToString());
            }
            return value;
        }

        /// <summary>
        /// 逻辑分支，根据布尔值选择返回的值，如果布尔值为true则返回t_value，否则返回f_value
        /// </summary>
        /// <param name="bool"></param>
        /// <param name="t_value"></param>
        /// <param name="f_value"></param>
        /// <returns></returns>
        [NodeAction(NodeType.Action, "逻辑选择")]
        public static object LogicalBranch([NodeParam(IsExplicit = false)]bool @bool, 
                                            object t_value,
                                            object f_value)
        {
            return @bool ? t_value : f_value;
        }

        /// <summary>
        /// 文本拼接，将多个文本值拼接成一个字符串，支持换行符和制表符的特殊处理
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>

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

        /// <summary>
        /// 动态构建对象，将字典中的键值对转换为一个动态对象，支持指定类名和打印结果
        /// </summary>
        /// <param name="dict"></param>
        /// <param name="classTypeName"></param>
        /// <param name="IsPrint"></param>
        /// <returns></returns>

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

    }
}
