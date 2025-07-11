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
        /// <summary>
        /// 类型名称
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// 构造方法的参数来源
        /// </summary>
        public List<ASTNode> Arguments { get; }
        public ObjectInstantiationNode(string typeName, List<ASTNode> arguments)
        {
            this.TypeName = typeName;
            this.Arguments = arguments;
        }
    }

}
