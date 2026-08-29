#nullable disable

using ScriptLang.Generator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScriptLang.Generator.Models
{

    internal class UsingCache
    {
        public string Namespace { get; }
        public bool IsStatic { get; }
        public bool IsAlias { get; }

        public UsingCache(string @namespace, bool isStatic, bool isAlias)
        {
            Namespace = @namespace;
            IsStatic = isStatic;
            IsAlias = isAlias;
        }
    }

    internal class ClassCache
    {
        public List<Action<SourceProductionContext>> SendGeneratorError { get; set; } = new List<Action<SourceProductionContext>>();
        public List<UsingCache> Namespaces { get; } = new List<UsingCache>();

        /// <summary>
        /// 类的语法节点
        /// </summary>
        public ClassDeclarationSyntax Syntax { get; }

        /// <summary>
        /// 类的符号信息
        /// </summary>
        public INamedTypeSymbol Symbol { get; }

        public ClassCache(ClassDeclarationSyntax classDeclaration, INamedTypeSymbol classSymbol)
        {

            var syntaxTree = classDeclaration.SyntaxTree;
            var root = syntaxTree.GetCompilationUnitRoot();
            foreach (var usingDirective in root.Usings)
            {
                var namespaceName = usingDirective.Name.ToString();
                string ns = usingDirective.Name?.ToString();

                bool isStatic = usingDirective.StaticKeyword != default;
                bool isAlias = usingDirective.Alias != null;
                Namespaces.Add(new UsingCache(namespaceName, isStatic, isAlias));
            }


            var className = classDeclaration.Identifier.Text;
            var @namespace = GeneratorHelper.GetNamespace(classDeclaration);


            Namespace = @namespace;
            //var className = GetClassNameWithGenerics(classDeclaration);
            //ClassName = className;
            Syntax = classDeclaration;
            Symbol = classSymbol;
        }

        private static string GetClassNameWithGenerics(ClassDeclarationSyntax cls)
        {
            if (cls.TypeParameterList == null)
                return cls.Identifier.Text;

            var genericParams = string.Join(
                ", ",
                cls.TypeParameterList.Parameters.Select(p => p.Identifier.Text)
            );

            return $"{cls.Identifier.Text}<{genericParams}>";
        }
        /// <summary>
        /// 所属命名空间
        /// </summary>
        public string Namespace { get; }

        /// <summary>
        /// 类型信息
        /// </summary>
        public TypeCache Type { get; set; }

        /// <summary>
        /// 类特性缓存
        /// </summary>
        public AttrCache Cache { get; } = new AttrCache();

        /// <summary>
        /// 类中成员符号缓存
        /// </summary>
        protected Dictionary<string, ItemCacheBase> ItemCaches { get; } = new Dictionary<string, ItemCacheBase>();

        /// <summary>
        /// 筛选获取某个成员
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public ItemCacheBase FirstOrDefault(Func<ItemCacheBase, bool> predicate) => ItemCaches.Values.FirstOrDefault(predicate);

        public bool ContainsMember(Func<ItemCacheBase, bool> predicate) => ItemCaches.Values.FirstOrDefault(predicate) != null;

        public  ItemCacheBase AddItem(ItemCacheBase info) 
        {
            if (ItemCaches.TryGetValue(info.Name, out var member))
            {
                return member;
            }
            else
            {
                ItemCaches.Add(info.Name, info);
                return info;
            }
        }

        public List<ItemCacheBase> GetItems() => ItemCaches.Values.ToList();

        public ItemCacheBase GetItem(string name) => ItemCaches[name];

        public override string ToString()
        {
            return $"{Namespace}.{Type.Name} : {Cache}";
        }
    }
}
