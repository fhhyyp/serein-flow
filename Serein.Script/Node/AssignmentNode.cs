using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{

    /// <summary>
    /// 变量节点
    /// </summary>
    public class AssignmentNode : ASTNode
    {
        public string Variable { get; }
        public ASTNode Value { get; }
        public AssignmentNode(string variable, ASTNode value) => (Variable, Value) = (variable, value);
    }


}
