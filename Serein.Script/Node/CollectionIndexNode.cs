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
        public CollectionIndexNode(ASTNode TargetValue,ASTNode indexValue)
        {
            this.Collection = TargetValue;
            this.Index = indexValue;
        }
    }
}
