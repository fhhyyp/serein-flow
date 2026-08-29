#nullable disable

using Microsoft.CodeAnalysis;
using ScriptLang.Generator.Extensions;
using ScriptLang.Generator.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScriptLang.Generator;

internal static class ScriptPrototypeExtension
{

    private static readonly DiagnosticDescriptor PROTOTYPE001 = new DiagnosticDescriptor(id: "PROTOTYPE001",
                                        title: "缺少参数",
                                        messageFormat: "需要参数 '{0}'",
                                        category: "DiagnosticsGenerator",
                                        DiagnosticSeverity.Warning,
                                        isEnabledByDefault: true);

    /// <summary>
    /// 代码生成
    /// </summary>
    /// <param name="classCache"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    internal static string GenerateCode(this ClassCache classCache, SourceProductionContext context)
    {
        var sb = new StringBuilder();
        var generator = new GeneratorCache<ClassCache>(context, classCache, sb);
        var code = generator.GeneratorUsing() // 添加 using
                            .GeneratorClass()
                            .ToCode();
        return code;

    }

    private const string ScriptEngine = "global::ScriptLang.ScriptEngine";
    private const string ScriptValue = "global::ScriptLang.Runtime.Value";
    private const string RuntimeException = "global::ScriptLang.Runtime.RuntimeException";


    private const string ScriptNullValue = "global::ScriptLang.Runtime.NullValue";
    private const string ScriptNumberValue_int = "global::ScriptLang.Runtime.NumberValue<int>";
    private const string ScriptNumberValue_long = "global::ScriptLang.Runtime.NumberValue<long>";
    private const string ScriptNumberValue_float = "global::ScriptLang.Runtime.NumberValue<float>";
    private const string ScriptNumberValue_double = "global::ScriptLang.Runtime.NumberValue<double>";
    private const string ScriptNumberValue_decimal = "global::ScriptLang.Runtime.NumberValue<decimal>";
    private const string ScriptDateTimeValue = "global::ScriptLang.Runtime.DateTimeValue";
    private const string ScriptTimeSpanValue = "global::ScriptLang.Runtime.TimeSpanValue";
    private const string ScriptStringValue = "global::ScriptLang.Runtime.StringValue";
    private const string ScriptBoolValue = "global::ScriptLang.Runtime.BoolValue";
    private const string ScriptObjectValue = "global::ScriptLang.Runtime.ObjectValue";
    private const string ScriptArrayValue = "global::ScriptLang.Runtime.ArrayValue";
    private const string ScriptClrObjectValue = "global::ScriptLang.Runtime.ClrObjectValue";
    private const string ScriptClrMethodValue = "global::ScriptLang.Runtime.ClrMethodValue";
    private const string ScriptFunctionValue = "global::ScriptLang.Runtime.FunctionValue";
    private const string ScriptCompiledFunctionValue = "global::ScriptLang.Runtime.CompiledFunctionValue";

    /// <summary>
    /// 是否属于脚本的值
    /// </summary>
    /// <param name="fullTypeName"></param>
    /// <returns></returns>
    private static bool IsScriptValueType(string fullTypeName)
    {
        return fullTypeName switch
        {
            ScriptNullValue => true,
            ScriptNumberValue_int => true,
            ScriptNumberValue_long => true,
            ScriptNumberValue_float => true,
            ScriptNumberValue_double => true,
            ScriptNumberValue_decimal => true,
            ScriptDateTimeValue => true,
            ScriptTimeSpanValue => true,
            ScriptStringValue => true,
            ScriptBoolValue => true,
            ScriptObjectValue => true,
            ScriptArrayValue => true,
            ScriptClrObjectValue => true,
            ScriptClrMethodValue => true,
            ScriptFunctionValue => true,
            ScriptCompiledFunctionValue => true,
            _ => false
        };
    }

