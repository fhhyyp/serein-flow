using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Tool.SereinExpression.Resolver
{
    public class MemberStringConditionResolver : SereinConditionResolver
    {
        public string MemberPath { get; set; }

        public StringConditionResolver.Operator Op { get; set; }

        public string Value { get; set; }


        public override bool Evaluate(object obj)
        {
            object memberValue;
            if (!string.IsNullOrWhiteSpace(MemberPath)) 
            {
                memberValue = GetMemberValue(obj, MemberPath);
            }
            else
            {
                memberValue = obj;
            }
            
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

        private object GetMemberValue(object obj, string memberPath)
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
