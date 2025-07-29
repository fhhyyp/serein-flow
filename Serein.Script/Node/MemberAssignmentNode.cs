using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    /// <summary>
    /// 表示对对象成员的赋值
    /// </summary>
    public class MemberAssignmentNode : ASTNode
    {
        /// <summary>
        /// 对象来源
        /// </summary>
        public ASTNode Object { get; }

        /// <summary>
        /// 对象中要赋值的成员的名称
        /// </summary>
        public string MemberName { get; }
        /// <summary>
        /// 值来源
        /// </summary>
        public ASTNode Value { get; }

        public MemberAssignmentNode(ASTNode obj, string memberName, ASTNode value)
        {
            Object = obj;
            MemberName = memberName;
            Value = value;
        }

        public override string ToString()
        {
            return $"{Object}.{MemberName} = {Value}";
        }
    }
}
