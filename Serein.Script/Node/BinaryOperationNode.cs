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
        /// <summary>
        /// 左元
        /// </summary>
        public ASTNode Left { get; }

        /// <summary>
        /// 操作符（布尔运算符 > 比较运算符 > 加减乘除 ）
        /// </summary>
        public string Operator { get; }

        /// <summary>
        /// 右元
        /// </summary>
        public ASTNode Right { get; }

        public BinaryOperationNode(ASTNode left, string op, ASTNode right)
        {
            Left = left;
            Operator = op;
            Right = right;
        }

        public override string ToString()
        {
            return $"({Left} {Operator} {Right})";
        }
    }
}
