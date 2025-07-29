using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    /// <summary>
    /// 集合索引获取
    /// </summary>
    public class CollectionIndexNode : ASTNode
    {
        /// <summary>
        /// 集合来源
        /// </summary>
        public ASTNode Collection { get; }

        /// <summary>
        /// 索引来源
        /// </summary>
        public ASTNode Index { get; }

        public CollectionIndexNode(ASTNode Collection, ASTNode indexValue)
        {
            this.Collection = Collection;
            this.Index = indexValue;
        }


        public override string ToString()
        {
            return $"{Collection}[{Index}]";
        }
    }

    /// <summary>
    /// 集合赋值节点
    /// </summary>
    public class CollectionAssignmentNode : ASTNode
    {
        /// <summary>
        /// 集合来源
        /// </summary>
        public CollectionIndexNode Collection { get; }

        /// <summary>
        /// 赋值值来源
        /// </summary>
        public ASTNode Value { get; }

        public CollectionAssignmentNode(CollectionIndexNode collection, ASTNode value)
        {
            this.Collection = collection;
            this.Value = value;
        }

        public override string ToString()
        {
            return $"{Collection} = {Value}";
        }
    }
}
