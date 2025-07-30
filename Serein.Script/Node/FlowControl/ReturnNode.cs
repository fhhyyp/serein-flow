using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node.FlowControl
{
    /// <summary>
    /// 返回值
    /// </summary>
    public class ReturnNode : ASTNode
    {
        /// <summary>
        /// 返回值来源
        /// </summary>
        public ASTNode? Value { get; }

        public ReturnNode(ASTNode returnNode)
        {
            Value = returnNode;
        }
        public ReturnNode()
        {
        }
    }

}
