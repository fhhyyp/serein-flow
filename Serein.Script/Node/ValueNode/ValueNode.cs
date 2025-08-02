using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    public class ValueNode<T> : ASTNode
    {
        public T Value { get; protected set; }

        public ValueNode()
        {
            
        }
        public ValueNode(T value)
        {
            this.Value = value;
        }
        public override string ToString()
        {
            return $"{Value}";
        }
    }
}
