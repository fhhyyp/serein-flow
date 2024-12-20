using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    /// <summary>
    /// 二元表达式节点
    /// </summary>

    public class BinaryOperationNode : ASTNode
    {
        public ASTNode Left { get; }
        public string Operator { get; }
        public ASTNode Right { get; }

        public BinaryOperationNode(ASTNode left, string op, ASTNode right)
        {
            Left = left;
            Operator = op;
            Right = right;
        }
    }
}
