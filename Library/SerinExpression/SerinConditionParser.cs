using System.Globalization;
using System.Reflection;

namespace Serein.Library.SerinExpression;

public class SerinConditionParser
{
    public static bool To<T>(T data, string expression)
    {
        try
        {

            return ConditionParse(data, expression).Evaluate(data);

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
    }

    public static ConditionResolver ConditionParse(object data, string expression)
    {
        if (expression.StartsWith('.')) // 表达式前缀属于从上一个节点数据对象获取成员值
        {
            return ParseObjectExpression(data, expression);
        }
        else
        {
            return ParseSimpleExpression(data, expression);
        }


        //bool ContainsArithmeticOperators(string expression)
        //{
        //    return expression.Contains('+') || expression.Contains('-') || expression.Contains('*') || expression.Contains('/');
        //}

    }

    /// <summary>
    /// 获取计算表达式的部分
    /// </summary>
    /// <param name="part"></param>
    /// <returns></returns>
    private static string GetArithmeticExpression(string part)
    {
        int startIndex = part.IndexOf('[');
        int endIndex = part.IndexOf(']');
        if (startIndex >= 0 && endIndex > startIndex)
        {
            return part.Substring(startIndex + 1, endIndex - startIndex - 1);
        }

        return null;

    }
    /// <summary>
    /// 获取对象指定名称的成员
    /// </summary>
    private static object? GetMemberValue(object? obj, string memberPath)
    {
        string[] members = memberPath[1..].Split('.');
        foreach (var member in members)
        {
            if (obj == null) return null;
            Type type = obj.GetType();
            PropertyInfo? propertyInfo = type.GetProperty(member);
            FieldInfo? fieldInfo = type.GetField(member);
            if (propertyInfo != null)
                obj = propertyInfo.GetValue(obj);
            else if (fieldInfo != null)
                obj = fieldInfo.GetValue(obj);
            else
                throw new ArgumentException($"Member {member} not found in type {type.FullName}");
        }
        return obj;
    }
    /// <summary>
    /// 解析对象表达式
    /// </summary>
    private static ConditionResolver ParseObjectExpression(object data, string expression)
    {
        var parts = expression.Split(' ');
        string operatorStr = parts[0];
        string valueStr = string.Join(' ', parts, 1, parts.Length - 1);

        int typeStartIndex = expression.IndexOf('<');
        int typeEndIndex = expression.IndexOf('>');

        string memberPath;
        Type type;
        object? targetObj;
        if (typeStartIndex + typeStartIndex == -2)
        {
            memberPath = operatorStr;
            targetObj = GetMemberValue(data, operatorStr);

            type = targetObj.GetType();

            operatorStr = parts[1].ToLower();
            valueStr = string.Join(' ', parts.Skip(2));
        }
        else
        {
            if (typeStartIndex >= typeEndIndex)
            {
                throw new ArgumentException("无效的表达式格式");
            }
            memberPath = expression.Substring(0, typeStartIndex).Trim();
            string typeStr = expression.Substring(typeStartIndex + 1, typeEndIndex - typeStartIndex - 1).Trim().ToLower();
            parts = expression.Substring(typeEndIndex + 1).Trim().Split(' ');
            if (parts.Length == 3)
            {
                operatorStr = parts[1].ToLower();
                valueStr = string.Join(' ', parts.Skip(2));
            }
            else
            {
                operatorStr = parts[0].ToLower();
                valueStr = string.Join(' ', parts.Skip(1));
            }
            targetObj = GetMemberValue(data, memberPath);

            Type? tempType = typeStr switch
            {
                "int" => typeof(int),
                "double" => typeof(double),
                "bool" => typeof(bool),
                "string" => typeof(string),
                _ => Type.GetType(typeStr)
            };
            type = tempType ?? throw new ArgumentException("对象表达式无效的类型声明");
        }



        if (type == typeof(int))
        {
            int value = int.Parse(valueStr, CultureInfo.InvariantCulture);
            return new MemberConditionResolver<int>
            {
                TargetObj = targetObj,
                //MemberPath = memberPath,
                Op = ParseValueTypeOperator<int>(operatorStr),
                Value = value,
                ArithmeticExpression = GetArithmeticExpression(parts[0])
            };
        }
        else if (type == typeof(double))
        {
            double value = double.Parse(valueStr, CultureInfo.InvariantCulture);
            return new MemberConditionResolver<double>
            {
                //MemberPath = memberPath,
                TargetObj = targetObj,
                Op = ParseValueTypeOperator<double>(operatorStr),
                Value = value,
                ArithmeticExpression = GetArithmeticExpression(parts[0])
            };

        }
        else if (type == typeof(bool))
        {
            return new MemberConditionResolver<bool>
            {
                //MemberPath = memberPath,
                TargetObj = targetObj,
                Op = (ValueTypeConditionResolver<bool>.Operator)ParseBoolOperator(operatorStr)
            };
        }
        else if (type == typeof(string))
        {
            return new MemberStringConditionResolver
            {
                MemberPath = memberPath,
                Op = ParseStringOperator(operatorStr),
                Value = valueStr
            };
        }

        throw new NotSupportedException($"Type {type} is not supported.");
    }

    private static ConditionResolver ParseSimpleExpression(object data, string expression)
    {
        if ("pass".Equals(expression.ToLower()))
        {
            return new PassConditionResolver
            {
                Op = PassConditionResolver.Operator.Pass,
            };
        }
        else
        {
            if ("not pass".Equals(expression.ToLower()))
            {
                return new PassConditionResolver
                {
                    Op = PassConditionResolver.Operator.NotPass,
                };
            }
            if ("!pass".Equals(expression.ToLower()))
            {
                return new PassConditionResolver
                {
                    Op = PassConditionResolver.Operator.NotPass,
                };
            }
        }


        var parts = expression.Split(' ');

        if (parts.Length < 2)
            throw new ArgumentException("无效的表达式格式。");

        //string typeStr = parts[0];
        string operatorStr = parts[0];
        string valueStr = string.Join(' ', parts, 1, parts.Length - 1);

        Type type = data.GetType();//Type.GetType(typeStr);
        if (type == typeof(int))
        {
            var op = ParseValueTypeOperator<int>(operatorStr);
            if (op == ValueTypeConditionResolver<int>.Operator.InRange || op == ValueTypeConditionResolver<int>.Operator.OutOfRange)
            {
                var temp = valueStr.Split('-');
                if (temp.Length < 2)
                    throw new ArgumentException($"范围无效：{valueStr}。");
                int rangeStart = int.Parse(temp[0], CultureInfo.InvariantCulture);
                int rangeEnd = int.Parse(temp[1], CultureInfo.InvariantCulture);
                return new ValueTypeConditionResolver<int>
                {
                    Op = op,
                    RangeStart = rangeStart,
                    RangeEnd = rangeEnd,
                    ArithmeticExpression = GetArithmeticExpression(parts[0]),
                };
            }
            else
            {
                int value = int.Parse(valueStr, CultureInfo.InvariantCulture);
                return new ValueTypeConditionResolver<int>
                {
                    Op = op,
                    Value = value,
                    ArithmeticExpression = GetArithmeticExpression(parts[0])
                };

            }

        }
        else if (type == typeof(double))
        {
            double value = double.Parse(valueStr, CultureInfo.InvariantCulture);
            return new ValueTypeConditionResolver<double>
            {
                Op = ParseValueTypeOperator<double>(operatorStr),
                Value = value,
                ArithmeticExpression = GetArithmeticExpression(parts[0])
            };
        }
        else if (type == typeof(bool))
        {
            bool value = bool.Parse(valueStr);
            return new BoolConditionResolver
            {
                Op = ParseBoolOperator(operatorStr),
                Value = value,
            };
        }
        else if (type == typeof(string))
        {
            return new StringConditionResolver
            {
                Op = ParseStringOperator(operatorStr),
                Value = valueStr
            };
        }

        throw new NotSupportedException($"Type {type} is not supported.");
    }


    private static ValueTypeConditionResolver<T>.Operator ParseValueTypeOperator<T>(string operatorStr) where T : struct, IComparable<T>
    {
        return operatorStr switch
        {
            ">" => ValueTypeConditionResolver<T>.Operator.GreaterThan,
            "<" => ValueTypeConditionResolver<T>.Operator.LessThan,
            "==" => ValueTypeConditionResolver<T>.Operator.Equal,
            ">=" => ValueTypeConditionResolver<T>.Operator.GreaterThanOrEqual,
            "≥" => ValueTypeConditionResolver<T>.Operator.GreaterThanOrEqual,
            "<=" => ValueTypeConditionResolver<T>.Operator.LessThanOrEqual,
            "≤" => ValueTypeConditionResolver<T>.Operator.LessThanOrEqual,
            "equals" => ValueTypeConditionResolver<T>.Operator.Equal,
            "in" => ValueTypeConditionResolver<T>.Operator.InRange,
            "!in" => ValueTypeConditionResolver<T>.Operator.OutOfRange,
            _ => throw new ArgumentException($"Invalid operator {operatorStr} for value type.")
        };
    }

    private static BoolConditionResolver.Operator ParseBoolOperator(string operatorStr)
    {
        return operatorStr switch
        {
            "is" => BoolConditionResolver.Operator.Is,
            "==" => BoolConditionResolver.Operator.Is,
            "equals" => BoolConditionResolver.Operator.Is,
            //"isFalse" => BoolConditionNode.Operator.IsFalse,
            _ => throw new ArgumentException($"Invalid operator {operatorStr} for bool type.")
        };
    }

    private static StringConditionResolver.Operator ParseStringOperator(string operatorStr)
    {
        return operatorStr switch
        {
            "c" => StringConditionResolver.Operator.Contains,
            "nc" => StringConditionResolver.Operator.DoesNotContain,
            "sw" => StringConditionResolver.Operator.StartsWith,
            "ew" => StringConditionResolver.Operator.EndsWith,

            "contains" => StringConditionResolver.Operator.Contains,
            "doesNotContain" => StringConditionResolver.Operator.DoesNotContain,
            "equals" => StringConditionResolver.Operator.Equal,
            "==" => StringConditionResolver.Operator.Equal,
            "notEquals" => StringConditionResolver.Operator.NotEqual,
            "!=" => StringConditionResolver.Operator.NotEqual,
            "startsWith" => StringConditionResolver.Operator.StartsWith,
            "endsWith" => StringConditionResolver.Operator.EndsWith,
            _ => throw new ArgumentException($"Invalid operator {operatorStr} for string type.")
        };
    }
}
