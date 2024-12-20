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
        public string Name { get; }
        public IdentifierNode(string name) => Name = name;
    }
}
