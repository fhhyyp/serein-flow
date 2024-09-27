using System.Data;

namespace Serein.NodeFlow.Tool.SereinExpression
{
    /// <summary>
    /// 使用表达式操作/获取 对象的值
    /// 获取值 @get .xx.xxx 
    /// 设置值 @set .xx.xxx  = [data]
    /// </summary>
    /// <param name="obj">操作的对象</param>
    /// <returns></returns>
    public class SerinArithmeticExpressionEvaluator
    {
        private static readonly DataTable table = new DataTable();

        public static double Evaluate(string expression, double inputValue)
        {
            // 替换占位符@为输入值
            expression = expression.Replace("@", inputValue.ToString());
            try
            {
                // 使用 DataTable.Compute 方法计算表达式
                var result = table.Compute(expression, string.Empty);
                return Convert.ToDouble(result);
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
            var parts = expression.Split([' '], 2);
            if (parts.Length != 2)
            {
                throw new ArgumentException("Invalid expression format.");
            }

            var operation = parts[0].ToLower();
            var operand = parts[1][0] == '.' ? parts[1][1..] : parts[1];

            var result = operation switch
            {
                "@num" => ComputedNumber(targetObJ, operand),
                "@call" => InvokeMethod(targetObJ, operand),
                "@get" => GetMember(targetObJ, operand),
                "@set" => SetMember(targetObJ, operand),
                _ => throw new NotSupportedException($"Operation {operation} is not supported.")
            };

            isChange = operation switch
            {
                /*"@num" => true,
                "@call" => true,
                "@get" => true,*/
                "@set" => false,
                _ => true,
            };

            return result;
        }


        private static readonly char[] separator = ['(', ')'];
        private static readonly char[] separatorArray = [','];

        /// <summary>
        /// 调用目标方法
        /// </summary>
        /// <param name="target">目标实例</param>
        /// <param name="methodCall">方法名称</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private static object InvokeMethod(object target, string methodCall)
        {
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

            var method = target.GetType().GetMethod(methodName);
            if (method is null)
            {
                throw new ArgumentException($"Method {methodName} not found on target.");
            }

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
            // 分割成员路径，按 '.' 处理多级访问
            var members = memberPath.Split('.');

            foreach (var member in members)
            {
                if (target == null) return null;

                // 检查成员是否包含数组索引，例如 "cars[0]"
                var arrayIndexStart = member.IndexOf('[');
                if (arrayIndexStart != -1)
                {
                    // 解析数组/集合名与索引部分
                    var arrayName = member.Substring(0, arrayIndexStart);
                    var arrayIndexEnd = member.IndexOf(']');
                    if (arrayIndexEnd == -1 || arrayIndexEnd <= arrayIndexStart + 1)
                    {
                        throw new ArgumentException($"Invalid array syntax for member {member}");
                    }

                    // 提取数组索引
                    var indexStr = member.Substring(arrayIndexStart + 1, arrayIndexEnd - arrayIndexStart - 1);
                    if (!int.TryParse(indexStr, out int index))
                    {
                        throw new ArgumentException($"Invalid array index '{indexStr}' for member {member}");
                    }

                    // 获取数组或集合对象
                    var arrayProperty = target.GetType().GetProperty(arrayName);
                    if (arrayProperty != null)
                    {
                        target = arrayProperty.GetValue(target);
                    }
                    else
                    {
                        var arrayField = target.GetType().GetField(arrayName);
                        if (arrayField != null)
                        {
                            target = arrayField.GetValue(target);
                        }
                        else
                        {
                            throw new ArgumentException($"Member {arrayName} not found on target.");
                        }
                    }

                    // 访问数组或集合中的指定索引
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
                    var property = target.GetType().GetProperty(member);
                    if (property != null)
                    {
                        target = property.GetValue(target);
                    }
                    else
                    {
                        var field = target.GetType().GetField(member);
                        if (field != null)
                        {
                            target = field.GetValue(target);
                        }
                        else
                        {
                            throw new ArgumentException($"Member {member} not found on target.");
                        }
                    }
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
                    var arrayProperty = target.GetType().GetProperty(arrayName);
                    if (arrayProperty != null)
                    {
                        target = arrayProperty.GetValue(target);
                    }
                    else
                    {
                        var arrayField = target.GetType().GetField(arrayName);
                        if (arrayField != null)
                        {
                            target = arrayField.GetValue(target);
                        }
                        else
                        {
                            throw new ArgumentException($"Member {arrayName} not found on target.");
                        }

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
                    var property = target.GetType().GetProperty(member);
                    if (property != null)
                    {
                        target = property.GetValue(target);
                    }
                    else
                    {
                        var field = target.GetType().GetField(member);
                        if (field != null)
                        {
                            target = field.GetValue(target);
                        }
                        else
                        {
                            throw new ArgumentException($"Member {member} not found on target.");
                        }
                    }
                }
            }

            var lastMember = members.Last();

            var lastProperty = target.GetType().GetProperty(lastMember);
            if (lastProperty != null)
            {
                var convertedValue = Convert.ChangeType(value, lastProperty.PropertyType);
                lastProperty.SetValue(target, convertedValue);
            }
            else
            {
                var lastField = target.GetType().GetField(lastMember);
                if (lastField != null)
                {
                    var convertedValue = Convert.ChangeType(value, lastField.FieldType);
                    lastField.SetValue(target, convertedValue);
                }
                else
                {
                    throw new ArgumentException($"Member {lastMember} not found on target.");
                }
            }

            return target;
        }
        /// <summary>
        /// 计算数学简单表达式
        /// </summary>
        /// <param name="value"></param>
        /// <param name="expression"></param>
        /// <returns></returns>
        private static double ComputedNumber(object value, string expression)
        {
            double numericValue = Convert.ToDouble(value);
            if (!string.IsNullOrEmpty(expression))
            {
                numericValue = SerinArithmeticExpressionEvaluator.Evaluate(expression, numericValue);
            }

            return numericValue;
        }
    }
}
