using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Tool.SereinExpression.Resolver
{
    public class StringConditionResolver : SereinConditionResolver
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
                /*return Op switch
                {
                    Operator.Contains => strObj.Contains(Value),
                    Operator.DoesNotContain => !strObj.Contains(Value),
                    Operator.Equal => strObj == Value,
                    Operator.NotEqual => strObj != Value,
                    Operator.StartsWith => strObj.StartsWith(Value),
                    Operator.EndsWith => strObj.EndsWith(Value),
                    _ => throw new NotSupportedException("不支持的条件类型"),
                };*/

                switch (Op)
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
                    default:
                        throw new NotSupportedException("不支持的条件类型");
                }
            }
            return false;
        }
    }
}
