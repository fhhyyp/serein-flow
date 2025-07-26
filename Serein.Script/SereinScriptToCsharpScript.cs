using Newtonsoft.Json.Linq;
using Serein.Library;
using Serein.Script.Node;
using Serein.Script.Node.FlowControl;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Serein.Script
{
    /// <summary>
    /// 将 Serein 脚本转换为 C# 脚本的类
    /// </summary>
    public class SereinScriptToCsharpScript
    {
        public const string ClassName = nameof(SereinScriptToCsharpScript);

        public SereinScriptMethodInfo sereinScriptMethodInfo ;

        /// <summary>
        /// 符号表
        /// </summary>
        private readonly Dictionary<ASTNode, Type> _symbolInfos;

        /// <summary>
        /// 记录方法节点是否需要异步调用
        /// </summary>
        private readonly Dictionary<ASTNode, bool> _asyncMethods;

        /// <summary>
        /// 临时变量表
        /// </summary>
        private readonly Dictionary<string, Type> _local = [];

        private bool _isTaskMain = false;

        public SereinScriptToCsharpScript(SereinScriptTypeAnalysis analysis)
        {
            _symbolInfos = analysis.NodeSymbolInfos;
            _asyncMethods = analysis.AsyncMethods;
            _isTaskMain = _asyncMethods.Any(kvp => kvp.Value);
        }

        private readonly StringBuilder _codeBuilder = new StringBuilder();
        private int _indentLevel = 0;

        private void AppendLine(string code)
        {
            _codeBuilder.AppendLine(new string(' ', _indentLevel * 4) + code);
        }

        private void Append(string code)
        {
            _codeBuilder.Append(code);
        }

        private void Indent() => _indentLevel++;
        private void Unindent() => _indentLevel--;

        private List<Action<StringBuilder>> _classDefinitions = new List<Action<StringBuilder>>();

        public SereinScriptMethodInfo CompileToCSharp(string mehtodName, ProgramNode programNode, Dictionary<string, Type>? param)
        {
            _codeBuilder.Clear();
            sereinScriptMethodInfo = new SereinScriptMethodInfo()
            {
                ClassName = ClassName,
                ParamInfos = new List<SereinScriptMethodInfo.SereinScriptParamInfo>(),
                MethodName = mehtodName,
            };
            var sb = _codeBuilder;
            var methodResultType = _symbolInfos[programNode];
            if(methodResultType == typeof(void))
            {
                methodResultType = typeof(object);
            }
            var taskFullName = typeof(Task).FullName;
            string? returnContent;
            if (_isTaskMain)
            {
                returnContent = $"global::{taskFullName}<global::{methodResultType.FullName}>";
                sereinScriptMethodInfo.IsAsync = true;
            }
            else
            {
                returnContent = $"global::{methodResultType.FullName}";
                sereinScriptMethodInfo.IsAsync = false;
            }

            AppendLine($"public partial class {ClassName}");
            AppendLine( "{");
            Indent();
            if(param is null || param.Count == 0)
            {
                AppendLine($"public static {returnContent} {mehtodName}()");
            }
            else
            {
                AppendLine($"public static {returnContent} {mehtodName}({GetMethodParamster(param)})");
            }
            AppendLine( "{");
            Indent();
            foreach (var stmt in programNode.Statements)
            {
                ConvertCode(stmt); // 递归遍历
                Append(";");
            }

            if (_symbolInfos[programNode] == typeof(void))
            {
                AppendLine("");
                AppendLine("return null;");
            }

            Unindent();
            AppendLine("}");
            Unindent();
            AppendLine("}");

            foreach (var cd in _classDefinitions)
            {
                cd.Invoke(sb);
            }
            sereinScriptMethodInfo.CsharpCode = sb.ToString();
            sereinScriptMethodInfo.ReturnType = methodResultType;
            return sereinScriptMethodInfo;
        }

        private string GetMethodParamster(Dictionary<string, Type> param)
        {
            var values = param.Select(kvp =>
            {
                var paramName = kvp.Key;
                var type = kvp.Value;
                _local[paramName] = type;
                sereinScriptMethodInfo.ParamInfos.Add(new SereinScriptMethodInfo.SereinScriptParamInfo
                {
                    ParameterType = type,
                    ParamName = paramName,
                });
                return $"global::{type.FullName} {paramName}";
            });
            return string.Join(',', values);
        }

        private void ConvertCode( ASTNode node)
        {
            switch (node)
            {
                case ProgramNode programNode: // 程序开始节点
                    break;
                case ReturnNode returnNode: // 程序退出节点
                    void ConvertCodeOfReturnNode(ReturnNode returnNode)
                    {
                        if (returnNode.Value is not null)
                        {
                            Append($"return ");
                            ConvertCode(returnNode.Value);
                            Append(";");
                            AppendLine(string.Empty);
                        }
                        else
                        {
                            AppendLine("return defult;");
                        }
                    }
                    ConvertCodeOfReturnNode(returnNode);
                    break;
                #region 字面量解析
                case NullNode nullNode: // null
                    Append("null");
                    break;
                case CharNode charNode: // char字面量
                    Append($"'{charNode.Value}'");
                    break;
                case StringNode stringNode: // 字符串字面量
                    Append($"\"{stringNode.Value}\"");
                    break;
                case BooleanNode booleanNode: // 布尔值字面量
                    Append($"{(booleanNode.Value ? "true" : "false")}");
                    break;
                case NumberIntNode numberIntNode: // int整型数值字面量
                    Append($"{numberIntNode.Value}");
                    break;
                case NumberLongNode numberLongNode: // long整型数值字面量
                    Append($"{numberLongNode.Value}");
                    break;
                case NumberFloatNode numberFloatNode: // float浮点数值字面量
                    Append($"{numberFloatNode.Value}f");
                    break;
                case NumberDoubleNode numberDoubleNode: // double浮点数值字面量
                    Append($"{numberDoubleNode.Value}d");
                    break; 
                #endregion
                case IdentifierNode identifierNode: // 变量定义
                    void ConvertCodeOfIdentifierNode(IdentifierNode identifierNode)
                    {
                        var varName = identifierNode.Name;

                        if(_local.TryGetValue(varName, out var type))
                        {
                            // 定义过，需要使用变量
                            Append(varName);
                        }
                        else
                        {
                            if (_symbolInfos.TryGetValue(identifierNode, out type))
                            {
                                // 不存在，定义变量
                                Append($"global::{type.FullName} {varName}");
                                _local[varName] = type;
                                //Append(varName);
                            }
                            else
                            {
                                throw new Exception($"加载符号表时，无法匹配 IdentifierNode 节点的类型。 name ： {varName}");
                            }
                        }
                    }
                    ConvertCodeOfIdentifierNode(identifierNode);
                    break;
                case IfNode ifNode: // if语句结构
                    void ConvertCodeOfIfNode(IfNode ifNOde)
                    {
                        AppendLine("");
                        Append($"if(");
                        ConvertCode(ifNOde.Condition); // 解析条件
                        Append($" == true)");
                        AppendLine("{");
                        Indent();
                        foreach(var item in ifNOde.TrueBranch)
                        {
                            ConvertCode(item);
                            //Append(";");
                            AppendLine(string.Empty);
                        }  
                        Unindent();
                        AppendLine("}");
                        AppendLine("else");
                        AppendLine("{");
                        Indent();
                        foreach (var item in ifNOde.TrueBranch)
                        {
                            ConvertCode(item);
                            //Append(";");
                            AppendLine(string.Empty);
                        }
                        Unindent();
                        AppendLine("}");
                    }
                    ConvertCodeOfIfNode(ifNode);
                    break;
                case WhileNode whileNode: // while语句结构
                    void ConvertCodeOfWhileNode(WhileNode whileNode)
                    {
                        AppendLine("");
                        Append($"while(");
                        ConvertCode(whileNode.Condition); // 解析条件
                        Append($" == {true})");
                        AppendLine("{");
                        Indent();
                        foreach (var item in whileNode.Body)
                        {
                            ConvertCode(item);
                            Append(";");
                            AppendLine(string.Empty);
                        }
                        Unindent();
                        AppendLine("}");
                    }
                    ConvertCodeOfWhileNode(whileNode);
                    break;
                case AssignmentNode assignmentNode: // 变量赋值语句
                    void ConvertCodeOfAssignmentNode(AssignmentNode assignmentNode)
                    {
                        ConvertCode(assignmentNode.Target);
                        Append(" = ");
                        if (assignmentNode.Value is null)
                        {
                            Append("null");
                        }
                        else
                        {
                            ConvertCode(assignmentNode.Value);
                        }
                        Append(";");
                        AppendLine(string.Empty);
                    }
                    ConvertCodeOfAssignmentNode(assignmentNode);
                    break;
                case BinaryOperationNode binaryOperationNode: // 二元运算操作
                    void ConvertCodeOfBinaryOperationNode(BinaryOperationNode binaryOperationNode)
                    {
                        Append("("); 
                        ConvertCode(binaryOperationNode.Left); // 左操作数
                        Append(binaryOperationNode.Operator); // 操作符
                        ConvertCode(binaryOperationNode.Right); // 左操作数
                        Append(")"); 
                    }
                    ConvertCodeOfBinaryOperationNode(binaryOperationNode);
                    break;
                case CollectionAssignmentNode collectionAssignmentNode:
                    void ConvertCodeOfCollectionAssignmentNode(CollectionAssignmentNode collectionAssignmentNode)
                    {
                        ConvertCode(collectionAssignmentNode.Collection);
                        Append(" = ");
                        ConvertCode(collectionAssignmentNode.Value);
                        Append(";");
                        AppendLine(string.Empty);
                    }
                    ConvertCodeOfCollectionAssignmentNode(collectionAssignmentNode);
                    break;
                case CollectionIndexNode collectionIndexNode: // 集合类型操作
                    void ConvertCodeOfCollectionIndexNode(CollectionIndexNode collectionIndexNode)
                    {
                        ConvertCode(collectionIndexNode.Collection);
                        Append("[");
                        ConvertCode(collectionIndexNode.Index);
                        Append("]");
                    }
                    ConvertCodeOfCollectionIndexNode(collectionIndexNode);
                    break;
                case ClassTypeDefinitionNode classTypeDefinitionNode: // 类型定义
                    void ConvertCodeOfClassTypeDefinitionNode(ClassTypeDefinitionNode classTypeDefinitionNode)
                    {
                        _classDefinitions.Add((sb) =>
                        {
                            var type = _symbolInfos[classTypeDefinitionNode.ClassType];
                            AppendLine($"public class {type.FullName}");
                            AppendLine("{");
                            Indent();
                            foreach (var property in classTypeDefinitionNode.Propertys)
                            {
                                var propertyName = property.Key;
                                var propertyType = _symbolInfos[property.Value];
                                AppendLine($"public global::{propertyType.FullName} {propertyName} {{ get; set; }}");
                            }
                            Unindent();
                            AppendLine("}");
                        });
                    }
                    ConvertCodeOfClassTypeDefinitionNode(classTypeDefinitionNode);
                    break;
                case TypeNode typeNode: // 类型
                    break;
                case ObjectInstantiationNode objectInstantiationNode: // 类型实例化
                    void ConvertCodeOfObjectInstantiationNode(ObjectInstantiationNode objectInstantiationNode)
                    {
                        var type = _symbolInfos[objectInstantiationNode.Type];
                        Append($"new global::{type}");
                        if (objectInstantiationNode.Arguments.Count > 0)
                        {
                            Append("(");
                            for (int i = 0; i < objectInstantiationNode.Arguments.Count; i++)
                            {
                                ConvertCode(objectInstantiationNode.Arguments[i]);
                                if (i < objectInstantiationNode.Arguments.Count - 1)
                                {
                                    Append(", ");
                                }
                            }
                            Append(")");
                        }
                        else
                        {
                            Append("()");
                        }
                        if (objectInstantiationNode.CtorAssignments.Count > 0)
                        {
                            AppendLine("{");
                            Indent();
                            foreach (var assignment in objectInstantiationNode.CtorAssignments)
                            {
                                ConvertCode(assignment);
                            }
                            Unindent();
                            AppendLine("}");
                        }
                    }
                    ConvertCodeOfObjectInstantiationNode(objectInstantiationNode);
                    break;
                case CtorAssignmentNode ctorAssignmentNode: // 构造器赋值
                    void ConvertCodeOfCtorAssignmentNode(CtorAssignmentNode ctorAssignmentNode)
                    {
                        var propertyName = ctorAssignmentNode.MemberName;
                        var value = ctorAssignmentNode.Value;
                        Append($"{propertyName} = ");
                        ConvertCode(value);
                        AppendLine(",");
                    }
                    ConvertCodeOfCtorAssignmentNode(ctorAssignmentNode);
                    break;
                case MemberAccessNode memberAccessNode: // 对象成员访问
                    void ConvertCodeOfMemberAccessNode(MemberAccessNode memberAccessNode)
                    {
                        ConvertCode(memberAccessNode.Object);
                        Append($".{memberAccessNode.MemberName}");
                    }
                    ConvertCodeOfMemberAccessNode(memberAccessNode); 
                    break;
                case MemberAssignmentNode memberAssignmentNode: // 对象成员赋值
                    void ConvertCodeOfMemberAssignmentNode(MemberAssignmentNode memberAssignmentNode)
                    {
                        ConvertCode(memberAssignmentNode.Object);
                        Append($".{memberAssignmentNode.MemberName} = ");
                        ConvertCode(memberAssignmentNode.Value);
                        Append($";");
                        AppendLine(string.Empty);
                    }
                    ConvertCodeOfMemberAssignmentNode(memberAssignmentNode); 
                    break;
                case MemberFunctionCallNode memberFunctionCallNode: // 对象方法调用
                    void ConvertCodeOfMemberFunctionCallNode(MemberFunctionCallNode memberFunctionCallNode)
                    {
                        var isAsync = _asyncMethods.TryGetValue(memberFunctionCallNode, out var isAsyncValue) && isAsyncValue;
                        if (isAsync)
                        {
                            Append($"(await ");
                        }

                        ConvertCode(memberFunctionCallNode.Object);
                        Append($".{memberFunctionCallNode.FunctionName}(");
                        for (int i = 0; i < memberFunctionCallNode.Arguments.Count; i++)
                        {
                            ASTNode? argNode = memberFunctionCallNode.Arguments[i];
                            ConvertCode(argNode);
                            if (i < memberFunctionCallNode.Arguments.Count - 1)
                            {
                                Append(", ");
                            }
                        }
                        Append($")");
                        if (isAsync)
                        {
                            Append($")");
                        }


                    }
                    ConvertCodeOfMemberFunctionCallNode(memberFunctionCallNode); 
                    break;
                case FunctionCallNode functionCallNode: // 外部挂载的函数调用
                    void ConvertCodeOfFunctionCallNode(FunctionCallNode functionCallNode)
                    {
                        var isAsync = _asyncMethods.TryGetValue(functionCallNode, out var isAsyncValue) && isAsyncValue;
                        if (isAsync)
                        {
                            Append($"(await ");
                        }
                        var funcName = functionCallNode.FunctionName switch
                        {
                            "int"      =>  "@int"       ,
                            "bool"     =>  "@bool"      ,
                            "double"   =>  "@double"    ,
                            "long"     =>  "@long"      ,
                            "decimal"  =>  "@decimal"   ,
                            "float"    =>  "@float"     ,
                            "byte"     =>  "@byte"      ,

                            _ => functionCallNode.FunctionName,
                        };
                        Append($"global::Serein.Library.ScriptBaseFunc.{funcName}(");
                        for (int i = 0; i < functionCallNode.Arguments.Count; i++)
                        {
                            ASTNode? argNode = functionCallNode.Arguments[i];
                            ConvertCode(argNode);
                            if (i < functionCallNode.Arguments.Count - 1)
                            {
                                Append(", ");
                            }
                        }
                        Append($")");
                        if (isAsync)
                        {
                            Append($")");
                        }
                    }
                    ConvertCodeOfFunctionCallNode(functionCallNode);
                    break;
                default: // 未定义的节点类型
                    break;
            }
        }

    }


}
