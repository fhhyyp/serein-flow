using Serein.Library;
using Serein.Library.Api;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Model.Infos;
using Serein.NodeFlow.Model.Nodes;
using System.Runtime.CompilerServices;
using System.Text;

namespace Serein.NodeFlow.Services
{
    internal static class CoreGenerateExtension
    {
        /// <summary>
        /// 生成流程接口信息描述
        /// </summary>
        /// <param name="flowCallNode"></param>
        /// <returns></returns>
        public static FlowApiMethodInfo? ToFlowApiMethodInfo(this SingleFlowCallNode flowCallNode)
        {
            var targetNode = flowCallNode.TargetNode;
            if (targetNode.ControlType is not (NodeControlType.Action or NodeControlType.Script)) return null;
            if (flowCallNode.MethodDetails is null) return null;
            if (string.IsNullOrWhiteSpace(flowCallNode.ApiGlobalName)) return null;


            FlowApiMethodInfo flowApiMethodInfo = new FlowApiMethodInfo(flowCallNode);
            flowApiMethodInfo.ReturnType = targetNode.ControlType == NodeControlType.Script ? typeof(object)
                                                                                            : flowCallNode.MethodDetails.ReturnType;

            flowApiMethodInfo.ApiMethodName = flowCallNode.ApiGlobalName;

            List<FlowApiMethodInfo.ParamInfo> list = [];

            int index = 0;
            foreach (var pd in flowCallNode.MethodDetails.ParameterDetailss)
            {
                if (pd.DataType is null || string.IsNullOrWhiteSpace(pd.Name))
                {
                    return null;
                }
                if (pd.IsParams)
                {
                    list.Add(new FlowApiMethodInfo.ParamInfo(pd.DataType, $"{pd.Name}{index++}"));
                }
                else
                {
                    list.Add(new FlowApiMethodInfo.ParamInfo(pd.DataType, pd.Name));
                }
            }

            flowApiMethodInfo.ParamInfos = list;
            return flowApiMethodInfo;
        }

        /// <summary>
        /// 生成方法名称
        /// </summary>
        /// <param name="flowNode"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToNodeMethodName(this IFlowNode flowNode)
        {
            /*if (!flowLibraryService.TryGetMethodInfo(flowNode.MethodDetails.AssemblyName,
                                                           flowNode.MethodDetails.MethodName,
                                                           out var methodInfo))
            {
                throw new Exception();
            }*/
            var guid = flowNode.Guid;
            var tmp = guid.Replace("-", "");
            var methodName = $"FlowMethod_{tmp}";
            return methodName;
        }


        /// <summary>
        /// 生成完全的xml注释
        /// </summary>
        /// <param name="context"></param>
        /// <param name="retractCount"></param>
        /// <returns></returns>
        public static string ToXmlComments(this string context, int retractCount = 0)
        {
            StringBuilder sb = new StringBuilder();
            var startLine = "/// <summary>";
            var endLine = "/// </summary>";
            sb.AppendLine(startLine);
            var rows = context.Split(Environment.NewLine);
            string retract = new string(' ', retractCount * 4);
            foreach (var row in rows)
            {
                // 处理转义
                var value = row.Replace("<", "&lt")
                               .Replace(">", "&gt");
                sb.AppendLine($"{retract}/// <para>{value}</para>");
            }
            sb.AppendLine(endLine);
            return sb.ToString();
        }

        /// <summary>
        /// 生成类型的驼峰命名法名称（首字母小写）
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToCamelCase(this Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            // 获取类型名称（不包括命名空间）
            string typeName = type.Name;

            if (string.IsNullOrEmpty(typeName))
            {
                return string.Empty;
            }

            // 处理泛型类型（去掉后面的`N）
            int indexOfBacktick = typeName.IndexOf('`');
            if (indexOfBacktick > 0)
            {
                typeName = typeName.Substring(0, indexOfBacktick);
            }

            // 如果是接口且以"I"开头，去掉第一个字母
            if (type.IsInterface && typeName.Length > 1 && typeName[0] == 'I' && char.IsUpper(typeName[1]))
            {
                typeName = typeName.Substring(1);
            }

            // 转换为驼峰命名法：首字母小写，其余不变
            if (typeName.Length > 0)
            {
                return char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
            }

            return typeName;
        }

        /// <summary>
        /// 生成类型的大驼峰命名法名称（PascalCase）
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToPascalCase(this Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            string typeName = type.Name;

            if (string.IsNullOrEmpty(typeName))
            {
                return string.Empty;
            }

            // 去掉泛型标记（如 `1）
            int indexOfBacktick = typeName.IndexOf('`');
            if (indexOfBacktick > 0)
            {
                typeName = typeName.Substring(0, indexOfBacktick);
            }

            // 如果是接口以 I 开头，且后面是大写，去掉前缀 I
            if (type.IsInterface && typeName.Length > 1 && typeName[0] == 'I' && char.IsUpper(typeName[1]))
            {
                typeName = typeName.Substring(1);
            }

            // 首字母转为大写（如果有需要）
            if (typeName.Length > 0)
            {
                return char.ToUpperInvariant(typeName[0]) + typeName.Substring(1);
            }

            return typeName;
        }

        /// <summary>
        /// 将字符串首字母大写（PascalCase）
        /// </summary>
        /// <param name="text">原始文本</param>
        /// <returns>首字母大写的文本</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToPascalCase(this string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (char.IsUpper(text[0]))
            {
                return text; // 已是大写
            }

            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }
    }



}
