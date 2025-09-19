using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Serein.Library.NodeGenerator
{

    /// <summary>
    /// 增量源生成器
    /// </summary>
    [Generator]
    public class FlowDataPropertyGenerator : IIncrementalGenerator
    {
        internal static FlowDataPropertyAttribute FlowDataProperty = new FlowDataPropertyAttribute();
        internal static DataInfoAttribute DataInfo = new DataInfoAttribute();

        /// <summary>
        /// 初始化生成器，定义需要执行的生成逻辑。
        /// </summary>
        /// <param name="context">增量生成器的上下文，用于注册生成逻辑。</param>
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            /*
             *   //Debugger.Launch();
            CreateSyntaxProvider : 第一个参数用于筛选特定语法节点，第二个参数则用于转换筛选出来的节点。 
            SemanticModel : 通过 语义模型 (SemanticModel) 来解析代码中的符号信息，获取类、方法、属性等更具体的类型和特性信息。例如某个特性属于哪个类型。
            AddSource : 生成器的最终目标是生成代码。使用 AddSource 将生成的代码以字符串形式注入到编译过程当中。
             */
            // 通过 SyntaxProvider 查找所有带有任意特性修饰的类声明语法节点


            var classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    // 定义要查找的语法节点类型，这里我们只关心类声明 (ClassDeclarationSyntax) 并且它们有至少一个特性 (Attribute)
                    (node, _) => node is ClassDeclarationSyntax cds && cds.AttributeLists.Count > 0,

                    // 提供一个函数来进一步分析这些类，并且只返回带有 MyClassAttribute 特性的类声明
                    (tmpContext, _) =>
                    {
                        var classDeclaration = (ClassDeclarationSyntax)tmpContext.Node;
                        var semanticModel = tmpContext.SemanticModel;


                        // 检查类的特性列表，看看是否存在 MyClassAttribute
                        if (classDeclaration.AttributeLists
                            .SelectMany(attrList => attrList.Attributes)
                            .Any(attr => semanticModel.GetSymbolInfo(attr).Symbol?.ContainingType.Name == nameof(FlowDataPropertyAttribute)))
                        {
                            var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration); // 获取类的符号
                            var classInfo = classSymbol.BuildCacheOfClass();



                            return (classDeclaration, classInfo);
                        }
                        return (null, null);
                    })
                // 过滤掉空结果
                .Where(cds => cds.classDeclaration != null);

            // 注册一个源生成任务，使用找到的类生成代码
            context.RegisterSourceOutput(classDeclarations, (sourceProductionContext, result) =>
            {

                // 获取 MyDataAttribute 中的 Type 参数，可以获取多个，这里为了记录代码获取了第一个
                //var typeArgument = attributeData.ConstructorArguments.FirstOrDefault();
                //var dataType = typeArgument.Value as INamedTypeSymbol;
                //Console.WriteLine(dataType);
                if (result.classDeclaration is ClassDeclarationSyntax classSyntax)
                {
                    // 获取类的命名空间和类名
                    var namespaceName = GetNamespace(classSyntax);
                    var className = classSyntax.Identifier.Text;
                    Debug.WriteLine($"Generator Class Name : {className}");
                    // 生成属性代码
                    var generatedCode = GenerateProperties(classSyntax, result.classInfo, namespaceName, className);

                    // 将生成的代码添加为源文件
                    sourceProductionContext.AddSource($"{className}.g.cs", SourceText.From(generatedCode, Encoding.UTF8));
                }
            });
        }

        /// <summary>
        /// 为给定的类生成带有自定义 set 行为的属性。
        /// 
        /// </summary>
        /// <param name="classSyntax">类的语法树节点。</param>
        /// <param name="namespaceName">类所在的命名空间。</param>
        /// <param name="classInfo">。</param>
        /// <param name="className">类的名称。</param>
        /// <returns>生成的 C# 属性代码。</returns>
        private string GenerateProperties(ClassDeclarationSyntax classSyntax, 
                                          Dictionary<string, Dictionary<string, object>> classInfo, 
                                          string namespaceName, 
                                          string className)
        {
            var sb = new StringBuilder();

            // 生成命名空间和类的开始部分
            sb.AppendLine($"using System;");
            sb.AppendLine($"using System.Linq;");
            sb.AppendLine($"using System.Threading;");
            sb.AppendLine($"using System.Threading.Tasks;");
            sb.AppendLine($"using System.Collections.Concurrent;");
            sb.AppendLine($"using System.Collections.Generic;");
            sb.AppendLine($"using Serein.Library;");
            sb.AppendLine($"using Serein.Library.Api;");
            sb.AppendLine($"");
            sb.AppendLine($"namespace {namespaceName}"); // 命名空间
            sb.AppendLine("{");
            sb.AppendLine($"    public partial class {className} : global::System.ComponentModel.INotifyPropertyChanged"); // 类名
            sb.AppendLine("    {");

            //object path = null ;
            //if(classInfo.TryGetValue(nameof(NodePropertyAttribute), out var values))
            //{
            //    values.TryGetValue(nameof(NodePropertyAttribute.ValuePath), out path); // 获取路径
            //}

            //
            //

            // "ParameterDetails";
            // "MethodDetails";
            try
            {
                var expInfo = MyAttributeResolver.BuildCacheOfField(classSyntax.Members.OfType<FieldDeclarationSyntax>());
                foreach (var fieldKV in expInfo)
                {
                    var field = fieldKV.Key;
                    if (field.IsReadonly())
                    {
                        continue;
                    }

                    //var leadingTrivia = field.GetLeadingTrivia().InsertSummaryComment("（此属性为自动生成）").ToString(); // 获取注释
                    var fieldName = field.Declaration.Variables.First().Identifier.Text; // 获取字段名称
                    var fieldType = field.Declaration.Type.ToString(); // 获取字段类型
                    var propertyName = field.ToPropertyName(); // 转为合适的属性名称
                    var attributeInfo = fieldKV.Value; // 缓存的特性信息
                    var isProtection = attributeInfo.Search(nameof(DataInfo), nameof(DataInfo.IsProtection), value => bool.Parse(value)); // 是否为保护字段
                    var isVerify = attributeInfo.Search(nameof(DataInfo), nameof(DataInfo.IsVerify), value => bool.Parse(value)); // 是否为保护字段

                    //sb.AppendLine(leadingTrivia);
                    sb.AppendLine($"        partial void On{propertyName}Changed({fieldType} oldValue, {fieldType} newValue);");
                    sb.AppendLine($"        partial void On{propertyName}Changed({fieldType} value);");

                    if (isVerify)
                    {
                        sb.AppendLine($"        partial void BeforeThe{propertyName}(ref bool __isAllow, {fieldType} newValue);");
                    }

                    if (isProtection)
                    {
                        sb.AppendLine($"        private bool __{propertyName}ProtectionField = false;");
                    }

                    sb.AppendLine();
                    // 生成 getter / setter
                    sb.AppendLine($"        /// <inheritdoc cref=\"{fieldName}\"/>");
                    sb.AppendLine( "        [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]");
                    sb.AppendLine($"        public {fieldType} {propertyName}");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            get => {fieldName};"); // getter方法
                    sb.AppendLine( "            set");
                    sb.AppendLine( "            {");
                    //sb.AppendLine($"                if ({fieldName} {(isProtection ? "== default" : "!= value")})"); // 非保护的Setter
                    if (isVerify)
                    {
                        sb.AppendLine($"                bool __isAllow = true;");
                        sb.AppendLine($"                BeforeThe{propertyName}(ref __isAllow, value);");
                        sb.AppendLine($"                if(!__isAllow) return; // 修改验证失败");
                    }
                    sb.AppendLine($"                var __oldValue = {fieldName};");
                    if (isProtection)
                    {
                        sb.AppendLine($"                if (!__{propertyName}ProtectionField && SetProperty<{fieldType}>(ref {fieldName}, value))"); // 非保护的Setter
                        sb.AppendLine($"                {{");
                        sb.AppendLine($"                    __{propertyName}ProtectionField = true;");
                    }
                    else
                    {
                        sb.AppendLine($"                if (SetProperty<{fieldType}>(ref {fieldName}, value))"); // 非保护的Setter
                        sb.AppendLine($"                {{");
                    }

                    //sb.AppendLine($"                    SetProperty<{fieldType}>(ref {fieldName}, value); ");
                    sb.AppendLine($"                    On{propertyName}Changed(value);");
                    sb.AppendLine($"                    On{propertyName}Changed(__oldValue, value);");
                    if (attributeInfo.Search(nameof(DataInfo), nameof(DataInfo.IsPrint), value => bool.Parse(value))) 
                    {
                        sb.AddCode(5, $"Console.WriteLine({fieldName});");
                    } // 是否打印

                    // private void ValueChangedNotificationRemoteEnv(string nodeGuid, string nodeValuePath, string proprtyName, string fieldType, string otherData = null)

                    // NodeValuePath.Node  ： 作用在节点
                    // NodeValuePath.DebugSetting  ： 节点 → 参数信息 
                    // NodeValuePath.Method  ： 节点 → 方法描述 
                    // NodeValuePath.Parameter  ： 节点 → 方法描述 → 参数描述

                    if (attributeInfo.Search(nameof(DataInfo), nameof(DataInfo.IsNotification), value => bool.Parse(value))) // 是否通知
                    {

                        if (classInfo.ExitsPath(nameof(NodeValuePath.Node))) // 节点 or 自定义节点
                        {
                            sb.AddCode(5, $"if (this?.Env?.IsControlRemoteEnv == true) // 正在控制远程环境时才触发");
                            sb.AddCode(5, $"{{");
                            sb.AddCode(6, $"this.Env?.NotificationNodeValueChangeAsync(this.Guid, nameof({propertyName}), value); // 通知远程环境属性发生改变了");
                            sb.AddCode(5, $"}}");
                        }
                        else
                        {
                            sb.AddCode(5, $"if (NodeModel?.Env?.IsControlRemoteEnv == true) // 正在控制远程环境时才触发");
                            sb.AddCode(5, $"{{");
                            if (classInfo.ExitsPath(nameof(NodeValuePath.Method))) // 节点方法详情
                            {
                                sb.AddCode(6, $"NodeModel?.Env?.NotificationNodeValueChangeAsync(NodeModel.Guid, \"MethodDetails.\"+nameof({propertyName}), value); // 通知远程环境属性发生改变了");
                            }
                            else if (classInfo.ExitsPath(nameof(NodeValuePath.Parameter))) // 节点方法入参参数描述
                            {
                                sb.AddCode(6, "NodeModel?.Env?.NotificationNodeValueChangeAsync(NodeModel.Guid, \"MethodDetails.ParameterDetailss[\"+$\"{Index}\"+\"]." + $"\"+nameof({propertyName}),value); // 通知远程环境属性发生改变了");
                            }
                            else if (classInfo.ExitsPath(nameof(NodeValuePath.DebugSetting))) // 节点的调试信息
                            {
                                
                                sb.AddCode(6, $"NodeModel?.Env?.NotificationNodeValueChangeAsync(NodeModel.Guid, \"DebugSetting.\"+nameof({propertyName}), value); // 通知远程环境属性发生改变了");
                               
                            }
                            sb.AddCode(5, $"}}");
                        }

                    } // 是否通知

                    sb.AppendLine("                }");
                    sb.AppendLine("            }");
                    sb.AppendLine("        }"); // 属性的结尾大括号
                    //if (!isProtection && field.TryGetDefaultValue(out var defaultValue))
                    //{

                    //    sb.AppendLine($"        }} = {defaultValue}"); 
                    //}
                }


                var isNodeImp = false;

                if (classInfo.TryGetValue(nameof(FlowDataPropertyAttribute), out var values) 
                    && values.TryGetValue(nameof(FlowDataPropertyAttribute.IsNodeImp), out object data)
                    && bool.TryParse(data.ToString(), out var isNodeImpTemp)
                    && isNodeImpTemp)
                {
                    isNodeImp = true;
                }

                if (!isNodeImp)
                {
                    sb.AppendLine("        /// <summary>");
                    sb.AppendLine("        /// 略");
                    sb.AppendLine("        /// <para>此事件为自动生成</para>");
                    sb.AppendLine("        /// </summary>");
                    sb.AppendLine("        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;");

                    sb.AppendLine("        protected bool SetProperty<T>(ref T storage, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)                 ");
                    sb.AppendLine("        {                                                                                                                    ");
                    sb.AppendLine("            if (Equals(storage, value))                                                                                      ");
                    sb.AppendLine("            {                                                                                                                ");
                    sb.AppendLine("                return false;                                                                                                ");
                    sb.AppendLine("            }                                                                                                                ");
                    sb.AppendLine("                                                                                                                             ");
                    sb.AppendLine("            storage = value;                                                                                                 ");
                    sb.AppendLine("            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));                 ");
                    sb.AppendLine("            return true;                                                                                                     ");
                    sb.AppendLine("        }                                                                                                                    ");

                    sb.AppendLine("         public void OnPropertyChanged(string propertyName) =>                                                               ");
                    sb.AppendLine("                   PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));          ");
                    sb.AppendLine("                                                                                                                             ");
                    sb.AppendLine("                                                                                                                             ");
                    sb.AppendLine("                                                                                                                             ");

                }


               

                //sb.AppendLine("        /// <summary>                                                                                                        ");
                //sb.AppendLine("        /// 略                                                                                                               ");
                //sb.AppendLine("        /// <para>此方法为自动生成</para>                                                                                    "); 
                //sb.AppendLine("        /// </summary>");
                //sb.AppendLine("        /// <param name=\"propertyName\"></param>                                                                            ");
                //sb.AppendLine("                                                                                                                             ");
                //sb.AppendLine("        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)      "); 
                //sb.AppendLine("        {"); 
                //sb.AppendLine("            Console.WriteLine(\"测试:\"+ propertyName);"); 
                //sb.AppendLine("            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));                 "); 
                //sb.AppendLine("        }");

            }
            finally
            {
                // 生成类的结束部分
                sb.AppendLine("    }"); // 类的结尾大括号
                sb.AppendLine("}"); // 命名空间的结尾大括号
            }

            return sb.ToString(); // 返回生成的代码
        }


        /// <summary>
        /// 获取类所在的命名空间。
        /// </summary>
        /// <param name="classSyntax">类的语法节点。</param>
        /// <returns>命名空间的名称，或者 "GlobalNamespace" 如果没有命名空间声明。</returns>
        private string GetNamespace(SyntaxNode classSyntax)
        {
            // 查找最近的命名空间声明
            var namespaceDeclaration = classSyntax.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            return namespaceDeclaration?.Name.ToString() ?? "GlobalNamespace";
        }


       




    }

    /// <summary>
    /// 扩展方法，用于处理 XML 文档注释中的 summary 标签
    /// </summary>
    public static class DocumentationCommentExtensions
    {


        /// <summary>
        /// 为 XML 文档注释中的 summary 标签插入指定的文本
        /// </summary>
        /// <param name="triviaList">语法节点的 LeadingTrivia 或 TrailingTrivia 列表</param>
        /// <param name="comment">要插入的注释文本</param>
        /// <returns>修改后的 Trivia 列表</returns>
        public static SyntaxTriviaList InsertSummaryComment(this SyntaxTriviaList triviaList, string comment)
        {
            var docCommentTrivia = triviaList.FirstOrDefault(trivia =>
                trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

            if (docCommentTrivia.HasStructure)
            {
                //var structuredTrivia = docCommentTrivia.GetStructure();
                var structuredTrivia = docCommentTrivia.GetStructure() as StructuredTriviaSyntax;

                // 查找 <summary> 标签
                var summaryNode = structuredTrivia.DescendantNodes()
                    .OfType<XmlElementSyntax>()
                    .FirstOrDefault(e => e.StartTag.Name.LocalName.Text == "summary");

                if (summaryNode != null)
                {
                    //// 在 <summary> 标签内插入指定的注释文本
                    //var generatorComment = SyntaxFactory.XmlText(comment).WithLeadingTrivia(SyntaxFactory.Whitespace(" "));
                    //var updatedSummaryNode = summaryNode.AddContent(generatorComment);

                    //// 用新的 <summary> 标签替换原来的
                    //var updatedStructuredTrivia = structuredTrivia.ReplaceNode(summaryNode, updatedSummaryNode);

                    //// 用新的注释替换原来的
                    //var updatedTrivia = SyntaxFactory.Trivia(updatedStructuredTrivia);
                    //triviaList = triviaList.Replace(docCommentTrivia, updatedTrivia);

                    // 创建 <para> 段落注释
                    var paraElement = SyntaxFactory.XmlElement(
                        SyntaxFactory.XmlElementStartTag(SyntaxFactory.XmlName("para")),   // 起始标签 <para>
                        SyntaxFactory.SingletonList<XmlNodeSyntax>(                        // 内容
                            SyntaxFactory.XmlText(comment)
                        ),
                        SyntaxFactory.XmlElementEndTag(SyntaxFactory.XmlName("para"))     // 结束标签 </para>
                    );

                    // WithLeadingTrivia(SyntaxFactory.Whitespace(""))

                    // .AddContent(SyntaxFactory.XmlNewLine("123")

                    // 将 <para> 插入到 <summary> 中
                    var updatedSummaryNode = summaryNode.AddContent(paraElement).AddContent();


                    // 用新的 <summary> 标签替换原来的
                    var updatedStructuredTrivia = structuredTrivia.ReplaceNode(summaryNode, updatedSummaryNode);

                    // 用新的注释替换原来的 (确保转换为 StructuredTriviaSyntax 类型)
                    var updatedTrivia = SyntaxFactory.Trivia(updatedStructuredTrivia);
                    triviaList = triviaList.Replace(docCommentTrivia, updatedTrivia);
                }
            }

            return triviaList;
        }
    }


    /// <summary>
    /// MyAttributeResolver
    /// </summary>

    public static class MyAttributeResolver
    {
        /// <summary>
        /// 构建类的特性缓存信息
        /// </summary>
        /// <param name="classSymbol"></param>
        /// <returns></returns>
        public static Dictionary<string, Dictionary<string, object>> BuildCacheOfClass(this INamedTypeSymbol classSymbol)
        {
            Dictionary<string, Dictionary<string, object>> attributesOfClass = new Dictionary<string, Dictionary<string, object>>();
            var tattribute = classSymbol.GetAttributes();
            foreach (var cad in tattribute)
            {
                var attributeName = cad.AttributeClass?.Name;
                if (!attributesOfClass.TryGetValue(attributeName, out var attributeInfo))
                {
                    attributeInfo = new Dictionary<string, object>();
                    attributesOfClass.Add(attributeName, attributeInfo);
                }

                foreach (var cata in cad.NamedArguments)
                {
                    var key = cata.Key;
                    var value = cata.Value.Value;
                    if (nameof(FlowDataPropertyAttribute).Equals(attributeName))
                    {
                        if(cata.Key == nameof(FlowDataPropertyAttribute.ValuePath))
                        {
                            string literal = Enum.GetName(typeof(NodeValuePath), cata.Value.Value);
                            attributeInfo.Add(key, literal);
                        }
                        else if (cata.Key == nameof(FlowDataPropertyAttribute.IsNodeImp))
                        {
                            string literal = cata.Value.Value.ToString();
                            attributeInfo.Add(key, literal);
                        }
                        
                    }
                    else
                    {
                        attributeInfo.Add(key, value);
                    }


                    Debug.WriteLine("key   : " + cata.Key);// 类特性的属性名
                    Debug.WriteLine("value : " + cata.Value.Value); // 类特性的属性值
                }
            }
            return attributesOfClass;

        }



        /// <summary>
        /// 字段名称转换为属性名称
        /// </summary>
        /// <param name="field"></param>
        /// <returns>遵循属性命名规范的新名称</returns>
        public static string ToPropertyName(this FieldDeclarationSyntax field)
        {
            var fieldName = field.Declaration.Variables.First().Identifier.Text;
            var propertyName = fieldName.StartsWith("_") ? char.ToUpper(fieldName[1]) + fieldName.Substring(2) : char.ToUpper(fieldName[0]) + fieldName.Substring(1); // 创建属性名称
            return propertyName;
        }

        /// <summary>
        /// 判断字段是否有默认值
        /// </summary>
        /// <param name="field"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static bool TryGetDefaultValue(this FieldDeclarationSyntax field ,out string defaultValue)
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



        /// <summary>
        /// 判断字段是否为只读
        /// </summary>
        /// <param name="fieldDeclaration">字段的语法节点</param>
        /// <returns>如果字段是只读的，返回 true；否则返回 false</returns>
        public static bool IsReadonly(this FieldDeclarationSyntax fieldDeclaration)
        {
            // 判断字段是否有 readonly 修饰符
            return fieldDeclaration.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);
        }


        /// <summary>
        /// <para>构建字段的缓存信息</para>
        /// <para>第1层：字段名称 - 特性集合</para>
        /// <para>第2层：特性名称 - 特性属性集合</para>
        /// <para>第3层：特性属性名称 - 对应的字面量</para>
        /// </summary>
        /// <param name="fieldDeclarationSyntaxes"></param>
        /// <returns>关于字段的特性缓存信息</returns>
        public static Dictionary<FieldDeclarationSyntax, Dictionary<string, Dictionary<string, string>>> BuildCacheOfField(IEnumerable<FieldDeclarationSyntax> fieldDeclarationSyntaxes)
        {
            Dictionary<FieldDeclarationSyntax, Dictionary<string, Dictionary<string, string>>> FieldData = new Dictionary<FieldDeclarationSyntax, Dictionary<string, Dictionary<string, string>>>();
            foreach (var field in fieldDeclarationSyntaxes)
            {
                // 获取字段名称和类型
                var variable = field.Declaration.Variables.First();
                var fieldName = variable.Identifier.Text;
                var fieldType = field.Declaration.Type.ToString();

                var attributeInfo = new Dictionary<string, Dictionary<string, string>>(); // 发现一个新字段
                FieldData.Add(field, attributeInfo);


                var attributes = field.AttributeLists;
                foreach (var attributeList in attributes)
                {

                    // 解析特性参数
                    foreach (var attribute in attributeList.Attributes)
                    {
                        var attributeName = attribute.Name.ToString(); // 特性名称
                        var arguments = attribute.ArgumentList?.Arguments;
                        if (arguments == null)
                        {
                            continue;
                        }
                        var attributeValue = new Dictionary<string, string>();
                        attributeInfo.Add(attributeName, attributeValue); // 找到特性



                        // 解析命名属性
                        foreach (var argument in arguments)
                        {
                            // Console.WriteLine($" - Constructor Argument: {argument.ToString()}");
                            if (argument is AttributeArgumentSyntax attributeArgument && attributeArgument.NameEquals != null)
                            {
                                var propertyName = attributeArgument.NameEquals.Name.ToString();
                                var propertyValue = attributeArgument.Expression.ToString();
                                attributeValue.Add(propertyName, propertyValue); // 记录属性
                            }
                        }
                    }
                }
            }
            return FieldData;
        }

        /// <summary>
        /// <para>通过条件检查缓存的信息，决定是否添加代码</para>
        /// <para>首先检查是否存在该特性，如果不存在，返回 false。</para>
        /// <para>然后检查是否存在属性，如果不存在，返回 false</para>
        /// <para>如果存在属性，则返回属性对应的值与 comparisonValue 进行比较，返回</para>
        /// <para></para>
        /// <para>若只传入 attributeName 参数，则只会检查是否存在该特性</para>
        /// <para>若只传入 attributeName与attributePropertyName 参数，则只会检查是否存在该特性的该属性</para>
        /// </summary>
        /// <param name="dict">缓存的特性信息</param>
        /// <param name="attributeName">查询的特性名称</param>
        /// <param name="attributePropertyName">查询的特性属性名称</param>
        /// <param name="judgeFunc">比较方法</param>
        /// <returns>如果存在查询项，返回 true ，否则返回 false</returns>
        public static bool Search(this Dictionary<string, Dictionary<string, string>> dict,
            string attributeName = null,
            string attributePropertyName = null,
            Func<string, bool> judgeFunc = null)
        {
            if (string.IsNullOrWhiteSpace(attributeName))
                return false;
            if (!dict.TryGetValue(attributeName, out var abs))
                return false;

            if (string.IsNullOrWhiteSpace(attributePropertyName))
                return true;
            if (!abs.TryGetValue(attributePropertyName, out var absValue))
                return false;
            if (judgeFunc == null)
                return true;
            
            return judgeFunc.Invoke(absValue); ;
            //return absValue.Equals(comparisonValue);

        }

        /// <summary>
        ///  添加代码
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="retractCount">缩进次数（4个空格）</param>
        /// <param name="code">要添加的代码</param>
        /// <returns>字符串构建器本身</returns>
        public static StringBuilder AddCode(this StringBuilder sb,
            int retractCount = 0,
            string code = null)
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                var retract = new string(' ', retractCount * 4);
                sb.AppendLine(retract + code);
            }
            return sb;
        }


        /// <summary>
        /// 检查类信息中是否存在指定的路径
        /// </summary>
        /// <param name="classInfo"></param>
        /// <param name="valuePath"></param>
        /// <returns></returns>
        public static bool ExitsPath(this Dictionary<string, Dictionary<string, object>> classInfo, string valuePath)
        {
           
           if (!classInfo.TryGetValue(nameof(FlowDataPropertyAttribute), out var keyValuePairs))
            {
                return false;
            }

            if (!keyValuePairs.TryGetValue(nameof(FlowDataPropertyGenerator.FlowDataProperty.ValuePath), out var value))
            {
                return false;
            }

            return value.Equals(valuePath);
        }




    }
}
