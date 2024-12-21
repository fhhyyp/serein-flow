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
        public bool IsOverlay { get; set; }
        public string ClassName { get; }
        public Dictionary<string, Type> Fields { get; }

        public ClassTypeDefinitionNode(Dictionary<string, Type> fields, string className, bool isOverlay)
        {
            this.Fields = fields;
            this.ClassName = className;
            IsOverlay = isOverlay;
        }
    }

}
