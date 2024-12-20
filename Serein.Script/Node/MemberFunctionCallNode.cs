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
        public ASTNode Object { get; }
        public string FunctionName { get; }
        public List<ASTNode> Arguments { get; }

        public MemberFunctionCallNode(ASTNode @object, string functionName, List<ASTNode> arguments)
        {
            Object = @object;
            FunctionName = functionName;
            Arguments = arguments;
        }
    }
}
