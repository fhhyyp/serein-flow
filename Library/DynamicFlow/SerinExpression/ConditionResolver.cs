using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Serein.DynamicFlow.SerinExpression
{

    public abstract class ConditionResolver
    {
        public abstract bool Evaluate(object obj);
    }

    public class PassConditionResolver : ConditionResolver
    {
        public Operator Op { get; set; }
        public override bool Evaluate(object obj)
        {
            return Op switch
            {
                Operator.Pass => true,
                Operator.NotPass => false,
                _ => throw new NotSupportedException("不支持的条件类型")
            };
        }

        public enum Operator
        {
            Pass,
            NotPass,
        }

    }

    public class ValueTypeConditionResolver<T> : ConditionResolver where T : struct, IComparable<T>
    {
        public enum Operator
        {
            /// <summary>
            /// 不进行任何操作
            /// </summary>
            Node,
            /// <summary>
            /// 大于
            /// </summary>
            GreaterThan,
            /// <summary>
            /// 小于
            /// </summary>
            LessThan,
            /// <summary>
            /// 等于
            /// </summary>
            Equal,
            /// <summary>
            /// 大于或等于
            /// </summary>
            GreaterThanOrEqual,
            /// <summary>
            /// 小于或等于
            /// </summary>
            LessThanOrEqual,
            /// <summary>
            /// 在两者之间
            /// </summary>
            InRange,
            /// <summary>
            /// 不在两者之间
            /// </summary>
            OutOfRange
        }

        public Operator Op { get; set; }
        public T Value { get; set; }
        public T RangeStart { get; set; }
        public T RangeEnd { get; set; }

        public string ArithmeticExpression { get; set; }


        public override bool Evaluate(object obj)
        {
            if (obj is T typedObj)
            {
                double numericValue = Convert.ToDouble(typedObj);
                if (!string.IsNullOrEmpty(ArithmeticExpression))
                {
                    numericValue = SerinArithmeticExpressionEvaluator.Evaluate(ArithmeticExpression, numericValue);
                }

                T evaluatedValue = (T)Convert.ChangeType(numericValue, typeof(T));

                return Op switch
                {
                    Operator.GreaterThan => evaluatedValue.CompareTo(Value) > 0,
                    Operator.LessThan => evaluatedValue.CompareTo(Value) < 0,
                    Operator.Equal => evaluatedValue.CompareTo(Value) == 0,
                    Operator.GreaterThanOrEqual => evaluatedValue.CompareTo(Value) >= 0,
                    Operator.LessThanOrEqual => evaluatedValue.CompareTo(Value) <= 0,
                    Operator.InRange => evaluatedValue.CompareTo(RangeStart) >= 0 && evaluatedValue.CompareTo(RangeEnd) <= 0,
                    Operator.OutOfRange => evaluatedValue.CompareTo(RangeStart) < 0 || evaluatedValue.CompareTo(RangeEnd) > 0,
                    _ => throw new NotSupportedException("不支持的条件类型")
                };
                /* switch (Op)
                {
                    case Operator.GreaterThan:
                        return evaluatedValue.CompareTo(Value) > 0;
                    case Operator.LessThan:
                        return evaluatedValue.CompareTo(Value) < 0;
                    case Operator.Equal:
                        return evaluatedValue.CompareTo(Value) == 0;
                    case Operator.GreaterThanOrEqual:
                        return evaluatedValue.CompareTo(Value) >= 0;
                    case Operator.LessThanOrEqual:
                        return evaluatedValue.CompareTo(Value) <= 0;
                    case Operator.InRange:
                        return evaluatedValue.CompareTo(RangeStart) >= 0 && evaluatedValue.CompareTo(RangeEnd) <= 0;
                    case Operator.OutOfRange:
                        return evaluatedValue.CompareTo(RangeStart) < 0 || evaluatedValue.CompareTo(RangeEnd) > 0;
                }*/
            }
            return false;
        }
    }

    public class BoolConditionResolver : ConditionResolver
    {
        public enum Operator
        {
            /// <summary>
            /// 是
            /// </summary>
            Is
        }

        public Operator Op { get; set; }
        public bool Value { get; set; }

        public override bool Evaluate(object obj)
        {

            if (obj is bool boolObj)
            {
                return boolObj == Value;
                /*switch (Op)
                {
                    case Operator.Is:
                        return boolObj == Value;
                }*/
            }
            return false;
        }
    }

    public class StringConditionResolver : ConditionResolver
    {
        public enum Operator
        {
            /// <summary>
            /// 出现过
            /// </summary>
            Contains,
            /// <summary>
            /// 没有出现过
            /// </summary>
            DoesNotContain,
            /// <summary>
            /// 相等
            /// </summary>
            Equal,
            /// <summary>
            /// 不相等
            /// </summary>
            NotEqual,
            /// <summary>
            /// 起始字符串等于
            /// </summary>
            StartsWith,
            /// <summary>
            /// 结束字符串等于
            /// </summary>
            EndsWith
        }

        public Operator Op { get; set; }

        public string Value { get; set; }


        public override bool Evaluate(object obj)
        {
            if (obj is string strObj)
            {
                return Op switch
                {
                    Operator.Contains => strObj.Contains(Value),
                    Operator.DoesNotContain => !strObj.Contains(Value),
                    Operator.Equal => strObj == Value,
                    Operator.NotEqual => strObj != Value,
                    Operator.StartsWith => strObj.StartsWith(Value),
                    Operator.EndsWith => strObj.EndsWith(Value),
                    _ => throw new NotSupportedException("不支持的条件类型"),
                };

               /* switch (Op)
                {
                    case Operator.Contains:
                        return strObj.Contains(Value);
                    case Operator.DoesNotContain:
                        return !strObj.Contains(Value);
                    case Operator.Equal:
                        return strObj == Value;
                    case Operator.NotEqual:
                        return strObj != Value;
                    case Operator.StartsWith:
                        return strObj.StartsWith(Value);
                    case Operator.EndsWith:
                        return strObj.EndsWith(Value);
                }*/
            }
            return false;
        }
    }
    public class MemberConditionResolver<T> : ConditionResolver where T : struct, IComparable<T>
    {
        //public string MemberPath { get; set; }
        public ValueTypeConditionResolver<T>.Operator Op { get; set; }
        public object? TargetObj { get; set; }
        public T Value { get; set; }

        public string ArithmeticExpression { get; set; }

        public override bool Evaluate(object? obj)
        {
            //object? memberValue = GetMemberValue(obj, MemberPath);
            if (TargetObj is T typedObj)
            {
                return new ValueTypeConditionResolver<T>
                {
                    Op = Op,
                    Value = Value,
                    ArithmeticExpression = ArithmeticExpression,
                }.Evaluate(typedObj);
            }
            return false;
        }

        //private object? GetMemberValue(object? obj, string memberPath)
        //{
        //    string[] members = memberPath[1..].Split('.');
        //    foreach (var member in members)
        //    {
        //        if (obj == null) return null;
        //        Type type = obj.GetType();
        //        PropertyInfo? propertyInfo = type.GetProperty(member);
        //        FieldInfo? fieldInfo = type.GetField(member);
        //        if (propertyInfo != null)
        //            obj = propertyInfo.GetValue(obj);
        //        else if (fieldInfo != null)
        //            obj = fieldInfo.GetValue(obj);
        //        else
        //            throw new ArgumentException($"Member {member} not found in type {type.FullName}");
        //    }
        //    return obj;
        //}
    }

    public class MemberStringConditionResolver : ConditionResolver
    {

        public string MemberPath { get; set; }

        public StringConditionResolver.Operator Op { get; set; }

        public string Value { get; set; }


        public override bool Evaluate(object obj)
        {
            object memberValue = GetMemberValue(obj, MemberPath);
            if (memberValue is string strObj)
            {
                return new StringConditionResolver
                {
                    Op = Op,
                    Value = Value
                }.Evaluate(strObj);
            }
            return false;
        }

        private object GetMemberValue(object? obj, string memberPath)
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





    }

}
