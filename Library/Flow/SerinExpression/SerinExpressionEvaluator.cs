using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Serein.Flow.NodeModel.SingleExpOpNode;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Serein.Flow.SerinExpression
{
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
        public static object Evaluate(string expression, object targetObJ,out bool IsChange)
        {
            var parts = expression.Split([' '], 2);
            if (parts.Length != 2)
            {
                throw new ArgumentException("Invalid expression format.");
            }

            var operation = parts[0].ToLower();
            var operand = parts[1][0] == '.' ? parts[1][1..]: parts[1];

            var result = operation switch
            {
                "@num" => ComputedNumber(targetObJ, operand),
                "@call" => InvokeMethod(targetObJ, operand),
                "@get" => GetMember(targetObJ, operand),
                "@set" => SetMember(targetObJ, operand),
                _ => throw new NotSupportedException($"Operation {operation} is not supported.")
            };

            IsChange = operation switch
            {
                "@num" => true,
                "@call" => true,
                "@get" => true,
                "@set" => false,
                _ => throw new NotSupportedException($"Operation {operation} is not supported.")
            };

            return result;
        }


        private static readonly char[] separator = ['(', ')'];
        private static readonly char[] separatorArray = [','];

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
            if (method == null)
            {
                throw new ArgumentException($"Method {methodName} not found on target.");
            }

            var parameterValues = method.GetParameters()
                                        .Select((p, index) => Convert.ChangeType(parameters[index], p.ParameterType))
                                        .ToArray();


            return method.Invoke(target, parameterValues);

        }

        private static object GetMember(object target, string memberPath)
        {
            var members = memberPath.Split('.');
            foreach (var member in members)
            {

                if (target == null) return null;


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


            return target;

        }

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

        private static double ComputedNumber(object value,string expression)
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
