using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    /// <summary>
    /// 挂载函数调用
    /// </summary>
    public class FunctionCallNode : ASTNode
    {
        public string FunctionName { get; }
        public List<ASTNode> Arguments { get; }

        public FunctionCallNode(string functionName, List<ASTNode> arguments)
        {
            FunctionName = functionName;
            Arguments = arguments;
        }
    }

}
