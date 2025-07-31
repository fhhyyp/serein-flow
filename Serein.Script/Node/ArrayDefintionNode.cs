using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    /// <summary>
    /// 数组定义节点
    /// <para>arr = [1, 2, 3, 4, 5];</para>
    /// <para>arr = ["A","B","C"];</para>
    /// </summary>
    internal class ArrayDefintionNode : ASTNode
    {
        /// <summary>
        /// 数组子项
        /// </summary>
        public List<ASTNode> Elements { get; } = new List<ASTNode>();

        public ArrayDefintionNode(List<ASTNode> elements)
        {
            Elements = elements;
        }

    }
}
