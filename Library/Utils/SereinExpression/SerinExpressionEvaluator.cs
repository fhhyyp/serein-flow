using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;

namespace Serein.Library.Utils.SereinExpression
{
    /// <summary>
    /// 使用表达式操作/获取 对象的值
    /// 获取值 @get .xx.xxx 
    /// 设置值 @set .xx.xxx  = [data]
    /// </summary>
    /// <param name="obj">操作的对象</param>
    /// <returns></returns>
    public class SerinArithmeticExpressionEvaluator<T> where T : struct, IComparable<T>
    {
        private static readonly DataTable table = new DataTable();

        public static T Evaluate(string expression, T inputValue)
        {

            // 替换占位符@为输入值
            expression = expression.Replace("@", inputValue.ToString());
            try
            {
                // 使用 DataTable.Compute 方法计算表达式
                var result = table.Compute(expression, string.Empty);
                return (T)result;
            }
            catch
            {
                throw new ArgumentException("Invalid arithmetic expression.");
            }
        }
    }

    public class SerinExpressionEvaluator
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="expression">表达式</param>
        /// <param name="targetObJ">操作对象</param>
        /// <param name="isChange">是否改变了对象（Set语法）</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public static object Evaluate(string expression, object targetObJ, out bool isChange)
        {
            if (expression.Equals("@get", StringComparison.OrdinalIgnoreCase))
            {
                isChange = false;
                return targetObJ;
            }
            //if (expression is null || targetObJ is null)
            //{
            //    throw new Exception("表达式条件expression is null、 targetObJ is null");
            //}
            var parts = expression.Split(new[] { ' ' }, 2, StringSplitOptions.None);
            if (parts.Length < 1)
            {
                throw new ArgumentException("Invalid expression format.");
            }

            var operation = parts[0];
            var operand = parts[1][0] == '.' ? parts[1].Substring(1) : parts[1];
            object result;
            isChange = false;

            //if (operation == "@num")
            //{
            //    result = ComputedNumber(targetObJ, operand);
            //}
           
            if (operation.Equals("@get",StringComparison.OrdinalIgnoreCase))
            {
                isChange = true;
                if (operand[0].Equals('#'))
                {
                    // 存在全局变量表达式
                    var strIndex = operand.IndexOf('#',1);
                    var globalDataKeyName = operand.Substring(1, strIndex - 1);
                    targetObJ = SereinEnv.GetFlowGlobalData(globalDataKeyName);
                    if(strIndex == operand.Length - 1)
                    {
                        return targetObJ;
                    }
                    operand = operand.Substring(strIndex+1);
                }
                
                result = GetMember(targetObJ, operand);
            }
            else if (operation.Equals("@dtc", StringComparison.OrdinalIgnoreCase))
            {
                isChange = true;
                result = DataTypeConversion(targetObJ, operand);
            }
            else if (operation.Equals("@call", StringComparison.OrdinalIgnoreCase))
            {
                result = InvokeMethod(targetObJ, operand);
            }
            else if (operation.Equals("@set",StringComparison.OrdinalIgnoreCase))
            {
                isChange = true;
                result = SetMember(targetObJ, operand);
            }
            else
            {
                throw new NotSupportedException($"Operation {operation} is not supported.");
            }



            return result;
        }


        private static readonly char[] separator = new char[] { '(', ')' };
        private static readonly char[] separatorArray = new char[] { ',' };

        /// <summary>
        /// 调用目标方法
        /// </summary>
        /// <param name="target">目标实例</param>
        /// <param name="methodCall">方法名称</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private static object InvokeMethod(object target, string methodCall)
        {
            if (target is null) return null;
            var methodParts = methodCall.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            if (methodParts.Length != 2)
            {
                throw new ArgumentException("Invalid method call format.");
            }

            var methodName = methodParts[0];
            var parameterList = methodParts[1];
            var parameters = parameterList.Split(separatorArray, StringSplitOptions.RemoveEmptyEntries)
                                          .Select(p => p.Trim())
                                          .ToArray();

            var method = target.GetType().GetMethod(methodName) ?? throw new ArgumentException($"Method {methodName} not found on target.");
            var parameterValues = method.GetParameters()
                                        .Select((p, index) => Convert.ChangeType(parameters[index], p.ParameterType))
                                        .ToArray();


            return method.Invoke(target, parameterValues);

        }

