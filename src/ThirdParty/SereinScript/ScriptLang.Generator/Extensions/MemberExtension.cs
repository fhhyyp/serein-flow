#nullable disable

using ScriptLang.Generator.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace ScriptLang.Generator.Extensions
{
    internal static class MemberExtension
    {
        extension(ItemCacheBase itemCache)
        {
            /// <summary>
            /// 获取符号在文档中的位置，方便定位问题
            /// </summary>
            public Microsoft.CodeAnalysis.Location Location => itemCache switch
            {
                FieldCache fieldCache => fieldCache.VariableSyntax.GetLocation(),
                PropertyCache propertyCache => propertyCache.Syntax.GetLocation(),
                _ => null
            };


        }
        extension(MemberCache member)
        {
            /// <summary>
            /// 是否能够生成属性，满足以下条件之一
            /// </summary>
            public bool IsAllowGenerationProperty => member switch
            {
                FieldCache fieldCache
                    when !fieldCache.IsConst && !fieldCache.IsReadOnly && !fieldCache.IsStatic
                        => true,
                PropertyCache propertyCache
                    when propertyCache.HasGetter && propertyCache.HasSetter && propertyCache.IsPartial
                        => true,
                _ => false
            };

        }
        
    }
}
