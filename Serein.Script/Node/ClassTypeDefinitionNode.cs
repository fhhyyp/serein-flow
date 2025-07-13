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
        [Obsolete("此属性已经过时，可能在下一个版本中移除", false)] 
        public bool IsOverlay { get; set; }

        /// <summary>
        /// 类名称
        /// </summary>
        public string ClassName { get; }

        /// <summary>
        /// 字段名称及字段类型
        /// </summary>
        [Obsolete("此属性已经过时，将会改为Dictionary<string, string>", false)] 
        public Dictionary<string, Type> Fields { get; }
        
        /// <summary>
        /// 字段名称及字段类型(Kvp[fididName:fidleTypeName])
        /// </summary>
        public Dictionary<string, string> FieldInfos { get; }

        public ClassTypeDefinitionNode(Dictionary<string, string> fields, string className)
        {
            this.FieldInfos = fields;
            this.ClassName = className;
        }

        [Obsolete("此构造方法已经过时，可能在下一个版本中移除", false)]
        public ClassTypeDefinitionNode(Dictionary<string, Type> fields, string className)
        {
            this.Fields = fields;
            this.ClassName = className;
        }

        [Obsolete("此构造方法已经过时，可能在下一个版本中移除", false)]
        public ClassTypeDefinitionNode(Dictionary<string, Type> fields, string className, bool isOverlay)
        {
            this.Fields = fields;
            this.ClassName = className;
            IsOverlay = isOverlay;
        }
    }

}