        /// <summary>
        /// 获取值
        /// </summary>
        /// <param name="target">目标实例</param>
        /// <param name="memberPath">属性路径</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private static object GetMember(object target, string memberPath)
        {
            if (target is null) return null;


            // 分割成员路径，按 '.' 处理多级访问
            var members = memberPath.Split('.');

            foreach (var member in members)
            {
                var hasType = SereinExpressionExtension.TryGetType(member, out var memberName, out var type);

                // 检查成员是否包含数组索引，例如 "array[0]" "dict[key]"
                var hasIndex = SereinExpressionExtension.TryGetIndex(member, out var elementName, out var strIndexKey);

                // 检查成员是否包含数组索引，例如 "cars[0]"
                if (hasIndex)
                {
                    // 解析数组/集合名与索引部分
                    var targetType = target?.GetType(); // 目标对象的类型
                    #region 处理键值对
                    if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                    {
                        // 目标是键值对
                        var method = targetType.GetMethod("get_Item", BindingFlags.Public | BindingFlags.Instance);
                        if (method != null)
                        {
                            target = method.Invoke(target, new object[] { strIndexKey });
                        }
                    } 
                    #endregion
                    else
                    {
                        #region 表达式处理集合对象
                        // 获取数组或集合对象

                        // 如果arrayName为空，说明target可能是数组，而不需要再获取属性了
                        if (!string.IsNullOrEmpty(elementName))
                        {
                            var arrayProperty = target?.GetType().GetProperty(elementName);
                            if (arrayProperty is null)
                            {
                                var arrayField = target?.GetType().GetField(elementName);
                                if (arrayField is null)
                                {
                                    throw new ArgumentException($"Member {elementName} not found on target.");
                                }
                                else
                                {
                                    target = arrayField.GetValue(target);
                                }
                            }
                            else
                            {
                                target = arrayProperty.GetValue(target);
                            }
                        }
                       
                        // 提取数组索引
                        if (!int.TryParse(strIndexKey, out int index))
                        {
                            throw new ArgumentException($"Invalid array index '{strIndexKey}' for member {member}");
                        }
                        // 访问数组或集合中的指定索引
                        if (target is Array array)
                        {
                            if (index < 0 || index >= array.Length)
                            {
                                throw new ArgumentException($"Index {index} out of bounds for array {elementName}");
                            }
                            target = array.GetValue(index);
                        }
                        else if (target is IList<object> list)
                        {
                            if (index < 0 || index >= list.Count)
                            {
                                throw new ArgumentException($"Index {index} out of bounds for list {elementName}");
                            }
                            target = list[index];
                        }
                        else
                        {
                            throw new ArgumentException($"Member {elementName} is not an array or list.");
                        }
                        #endregion
                    }
                }
                else
                {
                    // 处理非数组情况的属性或字段

                    var property = target?.GetType().GetProperty(hasType ? memberName : member);
                    if (property is null)
                    {
                        var field = target?.GetType().GetField(member);
                        if (field is null)
                        {
                            throw new ArgumentException($"Member {member} not found on target.");
                        }
                        else
                        {
                            target = field.GetValue(target);
                        }
                    }
                    else
                    {
                        target = property.GetValue(target);
                    }
                }

                if (hasType)
                {
                    target = target.ToConvert(type);
                }


            }
            return target;
        }

