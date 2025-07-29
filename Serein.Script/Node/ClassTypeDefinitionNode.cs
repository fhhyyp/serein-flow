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
        /// 类型名称
        /// </summary>
        public TypeNode ClassType { get; }

        /// <summary>
        /// 类型中的属性
        /// </summary>
        public Dictionary<string, TypeNode> Propertys { get; }

        public ClassTypeDefinitionNode(Dictionary<string, TypeNode> propertys, TypeNode className)
        {
            this.Propertys = propertys;
            this.ClassType = className;
        }

        public override string ToString()
        {
            var p = string.Join(",", Propertys.Select(p => $"{p.Value}"));
            return $"{ClassType}({p})";
        }


        /* /// <summary>
         /// 字段名称及字段类型
         /// </summary>
         [Obsolete("此属性已经过时，将会改为Dictionary<string, string>", false)] 
         public Dictionary<TypeNode, Type> Fields { get; }

 */


        /*  /// <summary>
          /// 字段名称及字段类型(Kvp[fididName:fidleTypeName])
          /// </summary>
          public Dictionary<TypeNode, string> FieldInfos { get; }
  */
        //[Obsolete("此构造方法已经过时，可能在下一个版本中移除", false)]

    }

}
