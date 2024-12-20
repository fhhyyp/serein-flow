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
        public ASTNode Object { get; }
        public string MemberName { get; }
        public ASTNode Value { get; }

        public MemberAssignmentNode(ASTNode obj, string memberName, ASTNode value)
        {
            Object = obj;
            MemberName = memberName;
            Value = value;
        }
    }
}
