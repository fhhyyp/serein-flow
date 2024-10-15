using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Tool.SereinExpression.Resolver
{
    public class ValueTypeConditionResolver<T> : SereinConditionResolver where T : struct, IComparable<T>
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
              
            var evaluatedValue = obj.ToConvert<T>();
            if (!string.IsNullOrEmpty(ArithmeticExpression))
            {
                evaluatedValue = SerinArithmeticExpressionEvaluator<T>.Evaluate(ArithmeticExpression, evaluatedValue);
            }

            switch (Op)
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
            }
            return false;

            //if (obj is T typedObj)
            //{
            //     numericValue = Convert.ToDouble(typedObj);
            //     numericValue = Convert.ToDouble(obj);
            //    if (!string.IsNullOrEmpty(ArithmeticExpression))
            //    {
            //        numericValue = SerinArithmeticExpressionEvaluator.Evaluate(ArithmeticExpression, numericValue);
            //    }

            //    T evaluatedValue = (T)Convert.ChangeType(numericValue, typeof(T));

            //    /*return Op switch
            //    {
            //        Operator.GreaterThan => evaluatedValue.CompareTo(Value) > 0,
            //        Operator.LessThan => evaluatedValue.CompareTo(Value) < 0,
            //        Operator.Equal => evaluatedValue.CompareTo(Value) == 0,
            //        Operator.GreaterThanOrEqual => evaluatedValue.CompareTo(Value) >= 0,
            //        Operator.LessThanOrEqual => evaluatedValue.CompareTo(Value) <= 0,
            //        Operator.InRange => evaluatedValue.CompareTo(RangeStart) >= 0 && evaluatedValue.CompareTo(RangeEnd) <= 0,
            //        Operator.OutOfRange => evaluatedValue.CompareTo(RangeStart) < 0 || evaluatedValue.CompareTo(RangeEnd) > 0,
            //        _ => throw new NotSupportedException("不支持的条件类型")
            //    };*/
            //    switch (Op)
            //    {
            //        case Operator.GreaterThan:
            //            return evaluatedValue.CompareTo(Value) > 0;
            //        case Operator.LessThan:
            //            return evaluatedValue.CompareTo(Value) < 0;
            //        case Operator.Equal:
            //            return evaluatedValue.CompareTo(Value) == 0;
            //        case Operator.GreaterThanOrEqual:
            //            return evaluatedValue.CompareTo(Value) >= 0;
            //        case Operator.LessThanOrEqual:
            //            return evaluatedValue.CompareTo(Value) <= 0;
            //        case Operator.InRange:
            //            return evaluatedValue.CompareTo(RangeStart) >= 0 && evaluatedValue.CompareTo(RangeEnd) <= 0;
            //        case Operator.OutOfRange:
            //            return evaluatedValue.CompareTo(RangeStart) < 0 || evaluatedValue.CompareTo(RangeEnd) > 0;
            //    }
            //}
            //return false;
        }
    }

}
