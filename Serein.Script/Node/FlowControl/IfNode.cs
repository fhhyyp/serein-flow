using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node.FlowControl
{
    /// <summary>
    /// 条件节点
    /// </summary>
    public class IfNode : ASTNode
    {
        /// <summary>
        /// 条件来源
        /// </summary>
        public ASTNode Condition { get; }

        /// <summary>
        /// 条件为 true 时所执行的语句
        /// </summary>
        public List<ASTNode> TrueBranch { get; }

        /// <summary>
        /// 条件为 false 时所执行的语句
        /// </summary>
        public List<ASTNode> FalseBranch { get; }
        public IfNode(ASTNode condition, List<ASTNode> trueBranch, List<ASTNode> falseBranch)
            => (Condition, TrueBranch, FalseBranch) = (condition, trueBranch, falseBranch);
    }

}
