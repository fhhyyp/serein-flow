using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    internal class UsingNode : ASTNode
    {
        public string Namespace { get; }

        public UsingNode(string @namespace)
        {
            Namespace = @namespace;
        }

        public override string ToString()
        {
            return $"using Namespace";
        }
    }
}
