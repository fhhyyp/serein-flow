using Newtonsoft.Json.Linq;
using Serein.Library.Utils;
using Serein.NodeFlow.Tool.SereinExpression.Resolver;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Serein.NodeFlow.Tool.SereinExpression
{
    /// <summary>
    /// 字符串工具类
    /// </summary>
    public static class StringHelper
    {
        /// <summary>
        /// Net低版本无法引入Skip函数，所以使用扩展方法代替
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static IEnumerable<T> MySkip<T>(this IEnumerable<T> source, int count)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            int skipped = 0;
            foreach (var item in source)
            {
                if (skipped++ >= count)
                    yield return item;
            }
        }

        public static string JoinStrings(string[] parts, int startIndex, int count, char separator)
        {
            if (parts == null)
                throw new ArgumentNullException(nameof(parts));

            if (startIndex < 0 || startIndex >= parts.Length || count < 0 || startIndex + count > parts.Length)
                throw new ArgumentOutOfRangeException();

            // 复制需要的部分到新的数组
            string[] subArray = new string[count];
            Array.Copy(parts, startIndex, subArray, 0, count);

            // 使用 string.Join 连接
            return string.Join(separator.ToString(), subArray);
        }



    }


    public class SereinConditionParser
    {
        public static bool To<T>(T data, string expression)
        {
            try
            {
                if (string.IsNullOrEmpty(expression))
                {
                    return false;
                }
                var parse = ConditionParse(data, expression);
                var result = parse.Evaluate(data);
                return result;

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        public static SereinConditionResolver ConditionParse(object data, string expression)
        {
            if (expression.StartsWith(".")) // 表达式前缀属于从上一个节点数据对象获取成员值
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
        private static object GetMemberValue(object obj, string memberPath)
        {
            //string[] members = memberPath[1..].Split('.');
            string[] members = memberPath.Substring(1).Split('.');

            foreach (var member in members)
            {
                if (obj is null) return null;
                Type type = obj.GetType();
                PropertyInfo propertyInfo = type.GetProperty(member);
                FieldInfo fieldInfo = type.GetField(member);
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
        private static SereinConditionResolver ParseObjectExpression(object data, string expression)
        {
            var parts = expression.Split(' ');
            string operatorStr = parts[0]; // 获取操作类型
            string valueStr; //= string.Join(' ', parts, 1, parts.Length - 1);
            string memberPath;
            Type type;
            object targetObj;

            // 尝试获取指定类型
            int typeStartIndex = expression.IndexOf('<'); 
            int typeEndIndex = expression.IndexOf('>');
            if (typeStartIndex + typeStartIndex == -2)
            {
                // 如果不需要转为指定类型
                memberPath = operatorStr;
                targetObj = SerinExpressionEvaluator.Evaluate("@get " + operatorStr, data, out _);
                //targetObj = GetMemberValue(data, operatorStr);
                type = targetObj.GetType();
                operatorStr = parts[1].ToLower(); // 
                valueStr = string.Join(" ", parts.Skip(2));
            }
            else
            {
                // 类型语法不正确
                if (typeStartIndex >= typeEndIndex)
                {
                    throw new ArgumentException("无效的表达式格式");
                }
                memberPath = expression.Substring(0, typeStartIndex).Trim();
                string typeStr = expression.Substring(typeStartIndex + 1, typeEndIndex - typeStartIndex - 1)
                                            .Trim().ToLower(); // 手动置顶的类型

                // 对象取值表达式
                parts = expression.Substring(typeEndIndex + 1).Trim().Split(' ');
                if (parts.Length == 3)
                {
                    operatorStr = parts[1].ToLower(); // 操作类型
                    valueStr = string.Join(" ", parts.Skip(2)); // 表达式值
                }
                else
                {
                    operatorStr = parts[0].ToLower(); // 操作类型
                    valueStr = string.Join(" ", parts.Skip(1)); // 表达式值
                }
                Type tempType;

                switch (typeStr)
                {
                    case "bool":
                        tempType = typeof(bool);
                        break;
                    case "float":
                        tempType = typeof(float);
                        break;
                    case "decimal":
                        tempType = typeof(decimal);
                        break;
                    case "double":
                        tempType = typeof(double);
                        break;
                    case "sbyte":
                        tempType = typeof(sbyte);
                        break;
                    case "byte":
                        tempType = typeof(byte);
                        break;
                    case "short":
                        tempType = typeof(short);
                        break;
                    case "ushort":
                        tempType = typeof(ushort);
                        break;
                    case "int":
                        tempType = typeof(int);
                        break;
                    case "uint":
                        tempType = typeof(uint);
                        break;
                    case "long":
                        tempType = typeof(long);
                        break;
                    case "ulong":
                        tempType = typeof(ulong);
                        break;
                    // 如果需要支持 nint 和 nuint
                    // case "nint":
                    //     tempType = typeof(nint);
                    //     break;
                    // case "nuint":
                    //     tempType = typeof(nuint);
                    //     break;
                    case "string":
                        tempType = typeof(string);
                        break;
                    default:
                        tempType = Type.GetType(typeStr);
                        break;
                }
                type = tempType ?? throw new ArgumentException("对象表达式无效的类型声明");
                if (string.IsNullOrWhiteSpace(memberPath))
                {
                    targetObj = Convert.ChangeType(data, type);
                }
                else
                {
                    targetObj = GetMemberValue(data, memberPath);// 获取对象成员，作为表达式的目标对象
                }

            }

            #region 解析类型 int
            if (type.IsValueType)
            {
                //return GetValueResolver(type, valueStr, operatorStr, parts);
            }
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
                    return new MemberConditionResolver<int>
                    {
                        Op = op,
                        RangeStart = rangeStart,
                        RangeEnd = rangeEnd,
                        TargetObj = targetObj,
                        ArithmeticExpression = GetArithmeticExpression(parts[0]),
                    };
                }
                else
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
            }
            #endregion
            #region 解析类型 double
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
            #endregion
            #region 解析类型 bool
            else if (type == typeof(bool))
            {
                return new MemberConditionResolver<bool>
                {
                    //MemberPath = memberPath,
                    TargetObj = targetObj,
                    Op = (ValueTypeConditionResolver<bool>.Operator)ParseBoolOperator(operatorStr)
                };
            }
            #endregion
            #region 解析类型 string
            else if (type == typeof(string))
            {
                return new MemberStringConditionResolver
                {
                    MemberPath = memberPath,
                    Op = ParseStringOperator(operatorStr),
                    Value = valueStr
                };
            } 
            #endregion

            throw new NotSupportedException($"Type {type} is not supported.");
        }


        /// <summary>
        /// 条件表达式解析
        /// </summary>
        /// <param name="data"></param>
        /// <param name="expression"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        private static SereinConditionResolver ParseSimpleExpression(object data, string expression)
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

            string operatorStr;
            string valueStr;
            Type type = null;
            // 尝试获取指定类型
            int typeStartIndex = expression.IndexOf('<');
            int typeEndIndex = expression.IndexOf('>');
            if (typeStartIndex + typeStartIndex == -2)
            {
                // 如果不需要转为指定类型
                 operatorStr = parts[0];
#if NET8_0_OR_GREATER
                valueStr = string.Join(' ', parts, 1, parts.Length - 1);

#elif NET462_OR_GREATER

                valueStr = StringHelper.JoinStrings(parts, 1, parts.Length - 1, ' ');
#endif

                type = data.GetType();
            }
            else
            {//string typeStr = parts[0];
                string typeStr = expression.Substring(typeStartIndex + 1, typeEndIndex - typeStartIndex - 1)
                                            .Trim().ToLower(); // 手动置顶的类型
                parts = expression.Substring(typeEndIndex + 1).Trim().Split(' ');
                operatorStr = parts[0].ToLower(); // 操作类型
                valueStr = string.Join(" ", parts.Skip(1)); // 表达式值


                Type tempType;

                switch (typeStr)
                {
                    case "bool":
                        tempType = typeof(bool);
                        break;
                    case "float":
                        tempType = typeof(float);
                        break;
                    case "decimal":
                        tempType = typeof(decimal);
                        break;
                    case "double":
                        tempType = typeof(double);
                        break;
                    case "sbyte":
                        tempType = typeof(sbyte);
                        break;
                    case "byte":
                        tempType = typeof(byte);
                        break;
                    case "short":
                        tempType = typeof(short);
                        break;
                    case "ushort":
                        tempType = typeof(ushort);
                        break;
                    case "int":
                        tempType = typeof(int);
                        break;
                    case "uint":
                        tempType = typeof(uint);
                        break;
                    case "long":
                        tempType = typeof(long);
                        break;
                    case "ulong":
                        tempType = typeof(ulong);
                        break;
                    // 如果需要支持 nint 和 nuint
                    // case "nint":
                    //     tempType = typeof(nint);
                    //     break;
                    // case "nuint":
                    //     tempType = typeof(nuint);
                    //     break;
                    case "string":
                        tempType = typeof(string);
                        break;
                    default:
                        tempType = Type.GetType(typeStr);
                        break;
                }
            }


            if (type == typeof(bool))
            {
                bool value = bool.Parse(valueStr);
                return new BoolConditionResolver
                {
                    Op = ParseBoolOperator(operatorStr),
                    Value = value,
                };
            }
            else if (type.IsValueType)
            {
                return GetValueResolver(type, valueStr, operatorStr, parts);
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

        public static SereinConditionResolver GetValueResolver(Type valueType, string valueStr, string operatorStr, string[] parts)// where T : struct, IComparable<T>
        {
            SereinConditionResolver resolver;

            if (valueType == typeof(float))
            {
                resolver = GetValueResolver<float>(valueStr, operatorStr, parts);
            }
            else if (valueType == typeof(decimal))
            {
                resolver = GetValueResolver<decimal>(valueStr, operatorStr, parts);
            }
            else if (valueType == typeof(double))
            {
                resolver = GetValueResolver<double>(valueStr, operatorStr, parts);
            }
            else if (valueType == typeof(sbyte))
            {
                resolver = GetValueResolver<sbyte>(valueStr, operatorStr, parts);
            }
            else if (valueType == typeof(byte))
            {
                resolver = GetValueResolver<byte>(valueStr, operatorStr, parts);
            }
            else if (valueType == typeof(short))
            {
                resolver = GetValueResolver<short>(valueStr, operatorStr, parts);
            }
            else if (valueType == typeof(ushort))
            {
                resolver = GetValueResolver<ushort>(valueStr, operatorStr, parts);
            }
            else if (valueType == typeof(int))
            {
                resolver = GetValueResolver<int>(valueStr, operatorStr, parts);
            }
            else if (valueType == typeof(uint))
            {
                resolver = GetValueResolver<uint>(valueStr, operatorStr, parts);
            }
            else if (valueType == typeof(long))
            {
                resolver = GetValueResolver<long>(valueStr, operatorStr, parts);
            }
            else if (valueType == typeof(ulong))
            {
                resolver = GetValueResolver<ulong>(valueStr, operatorStr, parts);
            }
#if NET8_0_OR_GREATER
            else if (valueType == typeof(nint))
            {
                resolver = GetValueResolver<nint>(valueStr, operatorStr, parts);
            }
            else if (valueType == typeof(nuint))
            {
                resolver = GetValueResolver<nuint>(valueStr, operatorStr, parts);
            }
#endif
            else
            {
                throw new ArgumentException("非预期值类型");
            }

            return resolver;

        }


        private static ValueTypeConditionResolver<T> GetValueResolver<T>(string valueStr, string operatorStr, string[] parts)
            where T :struct, IComparable<T>
        {
            var op = ParseValueTypeOperator<T>(operatorStr);
            if (op == ValueTypeConditionResolver<T>.Operator.InRange || op == ValueTypeConditionResolver<T>.Operator.OutOfRange)
            {
                var temp = valueStr.Split('-');
                var leftNum = string.Empty;
                var rightNum = string.Empty;
                if (temp.Length < 2 || temp.Length > 4)
                {
                    throw new ArgumentException($"范围无效：{valueStr}。");
                }
                else if (temp.Length == 2)
                {
                    leftNum = temp[0];
                    rightNum = temp[1];
                }
                else if (temp.Length == 3)
                {
                    if (string.IsNullOrEmpty(temp[0]) 
                        && !string.IsNullOrEmpty(temp[1]) 
                        && !string.IsNullOrEmpty(temp[2]))
                    {
                        leftNum = "-" + temp[1];
                        rightNum = temp[2];
                    }
                    else
                    {
                        throw new ArgumentException($"范围无效：{valueStr}。");
                    }
                }
                else if (temp.Length == 4)
                {
                    if (string.IsNullOrEmpty(temp[0])
                        && !string.IsNullOrEmpty(temp[1])
                        && string.IsNullOrEmpty(temp[2])
                        && !string.IsNullOrEmpty(temp[3]))
                    {
                        leftNum = "-" + temp[1];
                        rightNum = temp[3];
                    }
                    else
                    {
                        throw new ArgumentException($"范围无效：{valueStr}。");
                    }
                }
               
                
                
                return new ValueTypeConditionResolver<T>
                {
                    Op = op,
                    RangeStart = leftNum.ToValueData<T>(),
                    RangeEnd = rightNum.ToValueData<T>(),
                    ArithmeticExpression = GetArithmeticExpression(parts[0]),
                };
            }
            else
            {
                return new ValueTypeConditionResolver<T>
                {
                    Op = op,
                    Value = valueStr.ToValueData<T>(),
                    ArithmeticExpression = GetArithmeticExpression(parts[0])
                };

            }
        } 
        //public static T ValueParse<T>(object value) where T : struct, IComparable<T>
        //{
        //    return (T)ValueParse(typeof(T), value);
        //}

        //public static object ValueParse(Type type, object value)
        //{

        //    string? valueStr = value.ToString();
        //    if (string.IsNullOrEmpty(valueStr))
        //    {
        //        throw new ArgumentException("value is null"); 
        //    }
        //    object result = type switch
        //    {
        //        Type t when t.IsEnum => Enum.Parse(type, valueStr),
        //        Type t when t == typeof(bool) => bool.Parse(valueStr),
        //        Type t when t == typeof(float) => float.Parse(valueStr, CultureInfo.InvariantCulture),
        //        Type t when t == typeof(decimal) => decimal.Parse(valueStr, CultureInfo.InvariantCulture),
        //        Type t when t == typeof(double) => double.Parse(valueStr, CultureInfo.InvariantCulture),
        //        Type t when t == typeof(sbyte) => sbyte.Parse(valueStr, CultureInfo.InvariantCulture),
        //        Type t when t == typeof(byte) => byte.Parse(valueStr, CultureInfo.InvariantCulture),
        //        Type t when t == typeof(short) => short.Parse(valueStr, CultureInfo.InvariantCulture),
        //        Type t when t == typeof(ushort) => ushort.Parse(valueStr, CultureInfo.InvariantCulture),
        //        Type t when t == typeof(int) => int.Parse(valueStr, CultureInfo.InvariantCulture),
        //        Type t when t == typeof(uint) => uint.Parse(valueStr, CultureInfo.InvariantCulture),
        //        Type t when t == typeof(long) => long.Parse(valueStr, CultureInfo.InvariantCulture),
        //        Type t when t == typeof(ulong) => ulong.Parse(valueStr, CultureInfo.InvariantCulture),
        //        Type t when t == typeof(nint) => nint.Parse(valueStr, CultureInfo.InvariantCulture),
        //        Type t when t == typeof(nuint) => nuint.Parse(valueStr, CultureInfo.InvariantCulture),
        //        _ => throw new ArgumentException("非预期值类型")
        //    };
        //    return result;
        //}



        /// <summary>
        /// 数值操作类型
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="operatorStr"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private static ValueTypeConditionResolver<T>.Operator ParseValueTypeOperator<T>(string operatorStr) where T : struct, IComparable<T>
        {
            if (operatorStr == ">")
            {
                return ValueTypeConditionResolver<T>.Operator.GreaterThan;
            }
            else if (operatorStr == "<")
            {
                return ValueTypeConditionResolver<T>.Operator.LessThan;
            }
            else if (operatorStr == "==")
            {
                return ValueTypeConditionResolver<T>.Operator.Equal;
            }
            else if (operatorStr == ">=" || operatorStr == "≥")
            {
                return ValueTypeConditionResolver<T>.Operator.GreaterThanOrEqual;
            }
            else if (operatorStr == "<=" || operatorStr == "≤")
            {
                return ValueTypeConditionResolver<T>.Operator.LessThanOrEqual;
            }
            else if (operatorStr == "in")
            {
                return ValueTypeConditionResolver<T>.Operator.InRange;
            }
            else if (operatorStr == "!in")
            {
                return ValueTypeConditionResolver<T>.Operator.OutOfRange;
            }
            else
            {
                throw new ArgumentException($"Invalid operator {operatorStr} for value type.");
            }

        }

        /// <summary>
        /// 布尔操作类型
        /// </summary>
        /// <param name="operatorStr"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private static BoolConditionResolver.Operator ParseBoolOperator(string operatorStr)
        {
            if (operatorStr == "is" || operatorStr == "==" || operatorStr == "equals")
            {
                return BoolConditionResolver.Operator.Is;
            }
            else
            {
                throw new ArgumentException($"Invalid operator {operatorStr} for bool type.");
            }

        }

        /// <summary>
        /// 字符串操作类型
        /// </summary>
        /// <param name="operatorStr"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private static StringConditionResolver.Operator ParseStringOperator(string operatorStr)
        {
            operatorStr = operatorStr.ToLower();

            if (operatorStr == "c" || operatorStr == "contains")
            {
                return StringConditionResolver.Operator.Contains;
            }
            else if (operatorStr == "nc" || operatorStr == "doesnotcontain")
            {
                return StringConditionResolver.Operator.DoesNotContain;
            }
            else if (operatorStr == "sw" || operatorStr == "startswith")
            {
                return StringConditionResolver.Operator.StartsWith;
            }
            else if (operatorStr == "ew" || operatorStr == "endswith")
            {
                return StringConditionResolver.Operator.EndsWith;
            }
            else if (operatorStr == "==" || operatorStr == "equals")
            {
                return StringConditionResolver.Operator.Equal;
            }
            else if (operatorStr == "!=" || operatorStr == "notequals")
            {
                return StringConditionResolver.Operator.NotEqual;
            }
            else
            {
                throw new ArgumentException($"Invalid operator {operatorStr} for string type.");
            }

        }

    }
}