    /// <summary>
    /// 生成命名空间
    /// </summary>
    /// <param name="generator"></param>
    /// <returns></returns>
    internal static GeneratorCache<ClassCache> GeneratorClass(this GeneratorCache<ClassCache> generator) =>
        generator //.AppendCode($"using Value = {ScriptValue}")
                  //.AppendCode($"using FunctionValue = {ScriptFunctionValue}")
                 .AppendCode()
                 .AppendCode($"namespace {generator.Cache.Namespace}") // 命名空间
                 .AppendCodeBlok(() =>
                 {
                     var iprototypeType  = $"global::ScriptLang.IPrototype";
                     generator.AppendCode($"partial class {generator.Cache.Type.Name} : {iprototypeType}") // 命名空间
                              .AppendCodeBlok(() =>
                              {
                                  var isTargertMethodName = "IsTarget";
                                  var dictName = $"_prototypeMethods";
                                  generator.AppendCode($"private static Dictionary<string, Func<{ScriptValue}, {ScriptEngine}, {ScriptValue}>> {dictName} = [];")
                                           .AppendCode()
                                           .AppendCode($"bool _isLoad = false;")
                                           .AppendCode()
                                           .AppendCode($"public bool IsLoad => _isLoad;")
                                           .AppendCode()
                                           .AppendCode($"public partial bool {isTargertMethodName}({ScriptValue} value);")
                                           .AppendCode()
                                           .AppendCode($"{ScriptValue}? {iprototypeType}.GetMethod({ScriptValue} value, string methodName, {ScriptEngine} engine) => ")
                                           .AppendCode($"    {dictName}.TryGetValue(methodName, out var func) ? func(value, engine) : null;")
                                           .AppendCode()
                                           .AppendCode($"void {iprototypeType}.Init()")
                                           .AppendCodeBlok(() =>
                                           {
                                               var classCache = generator.Cache;
                                               var isPushThis = classCache.Cache.GetAttr<PrototypeExtensionAttribute, bool>(x => x.PushThis);
                                               var namingFormat = classCache.Cache.GetAttr<PrototypeExtensionAttribute, NamingFormat>(x => x.NamingFormat);

                                               var methods = classCache.GetItems().OfType<MethodCache>();
                                               var genPropertys = methods.Where(x => x.AttrsCache.ContainsAttr<PrototypePropertyAttribute>() /* && x.Parameters.Count > 0 */);
                                               var genFunctions = methods.Where(x => x.AttrsCache.ContainsAttr<PrototypeFunctionAttribute>() /* && x.Parameters.Count > 0 */);
                                               foreach (var property in genPropertys) 
                                               {
                                                   generator.GenerateProperty(property, isPushThis, namingFormat);
                                               }
                                               foreach (var method in genFunctions) 
                                               { 
                                                   generator.GenerateMethod(method, isPushThis, namingFormat);
                                               }
                                               var isTargertMethod = methods.FirstOrDefault(x => x.IsPartial && x.Name == isTargertMethodName && x.Parameters.Count == 1);
                                               if (isTargertMethod == null) 
                                               {

                                                   var desc = new DiagnosticDescriptor(
                                                        id: "Script001",
                                                        title: "需要实现分部方法",
                                                        messageFormat: $"已为 {generator.Cache.Type.Name} 类型创建继承 'IPrototype' 接口的分布类，" +
                                                            $"需要在该类中实现具有 partial 修饰的 bool {isTargertMethodName}(Value value) 接口方法",
                                                        category: "MemberDefinition",
                                                        defaultSeverity: DiagnosticSeverity.Error,
                                                        isEnabledByDefault: true
                                                    );
                                                   generator.Context.ReportDiagnostic(Diagnostic.Create(desc, location: classCache.Syntax.Identifier.GetLocation(), classCache.Type.Name));
                                               }

                                               generator.AppendCode($"_isLoad = true;");
                                               generator.AppendCode();

                                           }).AppendCode();
                              });
                 });

    

