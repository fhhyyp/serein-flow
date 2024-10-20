using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Utils.SereinExpression.Resolver
{
    public class MemberConditionResolver<T> : SereinConditionResolver where T : struct, IComparable<T>
    {
        //public string MemberPath { get; set; }
        public ValueTypeConditionResolver<T>.Operator Op { get; set; }
        public object TargetObj { get; set; }
        public T Value { get; set; }

        public string ArithmeticExpression { get; set; }
        public T RangeEnd { get; internal set; }
        public T RangeStart { get; internal set; }

        public override bool Evaluate(object obj)
        {
            //object? memberValue = GetMemberValue(obj, MemberPath);


            if (TargetObj is T typedObj)
            {
                return new ValueTypeConditionResolver<T>
                {
                    RangeStart = RangeStart,
                    RangeEnd = RangeEnd,
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

}
