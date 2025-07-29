using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    /// <summary>
    /// 对象成员方法调用
    /// </summary>
    public class MemberFunctionCallNode : ASTNode
    {
        /// <summary>
        /// 对象来源
        /// </summary>
        public ASTNode Object { get; }

        /// <summary>
        /// 对象中要调用的方法的名称
        /// </summary>
        public string FunctionName { get; }

        /// <summary>
        /// 方法参数来源
        /// </summary>
        public List<ASTNode> Arguments { get; }

        public MemberFunctionCallNode(ASTNode @object, string functionName, List<ASTNode> arguments)
        {
            Object = @object;
            FunctionName = functionName;
            Arguments = arguments;
        }


        public override string ToString()
        {
            var p = string.Join(",", Arguments.Select(p => $"{p}"));
            return $"{Object}.{FunctionName}({p})";
        }
    }
}
