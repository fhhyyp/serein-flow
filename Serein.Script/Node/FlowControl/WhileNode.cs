using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node.FlowControl
{
    /// <summary>
    /// 循环条件节点
    /// </summary>
    public class WhileNode : ASTNode
    {
        /// <summary>
        /// 循环条件值来源
        /// </summary>
        public ASTNode Condition { get; }

        /// <summary>
        /// 循环中语句块
        /// </summary>
        public List<ASTNode> Body { get; }
        public WhileNode(ASTNode condition, List<ASTNode> body) => (Condition, Body) = (condition, body);
    }

}
