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
        public ASTNode IndexValue { get; }
        public CollectionIndexNode(ASTNode indexValue)
        {
            this.IndexValue = indexValue;
        }
    }
}
