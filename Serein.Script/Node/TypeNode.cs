using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    /// <summary>
    /// 类型节点
    /// </summary>
    public class TypeNode : ASTNode
    {
        /// <summary>
        /// 类型名称
        /// </summary>
        public string TypeName { get;  }

        public TypeNode(string typeName)
        {
            TypeName = typeName;
        }

        public override string ToString()
        {
            return $"[type]{TypeName}";
        }
    }
}
