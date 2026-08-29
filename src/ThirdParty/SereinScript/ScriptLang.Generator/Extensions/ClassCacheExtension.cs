#nullable disable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ScriptLang.Generator.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ScriptLang.Generator.Extensions
{
    internal static class ClassCacheExtension
    {
        /// <summary>
        /// 构建类的特性缓存信息
        /// </summary>
        /// <param name="classCache"></param>
        /// <param name="classSymbol"></param>
        /// <param name="context"></param>
        /// <param name="customHandle"></param>
        /// <returns></returns>
        internal static void BuildCacheOfClass(this ClassCache classCache, INamedTypeSymbol classSymbol,
            GeneratorSyntaxContext context, Action<AttrInfo, AttributeData> customHandle = null)
        {

            var fieldDeclarations = classCache.Syntax.Members.OfType<FieldDeclarationSyntax>();
            var properryDeclarations = classCache.Syntax.Members.OfType<PropertyDeclarationSyntax>();
            var methodDeclarations = classCache.Syntax.Members.OfType<MethodDeclarationSyntax>();

            classCache.BuildCacheOfField(context.SemanticModel, fieldDeclarations);
            classCache.BuildCacheOfProperty(context.SemanticModel, properryDeclarations);
            classCache.BuildCacheOfMethod(context.SemanticModel, methodDeclarations);

            var attrs = classSymbol.GetAttributes();
            foreach (var attr in attrs)
            {
                var typeFullname = attr.AttributeClass.ToDisplayString(GeneratorConfig.GlobalFullTypeFormat);
                //var attributeName = attr.AttributeClass?.Name;
                var info = new AttrInfo(typeFullname);

                customHandle?.Invoke(info, attr);

                foreach (var nameArg in attr.NamedArguments)
                {
                    var key = nameArg.Key;
                    if (nameArg.Value.Kind == TypedConstantKind.Array)
                    {
                        var value = nameArg.Value.Values;
                        info.AddMember(key, value);

                    }
                    else
                    {

                        var value = nameArg.Value.Value;
                        info.AddMember(key, value);
                    }

                }
                classCache.Cache.AddInfo(info);
            }

            var className = classSymbol.Name;
            var classType = classSymbol.ToDisplayString(GeneratorConfig.GlobalFullTypeFormat);
            //classCache.ClassFullName = classType;
            classCache.Type = new TypeCache(className, classType);

            //return attributesOfClass;
        }


        internal static void BuildCacheOfMethod(
                this ClassCache classCache,
                SemanticModel semanticModel,
                IEnumerable<MethodDeclarationSyntax> methodDeclarationSyntaxes)
        {
            foreach (MethodDeclarationSyntax syntax in methodDeclarationSyntaxes)
            {
                
                // 属性的访问修饰符
                var accessibility = syntax.GetAccessibility();
                // 是否为静态方法
                var isStatic = syntax.Modifiers.Any(SyntaxKind.StaticKeyword);
                // 是否为分部方法
                var isPartial = syntax.Modifiers.Any(SyntaxKind.PartialKeyword);
                // 是否为抽象方法
                var isAbstract = syntax.Modifiers.Any(SyntaxKind.AbstractKeyword);
                // 是否为重写的方法
                var isOverride = syntax.Modifiers.Any(SyntaxKind.OverrideKeyword);
                // 是否为可被重写的方法
                var isVirtual = syntax.Modifiers.Any(SyntaxKind.VirtualKeyword);
                // 是否隐藏父类成员
                var isNew = syntax.Modifiers.Any(SyntaxKind.NewKeyword);
                // 是否为异步方法
                var isAsync = syntax.Modifiers.Any(SyntaxKind.AsyncKeyword);
                // 方法名称
                var methodName = syntax.Identifier.Text;
                // 返回值类型信息
                var returnType = GetTypeCache(syntax.ReturnType, semanticModel);

                // 获取方法入参
                var paramsCaches = syntax.ParameterList.Parameters.Select(p => new ParameterCache
                {
                    Syntax = p,
                    Name = p.Identifier.Text,
                    Type = GetTypeCache(p.Type, semanticModel),
                    IsIn = p.Modifiers.Any(SyntaxKind.InKeyword),
                    IsOut = p.Modifiers.Any(SyntaxKind.OutKeyword),
                    IsRef = p.Modifiers.Any(SyntaxKind.RefKeyword),
                    IsParams = p.Modifiers.Any(SyntaxKind.ParamsKeyword),
                    DefaultValue = p.Default?.Value.ToString() ?? null,
                }).ToList();

                MethodCache methodCache = new MethodCache(paramsCaches)
                {
                    Syntax = syntax,
                    AccessibilityType = accessibility,
                    IsStatic = isStatic,
                    IsPartial = isPartial,
                    IsAbstract = isAbstract,
                    IsOverride = isOverride,
                    IsVirtual = isVirtual,
                    IsNew = isNew,
                    IsAsync = isAsync,
                    Name = methodName,
                    ReturnType = returnType,
                };

                classCache.AddItem(methodCache)
                          .LoadAttributeCache(semanticModel, syntax.AttributeLists);

            }
        }



        /// <summary>
        /// <para>构建字段的缓存信息</para>
        /// <para>第1层：字段名称 - 特性集合</para>
        /// <para>第2层：特性名称 - 特性属性集合</para>
        /// <para>第3层：特性属性名称 - 对应的字面量</para>
        /// </summary>
        /// <param name="semanticModel"></param>
        /// <param name="classCache"></param>
        /// <param name="fieldDeclarationSyntaxes"></param>
        /// <returns>关于字段的特性缓存信息</returns>
        internal static void BuildCacheOfField(this ClassCache classCache,
            SemanticModel semanticModel,
            IEnumerable<FieldDeclarationSyntax> fieldDeclarationSyntaxes)
        {
            foreach (FieldDeclarationSyntax syntax in fieldDeclarationSyntaxes)
            {
                // 获取类型 Symbol（关键）
                var typeSyntax = syntax.Declaration.Type;
                var typeSymbol = semanticModel.GetTypeInfo(syntax.Declaration.Type).Type;
                if (typeSymbol == null)
                {
                    continue;
                }

                // 是否为可空类型
                var isNullableValueType = typeSyntax is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

                foreach (VariableDeclaratorSyntax variable in syntax.Declaration.Variables)
                {

                    // 属性的访问修饰符
                    var accessibility = syntax.GetAccessibility();
                    // 是否为静态字段
                    var isStatic = syntax.Modifiers.Any(SyntaxKind.StaticKeyword); //semanticModel.GetDeclaredSymbol(variable).IsStatic,
                    // 是否为只读字段
                    var isReadOnly = syntax.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);
                    // 是否为常量字段
                    var isConst = syntax.Modifiers.Any(SyntaxKind.ConstKeyword);
                    // 字段类型
                    var typeCache = GetTypeCache(typeSyntax, semanticModel);
                    // 字段名
                    var fieldName = variable.Identifier.Text;
                    // 默认值（如果有）
                    var defaultValue = variable.Initializer?.Value?.ToString() ?? null;




                    var cache = new FieldCache
                    {
                        FieldSyntax = syntax,
                        VariableSyntax = variable,
                        AccessibilityType = accessibility,
                        Name = fieldName,
                        DefaultValue = defaultValue,
                        Type = typeCache,
                        IsNullable = isNullableValueType,
                        IsConst = isConst,
                        IsStatic = isStatic,
                        IsReadOnly = isReadOnly,
                    };

                    classCache.AddItem(cache)
                              .LoadAttributeCache(semanticModel, syntax.AttributeLists);

                }
            }
        }


        /// <summary>
        /// <para>构建属性的缓存信息</para>
        /// <para>第1层：属性名称 - 特性集合</para>
        /// <para>第2层：特性名称 - 特性属性集合</para>
        /// <para>第3层：特性属性名称 - 对应的字面量</para>
        /// </summary>
        /// <param name="semanticModel"></param>
        /// <param name="classCache"></param>
        /// <param name="propertyDeclarationSyntaxes"></param>
        /// <returns>关于属性的特性缓存信息</returns>
        internal static void BuildCacheOfProperty(
                this ClassCache classCache,
                SemanticModel semanticModel,
                IEnumerable<PropertyDeclarationSyntax> propertyDeclarationSyntaxes)
        {
            foreach (PropertyDeclarationSyntax syntax in propertyDeclarationSyntaxes)
            {
                // 忽略只读属性（无 set）
                if (syntax.AccessorList == null ||
                    !syntax.AccessorList.Accessors.Any(a => a.Kind() == SyntaxKind.SetAccessorDeclaration))
                {
                    continue;
                }
                // 获取类型 Symbol（关键）
                var typeSyntax = syntax.Type;

                // 属性的访问修饰符
                var accessibility = syntax.GetAccessibility();
                // 是否为静态属性
                var isStatic = syntax.Modifiers.Any(SyntaxKind.StaticKeyword);
                // 是否为分部属性
                var isPartial = syntax.Modifiers.Any(SyntaxKind.PartialKeyword);
                // 是否为抽象属性
                var isAbstract = syntax.Modifiers.Any(SyntaxKind.AbstractKeyword);
                // 是否为重写的属性
                var isOverride = syntax.Modifiers.Any(SyntaxKind.OverrideKeyword);
                // 是否为可被重写的属性
                var isVirtual = syntax.Modifiers.Any(SyntaxKind.VirtualKeyword);
                // 是否隐藏父类成员
                var isNew = syntax.Modifiers.Any(SyntaxKind.NewKeyword);
                // 属性类型
                var typeCache = GetTypeCache(typeSyntax, semanticModel);
                // 是否为可空类型
                bool isNullableValueType = typeSyntax is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
                // 属性名
                var propertyName = syntax.Identifier.Text;
                // 默认值（如果有）
                var defaultValue = syntax.Initializer?.Value?.ToString() ?? null;


                // 获取 getter 和 setter 访问器及它们的访问修饰符
                var getter = syntax.AccessorList.Accessors.FirstOrDefault(a => a.Kind() == SyntaxKind.GetAccessorDeclaration);
                var setter = syntax.AccessorList.Accessors.FirstOrDefault(a => a.Kind() == SyntaxKind.SetAccessorDeclaration);
                var getterAccessibility = getter.GetGetterSetterAccessibility();
                var setterAccessibility = setter.GetGetterSetterAccessibility();



                // 注册到 ClassCache
                var cache = new PropertyCache
                {
                    Syntax = syntax,
                    AccessibilityType = accessibility,
                    Name = propertyName,
                    DefaultValue = defaultValue,
                    Type = typeCache,
                    IsNullable = isNullableValueType,
                    IsStatic = isStatic,
                    IsPartial = isPartial,
                    IsAbstract = isAbstract,
                    IsOverride = isOverride,
                    IsVirtual = isVirtual,
                    IsNew = isNew,
                    HasGetter = getter != null,
                    HasSetter = setter != null,
                    GetterAccessibilty = getterAccessibility,
                    SetterAccessibilty = setterAccessibility,
                };

                classCache.AddItem(cache)
                          .LoadAttributeCache(semanticModel, syntax.AttributeLists);

            }
        }

        /// <summary>
        /// 加载特性信息
        /// </summary>
        /// <param name="cache"></param>
        /// <param name="semanticModel"></param>
        /// <param name="attributes"></param>
        private static void LoadAttributeCache(this ItemCacheBase cache, SemanticModel semanticModel, SyntaxList<AttributeListSyntax> attributes)
        {
            // 处理属性特性
            if (attributes.Count == 0)
            {
                return;
            }

            // 加载属性特性
            foreach (var attributeList in attributes)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var info = LoadFieldSyntax(attribute, semanticModel);
                    cache.AttrsCache.AddInfo(info);
                }
            }
        }

        /// <summary>
        /// 加载特性属性
        /// </summary>
        /// <param name="attribute"></param>
        /// <param name="semanticModel"></param>
        /// <returns></returns>
        private static AttrInfo LoadFieldSyntax(AttributeSyntax attribute, SemanticModel semanticModel)
        {
            var typeFullname = semanticModel.GetTypeInfo(attribute).Type.ToDisplayString(GeneratorConfig.GlobalFullTypeFormat);


            AttrInfo info = new AttrInfo(typeFullname);

            var arguments = attribute.ArgumentList?.Arguments;
            if (arguments == null || arguments.Value.Count == 0)
            {
                return info;
            }

            // 解析命名属性
            foreach (var argument in arguments)
            {
                var memberName = argument.NameEquals?.Name?.ToString();
                if (string.IsNullOrEmpty(memberName))
                {
                    continue;
                }
                var expr = argument.Expression;

                object value = TryGetValue(expr, semanticModel);
                info.AddMember(memberName, value);
            }
            return info;
        }

        /// <summary>
        /// 获取表达式的值，支持 nameof()、typeof()、字面量和其它常量表达式
        /// </summary>
        /// <param name="expr"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        private static object TryGetValue(ExpressionSyntax expr, SemanticModel model)
        {
            // 处理 nameof()
            if (expr is InvocationExpressionSyntax invocation &&
                invocation.Expression is IdentifierNameSyntax id &&
                id.Identifier.Text == "nameof")
            {
                var constant = model.GetConstantValue(expr);
                if (constant.HasValue)
                    return constant.Value;
            }

            // 处理 typeof()
            if (expr is TypeOfExpressionSyntax typeOfExpr)
            {
                var typeInfo = model.GetTypeInfo(typeOfExpr.Type);
                return typeInfo.Type?.ToDisplayString();
            }

            // 字面量
            if (expr is LiteralExpressionSyntax literal)
            {
                return literal.Token.Value;
            }

            // 其它的常量表达式
            var cv = model.GetConstantValue(expr);
            if (cv.HasValue)
                return cv.Value;

            // fallback
            return expr.ToString();
        }


        /// <summary>
        /// 获取类型符号对应的类型名称和全名，并封装到 TypeCache 中
        /// </summary>
        /// <param name="typeSyntax"></param>
        /// <param name="semanticModel"></param>
        /// <returns></returns>
        internal static TypeCache GetTypeCache(this TypeSyntax typeSyntax, SemanticModel semanticModel)
        {
            var name = typeSyntax.ToName(semanticModel);  
            var fullName = typeSyntax.ToFullName(semanticModel);  
            return new TypeCache(name, fullName);
        }


        /// <summary>
        /// 获取类型符号对应的类型全名
        /// </summary>
        /// <param name="typeSyntax">类型符号</param>
        /// <param name="semanticModel">语法模型</param>
        /// <param name="isGlobal">是否包含全局命名空间（包含 global 关键字）</param>
        /// <returns></returns>
        internal static string ToFullName(this TypeSyntax typeSyntax, SemanticModel semanticModel, bool isGlobal = true)
        {
            var typeInfo = semanticModel.GetTypeInfo(typeSyntax);
            var type = typeInfo.Type;
            var fullName = isGlobal switch
            {
                true => type.ToDisplayString(GeneratorConfig.GlobalFullTypeFormat),
                _ => type.ToDisplayString(GeneratorConfig.FullTypeFormat),
            };
            return fullName;
        }

        /// <summary>
        /// 获取类型符号对应的类型全名
        /// </summary>
        /// <param name="typeSyntax">类型符号</param>
        /// <param name="semanticModel">语法模型</param>
        /// <returns></returns>
        internal static string ToName(this TypeSyntax typeSyntax, SemanticModel semanticModel)
        {
            var typeInfo = semanticModel.GetTypeInfo(typeSyntax);
            var type = typeInfo.Type;
            var name = type.Name;
            return name;
        }

        /// <summary>
        /// 实现INPC接口
        /// </summary>
        /// <param name="generator"></param>
        /// <returns></returns>
        internal static GeneratorCache<T> GeneratorINPC<T>(this GeneratorCache<T> generator) where T : ClassCache
        {
            generator.AppendCode("public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;");
            generator.AppendCode("protected bool SetProperty<T>(ref T storage, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)");
            generator.AppendCode("{");
            generator.IncreaseTab();
            generator.AppendCode("if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(storage, value)) return false;");
            generator.AppendCode("storage = value;");
            generator.AppendCode("OnPropertyChanged(propertyName);");
            generator.AppendCode("return true;");
            generator.DecreaseTab();
            generator.AppendCode("}");
            generator.AppendCode("public void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));");
            generator.AppendCode("");
            return generator;
        }
    }
}