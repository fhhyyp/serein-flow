using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{

    /// <summary>
    /// 赋值节点
    /// </summary>
    public class AssignmentNode : ASTNode
    {
        /// <summary>
        /// 变量名称
        /// </summary>
        public string Variable { get; }
        /// <summary>
        /// 对应的节点
        /// </summary>
        public ASTNode Value { get; }

        public AssignmentNode(string variable, ASTNode value) => (Variable, Value) = (variable, value);
    }


}
