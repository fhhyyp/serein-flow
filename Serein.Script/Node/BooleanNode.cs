using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    /// <summary>
    /// 布尔字面量
    /// </summary>
    public class BooleanNode : ASTNode
    {
        public bool Value { get; }
        public BooleanNode(bool value) => Value = value;
    }
}
