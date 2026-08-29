#nullable disable

using ScriptLang.Generator.Extensions;
using ScriptLang.Generator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace ScriptLang.Generator;

/// <summary>
/// 脚本原型生成器
/// </summary>
[Generator]
public class ScriptPrototypeGenerator : IIncrementalGenerator
{


    /// <summary>
    /// 初始化生成器，定义需要执行的生成逻辑。
    /// </summary>
    /// <param name="context">增量生成器的上下文，用于注册生成逻辑</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
#if DEBUG
        if (GeneratorConfig.IsDebugScriptPrototypeToolkits) Debugger.Launch(); // 用于调试源生成器
#endif
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource("ScriptPrototypeToolkits.g.cs", SourceText.From(_attributeText, Encoding.UTF8)));


        var classDeclarations = context.SyntaxProvider
                                       .CreateSyntaxProvider(Predicate, Transform)
                                       .Where(x => x != null);
                                       //.Collect();


        // 注册一个源生成任务，使用找到的类生成代码
        context.RegisterSourceOutput(classDeclarations, GeneratorCode);
    }




    /// <summary>
    ///  分析这些类
    /// </summary>
    /// <param name="node"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private static bool Predicate(SyntaxNode node, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return false;
        }

        return node is ClassDeclarationSyntax;
    }

    /// <summary>
    /// 创建缓存
    /// </summary>
    /// <param name="context"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private ClassCache Transform(GeneratorSyntaxContext context, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return null;
        }
        var semanticModel = context.SemanticModel;
        if (context.Node is ClassDeclarationSyntax classDeclaration
            && semanticModel.GetDeclaredSymbol(classDeclaration) is INamedTypeSymbol classSymbol)
        {
            
            var classCache = new ClassCache(classDeclaration, classSymbol);

            classCache.BuildCacheOfClass(classSymbol, context, (info, attr) =>
            {
                var attributeName = attr.AttributeClass?.Name;
            });
            var isPrototypeExtension = classCache.Cache.ContainsAttr<PrototypeExtensionAttribute>();
            if (isPrototypeExtension)
            {
                return classCache;
            }
        }
        return null;
    }

    /// <summary>
    /// 生成代码
    /// </summary>
    /// <param name="context"></param>
    /// <param name="classCaches"></param>
    private static void GeneratorCode(SourceProductionContext context, ClassCache classCaches)
    {
        try
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                return;
            }

            var generatedFileName = $"{classCaches.Type.Name}.g.cs";
            var generatedCode = classCaches.GenerateCode(context);
            context.AddSource(generatedFileName, SourceText.From(generatedCode, Encoding.UTF8));
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine($"无法生成 '{classCaches.Type.FullName}' 代码，异常 ： {ex.Message} ");
        }

    }

    private const string _attributeText =
        """
        using System;

        namespace ScriptLang
        {
           /// <summary> 定义原型扩展的属性 </summary>
           [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
           internal sealed class PrototypeExtensionAttribute : Attribute
           {
               /// <summary> 是否将该类作为参数传递给方法（适用于需要拓展 Value 类型方法）</summary>
               public bool PushThis = false;

               /// <summary> 生成的属性、方法命名风格（如果属性、方法存在别名，会忽略此设置） </summary>
               public NamingFormat NamingFormat = NamingFormat.Net;
           }

           /// <summary> 命名风格 </summary>
           public enum NamingFormat
           {
               /// <summary> 首字母大写 </summary>
               Net,
               /// <summary> 首字母小写 </summary>
               Js,
           }
        
            /// <summary> 定义原型方法的属性 </summary>
            [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
            internal sealed class PrototypePropertyAttribute : Attribute
            {
                /// <summary>重新定义属性名称</summary>
            #nullable enable
                public string? Name = default;
            }
        
            /// <summary> 定义原型方法的属性 </summary>
            [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
            internal sealed class PrototypeFunctionAttribute : Attribute
            {
                /// <summary>重新定义方法名称</summary>
             #nullable enable
                public string? Name = default;
            }
        }
        """;
}
