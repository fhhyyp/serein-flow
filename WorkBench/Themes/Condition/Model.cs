using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicDemo.Themes.Condition
{
    //public class IntConditionNode : ConditionNode
    //{
    //    public int Value { get; set; }
    //    public int MinValue { get; set; }
    //    public int MaxValue { get; set; }
    //    public List<int> ExcludeValues { get; set; }

    //    public override bool Evaluate(object value)
    //    {
    //        if (value is int intValue)
    //        {
    //            switch (Condition)
    //            {
    //                case ConditionType.GreaterThan:
    //                    return intValue > Value;
    //                case ConditionType.LessThan:
    //                    return intValue < Value;
    //                case ConditionType.EqualTo:
    //                    return intValue == Value;
    //                case ConditionType.Between:
    //                    return intValue >= MinValue && intValue <= MaxValue;
    //                case ConditionType.NotBetween:
    //                    return intValue < MinValue || intValue > MaxValue;
    //                case ConditionType.NotInRange:
    //                    return !ExcludeValues.Contains(intValue);
    //                default:
    //                    return false;
    //            }
    //        }
    //        return false;
    //    }
    //}

    //public class BoolConditionNode : ConditionNode
    //{
    //    public override bool Evaluate(object value)
    //    {
    //        if (value is bool boolValue)
    //        {
    //            switch (Condition)
    //            {
    //                case ConditionType.IsTrue:
    //                    return boolValue;
    //                case ConditionType.IsFalse:
    //                    return !boolValue;
    //                default:
    //                    return false;
    //            }
    //        }
    //        return false;
    //    }
    //}


    //public class StringConditionNode : ConditionNode
    //{
    //    public string Substring { get; set; }

    //    public override bool Evaluate(object value)
    //    {
    //        if (value is string stringValue)
    //        {
    //            switch (Condition)
    //            {
    //                case ConditionType.Contains:
    //                    return stringValue.Contains(Substring);
    //                case ConditionType.DoesNotContain:
    //                    return !stringValue.Contains(Substring);
    //                case ConditionType.IsNotEmpty:
    //                    return !string.IsNullOrEmpty(stringValue);
    //                default:
    //                    return false;
    //            }
    //        }
    //        return false;
    //    }
    //}


}
