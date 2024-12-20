using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    /// <summary>
    /// 条件节点
    /// </summary>
    public class IfNode : ASTNode
    {
        public ASTNode Condition { get; }
        public List<ASTNode> TrueBranch { get; }
        public List<ASTNode> FalseBranch { get; }
        public IfNode(ASTNode condition, List<ASTNode> trueBranch, List<ASTNode> falseBranch)
            => (Condition, TrueBranch, FalseBranch) = (condition, trueBranch, falseBranch);
    }

}
