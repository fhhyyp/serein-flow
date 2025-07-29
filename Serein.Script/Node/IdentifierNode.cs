using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    /// <summary>
    /// 标识符（变量）
    /// </summary>
    public class IdentifierNode : ASTNode
    {
        /// <summary>
        /// 定义的名称
        /// </summary>
        public string Name { get; }
        public IdentifierNode(string name) => Name = name;


        public override string ToString()
        {
            return $"let {Name}";
        }
    }
}