        /// <summary>
        /// 设置目标的值
        /// </summary>
        /// <param name="target">目标实例</param>
        /// <param name="assignment">属性路径 </param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private static object SetMember(object target, string assignment)
        {
            var parts = assignment.Split(new[] { '=' }, 2);
            if (parts.Length != 2)
            {
                throw new ArgumentException("Invalid assignment format.");
            }

            var memberPath = parts[0].Trim();
            var value = parts[1].Trim();

            var members = memberPath.Split('.');
            for (int i = 0; i < members.Length - 1; i++)
            {
                var member = members[i];

                // 检查是否包含数组索引
                var arrayIndexStart = member.IndexOf('[');
                if (arrayIndexStart != -1)
                {
                    // 解析数组名和索引
                    var arrayName = member.Substring(0, arrayIndexStart);
                    var arrayIndexEnd = member.IndexOf(']');
                    if (arrayIndexEnd == -1 || arrayIndexEnd <= arrayIndexStart + 1)
                    {
                        throw new ArgumentException($"Invalid array syntax for member {member}");
                    }

                    var indexStr = member.Substring(arrayIndexStart + 1, arrayIndexEnd - arrayIndexStart - 1);
                    if (!int.TryParse(indexStr, out int index))
                    {
                        throw new ArgumentException($"Invalid array index '{indexStr}' for member {member}");
                    }

                    // 获取数组或集合
                    var arrayProperty = target?.GetType().GetProperty(arrayName);
                    if (arrayProperty is null)
                    {
                        var arrayField = target?.GetType().GetField(arrayName);
                        if (arrayField is null)
                        {
                            throw new ArgumentException($"Member {arrayName} not found on target.");
                        }
                        else
                        {
                            target = arrayField.GetValue(target);
                        }

                    }
                    else
                    {
                        target = arrayProperty.GetValue(target);
                    }

                    // 获取目标数组或集合中的指定元素
                    if (target is Array array)
                    {
                        if (index < 0 || index >= array.Length)
                        {
                            throw new ArgumentException($"Index {index} out of bounds for array {arrayName}");
                        }
                        target = array.GetValue(index);
                    }
                    else if (target is IList<object> list)
                    {
                        if (index < 0 || index >= list.Count)
                        {
                            throw new ArgumentException($"Index {index} out of bounds for list {arrayName}");
                        }
                        target = list[index];
                    }
                    else
                    {
                        throw new ArgumentException($"Member {arrayName} is not an array or list.");
                    }
                }
                else
                {
                    // 处理非数组情况的属性或字段
                    var property = target?.GetType().GetProperty(member);
                    if (property is null)
                    {
                        var field = target?.GetType().GetField(member);
                        if (field is null)
                        {
                            throw new ArgumentException($"Member {member} not found on target.");
                        }
                        else
                        {
                            target = field.GetValue(target);
                        }
                    }
                    else
                    {
                        target = property.GetValue(target);
                    }
                }
            }

            // 设置值
            var lastMember = members.Last();

            var lastProperty = target?.GetType().GetProperty(lastMember);
            if (lastProperty is null)
            {
                var lastField = target?.GetType().GetField(lastMember);
                if (lastField is null)
                {
                    throw new ArgumentException($"Member {lastMember} not found on target.");
                }
                else
                {
                    var convertedValue = Convert.ChangeType(value, lastField.FieldType);
                    lastField.SetValue(target, convertedValue);
                }
            }
            else
            {
                var convertedValue = Convert.ChangeType(value, lastProperty.PropertyType);
                lastProperty.SetValue(target, convertedValue);
            }

            return target;
        }

      

        /// <summary>
        /// 数据类型转换
        /// </summary>
        /// <param name="value"></param>
        /// <param name="expression"></param>
        /// <returns></returns>
        private static object DataTypeConversion(object value, string expression)
        {
            // 使用方法
            // @dtc <int>value
            // @dtc <int>flowObj
            if(!SereinExpressionExtension.TryGetType(expression,out var elementName,  out var type))
            {
                throw new ArgumentException($"无法获取类型：{expression}");
            }

            int endIndex = expression.IndexOf('>');
            if(endIndex == expression.Length -1)
            {
                return value.ToConvert(type);
            }
            else
            {
                string valueStr = expression.Substring(endIndex + 1, expression.Length - endIndex - 1);
                return valueStr.ToValueData(type);
            }
        }


        /// <summary>
        /// 计算数学简单表达式
        /// </summary>
        /// <param name="value"></param>
        /// <param name="expression"></param>
        /// <returns></returns>
        private static decimal ComputedNumber(object value, string expression)
        {
            return ComputedNumber<decimal>(value, expression);
        }

        /// <summary>
        /// 计算数学公式
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="expression"></param>
        /// <returns></returns>
        private static T ComputedNumber<T>(object value, string expression) where T : struct, IComparable<T>
        {
            T result = value.ToConvert<T>();
            return SerinArithmeticExpressionEvaluator<T>.Evaluate(expression, result);
        }


       

    }
}
