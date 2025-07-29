using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    /// <summary>
    /// 类型创建
    /// </summary>
    public class ObjectInstantiationNode : ASTNode
    {
        /// <summary>
        /// 类型来源
        /// </summary>
        public TypeNode Type { get; }

        /// <summary>
        /// 构造方法的参数来源
        /// </summary>
        public List<ASTNode> Arguments { get; }

        /// <summary>
        /// 构造器赋值
        /// </summary>
        public List<CtorAssignmentNode> CtorAssignments { get; private set; } = [];

        public ObjectInstantiationNode(TypeNode type, List<ASTNode> arguments)
        {
            this.Type = type;
            this.Arguments = arguments;
        }

        public ObjectInstantiationNode SetCtorAssignments(List<CtorAssignmentNode> ctorAssignments)
        {
            CtorAssignments = ctorAssignments;
            return this;
        }

        public override string ToString()
        {
            var arg = string.Join(",", Arguments.Select(p => $"{p}"));
            var ctor_arg = string.Join(",", CtorAssignments.Select(p => $"{p}"));
            return $"new {Type}({arg}){ctor_arg}";
        }
    }

}
