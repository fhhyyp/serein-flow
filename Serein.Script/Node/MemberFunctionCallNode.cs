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
        /// 需要被调用的对象
        /// </summary>
        public ASTNode Object { get; }

        /// <summary>
        /// 被调用的方法名称
        /// </summary>
        public string FunctionName { get; }

        /// <summary>
        /// 方法参数
        /// </summary>
        public List<ASTNode> Arguments { get; }

        public MemberFunctionCallNode(ASTNode @object, string functionName, List<ASTNode> arguments)
        {
            Object = @object;
            FunctionName = functionName;
            Arguments = arguments;
        }
    }
}