    internal static GeneratorCache<ClassCache> GenerateProperty(this GeneratorCache<ClassCache> generator, MethodCache methodCache, bool isPushThis, NamingFormat namingFormat)
    {
        var dictName = $"_prototypeMethods";
        var funcType = $"Func<{ScriptValue}, {ScriptEngine}, {ScriptValue}>";
        var defineName = methodCache.AttrsCache.GetAttr<PrototypePropertyAttribute, string>(x => x.Name);
        if (string.IsNullOrWhiteSpace(defineName))
        {
            if (namingFormat == NamingFormat.Net)
            {
                defineName = GeneratorHelper.GetNetName(methodCache.Name);
            }
            else
            {
                defineName = GeneratorHelper.GetJsName(methodCache.Name);
            }
        }
        var varName = $"_{methodCache.Name}";
        generator.AppendCode($"var {varName} = new {funcType}((v, env)=>")
                 .AppendCodeBlok(() =>
                 {
                     if (!isPushThis)
                     {
                         generator.AppendCode($"return {methodCache.Name}();");
                         return;
                     }
                     if (methodCache.Parameters.Count == 0)
                     {
                         generator.AppendCode($"return {ScriptNullValue};");
                         return;
                     }
                     var param0 = methodCache.Parameters[0];
                     var targetParamType = param0.Type.FullName;
                     var isScriptValue = IsScriptValueType(targetParamType);
                     if (isScriptValue)
                     {
                         generator.AppendCode($"var target = ({param0.Type.FullName})v;");
                     }
                     else
                     {
                         generator.AppendCode($"var target = ({param0.Type.FullName})(({ScriptClrObjectValue})v).Value;");
                     }
                     generator.AppendCode($"return {methodCache.Name}(target);");


                 })
                 .AppendCode($");")
                 .AppendCode($"{dictName}[\"{defineName}\"] = {varName};");
        return generator;
    }


