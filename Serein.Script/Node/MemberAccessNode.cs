using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    /// <summary>
    /// 表示对象的成员访问
    /// </summary>
    public class MemberAccessNode : ASTNode
    {
        public ASTNode Object { get; }
        public string MemberName { get; }

        public MemberAccessNode(ASTNode obj, string memberName)
        {
            Object = obj;
            MemberName = memberName;
        }
    }
}
