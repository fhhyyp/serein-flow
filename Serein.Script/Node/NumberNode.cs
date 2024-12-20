using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{


    /// <summary>
    /// 整数型字面量
    /// </summary>
    public class NumberNode : ASTNode
    {
        public int Value { get; }
        public NumberNode(int value) => Value = value;
    }


}
