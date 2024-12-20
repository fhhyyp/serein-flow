using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    /// <summary>
    /// 动态类型定义
    /// </summary>
    public class ClassTypeDefinitionNode : ASTNode
    {
        public string ClassName { get; }
        public Dictionary<string, Type> Fields { get; }

        public ClassTypeDefinitionNode(Dictionary<string, Type> fields, string className)
        {
            this.Fields = fields;
            this.ClassName = className;
        }
    }

}
