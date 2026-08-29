#nullable disable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ScriptLang.Generator.Models;
using System;
using System.Linq;

namespace ScriptLang.Generator.Extensions
{
    /// <summary>
    /// 工具类
    /// </summary>
    internal static class GeneratorHelper
    {
        /// <summary>
        /// 获取类所在的命名空间。
        /// </summary>
        /// <param name="classSyntax">类的语法节点。</param>
        /// <returns>命名空间的名称，或者 "GlobalNamespace" 如果没有命名空间声明。</returns>
        internal static string GetNamespace(SyntaxNode classSyntax)
        {
            // 查找最近的命名空间声明
            var namespaceDeclaration = classSyntax.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            return namespaceDeclaration?.Name.ToString() ?? "GlobalNamespace";
        }

        /// <summary>
        /// 字段名称转换为属性名称
        /// </summary>
        /// <returns>遵循属性命名规范的新名称</returns>
        public static string GetPropertyName(MemberCache member)
        {
            if (member is FieldCache field)
            {
                var fieldName = field.Name;
                var propertyName = fieldName.StartsWith("_")
                            ? char.ToUpper(fieldName[1]) + fieldName.Substring(2)  // 开头是下划线，去掉下划线并将下一个字符大写
                            : char.ToUpper(fieldName[0]) + fieldName.Substring(1); // 名称首字母大写
                return propertyName;
            }
            else
            {
                return member.Name;
            }
        }

        /// <summary> 生成首字母大写风格名称 </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetNetName(string name)
        {
            return char.ToUpper(name[0]) + name.Substring(1); // 名称首字母大写
        }
        /// <summary> 生成首字母小写风格名称 </summary>
        public static string GetJsName(string name)
        {
            return char.ToLower(name[0]) + name.Substring(1); // 名称首字母大写
        }

        /// <summary>
        /// 检查是否继承了某个类
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        internal static bool Inherits(this INamedTypeSymbol symbol, Func<INamedTypeSymbol, bool> func)
        {
            while (symbol != null)
            {
                if (func.Invoke(symbol)) // 如果继承了该类
                    return true;

                symbol = symbol.BaseType;
            }
            return false;
        }

        /// <returns></returns>
        /// <summary>
        /// 获取成员的访问修饰符
        /// </summary>
        public static AccessibilityType GetGetterSetterAccessibility(
            this AccessorDeclarationSyntax member)
        {
            var modifiers = member.Modifiers;
            
            if(modifiers.Count == 0)
            {
                // 没有访问修饰符，默认与属性相同
                return AccessibilityType.Undefined; // 使用 Nullable 表示与属性相同
            }

            // private protected
            if (modifiers.Any(SyntaxKind.PrivateKeyword) &&
                modifiers.Any(SyntaxKind.ProtectedKeyword))
            {
                return AccessibilityType.PrivateProtected;
            }

            // protected internal
            if (modifiers.Any(SyntaxKind.ProtectedKeyword) &&
                modifiers.Any(SyntaxKind.InternalKeyword))
            {
                return AccessibilityType.ProtectedInternal;
            }

            // public
            if (modifiers.Any(SyntaxKind.PublicKeyword))
            {
                return AccessibilityType.Public;
            }

            // private
            if (modifiers.Any(SyntaxKind.PrivateKeyword))
            {
                return AccessibilityType.Private;
            }

            // protected
            if (modifiers.Any(SyntaxKind.ProtectedKeyword))
            {
                return AccessibilityType.Protected;
            }

            // internal
            if (modifiers.Any(SyntaxKind.InternalKeyword))
            {
                return AccessibilityType.Internal;
            }

            // 默认可见性
            return AccessibilityType.Private;
        }

        /// <returns></returns>
        /// <summary>
        /// 获取成员的访问修饰符
        /// </summary>
        public static AccessibilityType GetAccessibility(
            this MemberDeclarationSyntax member)
        {
            var modifiers = member.Modifiers;

            // private protected
            if (modifiers.Any(SyntaxKind.PrivateKeyword) &&
                modifiers.Any(SyntaxKind.ProtectedKeyword))
            {
                return AccessibilityType.PrivateProtected;
            }

            // protected internal
            if (modifiers.Any(SyntaxKind.ProtectedKeyword) &&
                modifiers.Any(SyntaxKind.InternalKeyword))
            {
                return AccessibilityType.ProtectedInternal;
            }

            // public
            if (modifiers.Any(SyntaxKind.PublicKeyword))
            {
                return AccessibilityType.Public;
            }

            // private
            if (modifiers.Any(SyntaxKind.PrivateKeyword))
            {
                return AccessibilityType.Private;
            }

            // protected
            if (modifiers.Any(SyntaxKind.ProtectedKeyword))
            {
                return AccessibilityType.Protected;
            }

            // internal
            if (modifiers.Any(SyntaxKind.InternalKeyword))
            {
                return AccessibilityType.Internal;
            }

            // 默认可见性
            return AccessibilityType.Private;
        }

        public static string ToCodeString(this AccessibilityType accessibility)
        {
            return accessibility switch
            {
                AccessibilityType.Public => "public",
                AccessibilityType.Private => "private",
                AccessibilityType.Protected => "protected",
                AccessibilityType.Internal => "internal",
                AccessibilityType.ProtectedInternal => "protected internal",
                AccessibilityType.PrivateProtected => "private protected",
                AccessibilityType.Undefined => string.Empty,
                _ => string.Empty,
            };
        }


        /// <summary>
        /// 判断字段/属性是否有默认值
        /// </summary>
        /// <param name="menber"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        [Obsolete("已废弃",true)]
        public static bool TryGetDefaultValue<TMember>(this TMember menber, out string defaultValue) where TMember : MemberDeclarationSyntax
        {
            if (menber is FieldDeclarationSyntax field)
            {
                if (field.Declaration.Variables.First().Initializer != null)
                {
                    defaultValue = field.Declaration.Variables.First().Initializer.Value.ToString();
                    return true;
                }
                else
                {
                    defaultValue = null;
                    return false;
                }
            }
            else if (menber is PropertyDeclarationSyntax property)
            {
                if (property.Initializer != null)
                {
                    defaultValue = property.Initializer.Value.ToString();
                    return true;
                }
                else
                {
                    defaultValue = null;
                    return false;
                }
            }
            else
            {
                defaultValue = null;
                return false;
            }

        }


    }

}
