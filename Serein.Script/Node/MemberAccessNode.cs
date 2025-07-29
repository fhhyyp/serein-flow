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
        /// <summary>
        /// 对象来源
        /// </summary>
        public ASTNode Object { get; }

        /// <summary>
        /// 对象中要获取的成员的名称
        /// </summary>
        public string MemberName { get; }

        public MemberAccessNode(ASTNode obj, string memberName)
        {
            Object = obj;
            MemberName = memberName;
        }

        public override string ToString()
        {
            return $"{Object}.{MemberName}";
        }
    }
}
