using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    /// <summary>
    /// 类型创建
    /// </summary>
    public class ObjectInstantiationNode : ASTNode
    {
        public string TypeName { get; }
        public List<ASTNode> Arguments { get; }
        public ObjectInstantiationNode(string typeName, List<ASTNode> arguments)
        {
            this.TypeName = typeName;
            this.Arguments = arguments;
        }
    }

}
