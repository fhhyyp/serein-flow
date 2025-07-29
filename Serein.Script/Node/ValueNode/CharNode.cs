using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    internal class CharNode : ASTNode
    {
        public char Value { get; }
        public CharNode(string value)
        {
            Value = char.Parse(value);
        }
        public override string ToString()
        {
            return $"'{Value}'";
        }
    }
}