    internal static GeneratorCache<ClassCache> GenerateMethod(this GeneratorCache<ClassCache> generator, MethodCache methodCache, bool isPushThis, NamingFormat namingFormat)
    {
        var dictName = $"_prototypeMethods";
        var funcType = $"Func<{ScriptValue}, {ScriptEngine}, {ScriptValue}>";
        var varName = $"_{methodCache.Name}";
        var defineName = methodCache.AttrsCache.GetAttr<PrototypeFunctionAttribute, string>(x => x.Name);
        if (string.IsNullOrWhiteSpace(defineName))
        {
            if(namingFormat == NamingFormat.Net)
            {
                defineName = GeneratorHelper.GetNetName(methodCache.Name);
            }
            else
            {
                defineName = GeneratorHelper.GetJsName(methodCache.Name);
            }
        }

        var isAsync = methodCache.IsAsync;


        generator.AppendCode($"var {varName} = new {funcType}((v, env)=>")
                 .AppendCodeBlok(() =>
                 {
                     generator//.AppendCode($"var target = ({generator.Cache.Type.FullName})v;")
                              .AppendCode($"return new {ScriptFunctionValue}(\"{defineName}\", {(isAsync ? "async " : string.Empty)}args =>")
                              .AppendCodeBlok(() =>
                              {
                                  var envIndex = methodCache.Parameters.Any(x => x.Type.FullName == ScriptEngine) ? methodCache.Parameters.FindIndex(x => x.Type.FullName == ScriptEngine) : -1;
                                  var needArgNum = (envIndex > 0 ? methodCache.Parameters.Count - 1 : methodCache.Parameters.Count) - (isPushThis ? 1 : 0);

                                  //var hasDefault = !string.IsNullOrWhiteSpace(param.DefaultValue); // 有默认值时，可以不传递参数

                                  var notRequiredParamCount = methodCache.Parameters.Count(param => !string.IsNullOrWhiteSpace(param.DefaultValue)); // 非必须参数数量
                                  var requiredParamCount = needArgNum - notRequiredParamCount;
                                  generator.AppendCode($"if(args.Count < {requiredParamCount})")
                                           .AppendCodeBlok(() =>
                                           {
                                               if(notRequiredParamCount > 0)
                                               {
                                                   generator.AppendCode($"throw new {RuntimeException}(\"{defineName}() 至少需要 {requiredParamCount} 个参数\");");
                                               }
                                               else
                                               {
                                                    generator.AppendCode($"throw new {RuntimeException}(\"{defineName}() 期望 {needArgNum} 个参数\");");
                                               }
                                           })
                                           .AppendCode();

                                  int paramIndex = 0;
                                  // 需要传入自身时，第一个参数默认是原型本身，所以从第二个参数开始检查
                                  for (int _index = (isPushThis ? 1 : 0); _index < methodCache.Parameters.Count; _index++)
                                  {
                                      if (_index == envIndex) continue;
                                      var param = methodCache.Parameters[_index];
                                      var paramType = param.Type.FullName;
                                      //var hasDefault = !string.IsNullOrWhiteSpace(param.DefaultValue); // 有默认值时，可以不传递参数

                                      // global::ScriptLang.Runtime.StringValue? arg0 = args.Count < 0 ? default
                                      //   : args[0] is StringValue _arg0 ? _arg0
                                      //   : throw new global::ScriptLang.Runtime.RuntimeException("read() 第 1 个参数期望 'StringValue' 类型值"); ;
                                      var argName = $"arg{paramIndex}";
                                      generator.AppendCode($"{paramType}? {argName} = args.Count <= {paramIndex} ? default")
                                               .AppendCode($"    : args[{paramIndex}] is {paramType} _{argName} ? _{argName}")
                                               .AppendCode($"    : throw new {RuntimeException}(\"{defineName}() 第 {paramIndex + 1} 个参数期望 '{param.Type.Name}' 类型值\");")
                                               .AppendCode();

                                      paramIndex++;
                                  }

                                  List<string> argNames = (isPushThis ? ["target"] : []);
                                  paramIndex = 0;
                                  for (int i = (isPushThis ? 1 : 0); i < methodCache.Parameters.Count; i++)
                                  {
                                      if (i == envIndex)
                                      {
                                          argNames.Add("env");
                                      }
                                      else
                                      {
                                          argNames.Add($"arg{paramIndex}");
                                          paramIndex++;
                                      }
                                  }
                                  // 将值作为参数传入
                                  if (isPushThis)
                                  {
                                      var targetParamType = methodCache.Parameters[0].Type.FullName;
                                      var isScriptValue = IsScriptValueType(targetParamType);
                                      if (isScriptValue)
                                      {
                                          generator.AppendCode($"var target = ({methodCache.Parameters[0].Type.FullName})v;");
                                      }
                                      else
                                      {
                                          generator.AppendCode($"var target = ({methodCache.Parameters[0].Type.FullName})(({ScriptClrObjectValue})v).Value;");
                                      }
                                  }


                                  if (methodCache.ReturnType.IsVoid)
                                  {
                                      generator.AppendCode($"{methodCache.Name}({string.Join(", ", argNames)});");
                                               //.AppendCode($"return {ScriptValue}.Null;");
                                  }
                                  else if (methodCache.ReturnType.IsTask)
                                  {
                                      if (methodCache.ReturnType.HasTaskReturnValue())
                                      {
                                          generator.AppendCode($"var result = await {methodCache.Name}({string.Join(", ", argNames)});")
                                                   .AppendCode($"return result;");
                                      }
                                      else
                                      {
                                          generator.AppendCode($"await {methodCache.Name}({string.Join(", ", argNames)});");
                                                   //.AppendCode($"return {ScriptValue}.Null;");
                                      }
                                  }
                                  else
                                  {
                                      generator.AppendCode($"var result = {methodCache.Name}({string.Join(", ", argNames)});")
                                               .AppendCode($"return result;");
                                  }
                              })
                              .AppendCode($");");
                 })
                 .AppendCode($");")
                                          .AppendCode($"{dictName}[\"{defineName}\"] = {varName};")
                 .AppendCode();
        return generator;
    }

  

}
