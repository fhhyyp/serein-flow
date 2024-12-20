using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    /// <summary>
    /// 循环条件节点
    /// </summary>
    public class WhileNode : ASTNode
    {
        public ASTNode Condition { get; }
        public List<ASTNode> Body { get; }
        public WhileNode(ASTNode condition, List<ASTNode> body) => (Condition, Body) = (condition, body);
    }

}
