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
        /// 作用的对象
        /// </summary>
        public ASTNode Object { get; }
        /// <summary>
        /// 被赋值的成员（属性/字段）名称
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
    }
}
