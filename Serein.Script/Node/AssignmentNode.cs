using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Serein.Script.Node
{

    /// <summary>
    /// 变量赋值节点
    /// </summary>
    public class AssignmentNode : ASTNode
    {
        /// <summary>
        /// 变量名称
        /// </summary>
        public ASTNode Target { get; }
        /// <summary>
        /// 值来源
        /// </summary>
        public ASTNode Value { get; }

        public AssignmentNode(ASTNode target, ASTNode value) => (Target, Value) = (target, value);

        public override string ToString()
        {
            return $"{Target} = {Value}";
        }


    }


}
