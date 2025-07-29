using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{


    /// <summary>
    /// 构造器对对象成员赋值
    /// </summary>
    public class CtorAssignmentNode : ASTNode
    {
        /// <summary>
        /// 成员来源类型
        /// </summary>
        public TypeNode Class { get; }

        /// <summary>
        /// 成员名称
        /// </summary>
        public string MemberName { get; }

        /// <summary>
        /// 值来源
        /// </summary>
        public ASTNode Value { get; }

        /// <summary>
        /// 构造器赋值
        /// </summary>
        /// <param name="typeNode">成员来源类型</param>
        /// <param name="memberName">成员名称</param>
        /// <param name="value">成员值来源</param>
        public CtorAssignmentNode(TypeNode typeNode, string memberName, ASTNode value)
        {
            Class = typeNode;
            MemberName = memberName;
            Value = value;
        }

        public override string ToString()
        {
            return $"ctor {Class}.{MemberName} = {Value}";
        }
    }
}
