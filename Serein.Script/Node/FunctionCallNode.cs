using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    /// <summary>
    /// 挂载函数调用
    /// </summary>
    public class FunctionCallNode : ASTNode
    {
        /// <summary>
        /// 方法名称
        /// </summary>
        public string FunctionName { get; }

        /// <summary>
        /// 参数来源
        /// </summary>
        public List<ASTNode> Arguments { get; }

        public FunctionCallNode(string functionName, List<ASTNode> arguments)
        {
            FunctionName = functionName;
            Arguments = arguments;
        }

        public override string ToString()
        {
            var p = string.Join(",", Arguments.Select(p => $"{p}"));
            return $"{FunctionName}({p})";
        }
    }

}
