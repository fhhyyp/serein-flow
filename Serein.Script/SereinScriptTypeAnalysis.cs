using Serein.Library.Utils;
using Serein.Script.Node;
using Serein.Script.Symbol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Serein.Script
{
    public class SereinScriptTypeAnalysis
    {
        private Dictionary<string, SymbolInfo> SymbolInfos = new Dictionary<string, SymbolInfo>();

        public SereinScriptTypeAnalysis(ProgramNode programNode)
        {
            SymbolInfos.Clear(); // 清空符号表
            foreach (ASTNode astNode in programNode.Statements)
            {
                var type = Trace(astNode);
                if (type is null) continue;
                var info = Analyse(astNode, type);
                if(info != null)
                {
                    SymbolInfos[info.Name] = info;
                }
                /*if(astNode is AssignmentNode assignmentNode)
                {
                    var name = assignmentNode.Variable;
                    var node = assignmentNode.Value;
                    var type = Analyse(node);
                    if(type is null)
                    {
                        continue;
                    }
                    var symbolInfo = new SymbolInfo
                    {
                        Type = type,
                        Node = node,
                        Name = name,
                    };
                    SymbolInfos[name] = symbolInfo;
                }*/
            }
        }

        /// <summary>
        /// 追踪类型
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private Type Trace(ASTNode node)
        {
            if (node == null)
            {
                return null;
            }
            switch (node)
            {
                case NullNode nullNode: // 返回null
                    return typeof(object);
                case BooleanNode booleanNode: // 返回布尔
                    return typeof(bool);
                case NumberIntNode numberNode: // 数值
                    return typeof(int);
                case StringNode stringNode: // 字符串
                    return typeof(string);
                case CharNode charNode: // char
                    return typeof(char);
                case IdentifierNode identifierNode: // 定义变量
                    return typeof(object);
                case AssignmentNode assignmentNode: // 赋值行为
                    var type = Trace(assignmentNode.Value);
                    return type;
                //throw new SereinSciptException(identifierNode, "尝试使用值为null的变量");
                //throw new SereinSciptException(identifierNode, "尝试使用未声明的变量");
                case BinaryOperationNode binOpNode: // 递归计算二元操作
                    var leftType = Trace(binOpNode.Left);
                    var op = binOpNode.Operator;
                    var rightType = Trace(binOpNode.Right);
                    var resultType = BinaryOperationEvaluator.EvaluateType(leftType, op, rightType);
                    return resultType;
                case ClassTypeDefinitionNode classTypeDefinitionNode:
                    var definitionType = DynamicObjectHelper.CreateTypeWithProperties(classTypeDefinitionNode.Fields, classTypeDefinitionNode.ClassName, true);
                    return definitionType;
                case ObjectInstantiationNode objectInstantiationNode: // 创建对象

                    var typeName = objectInstantiationNode.TypeName;
                    var objectType = Type.GetType(typeName);
                    objectType ??= DynamicObjectHelper.GetCacheType(typeName);
                    return objectType;
                case FunctionCallNode callNode: // 调用方法
                    return null;
                case MemberFunctionCallNode memberFunctionCallNode: // 对象方法调用
                    return null;
                case MemberAccessNode memberAccessNode: // 对象成员访问
                    var memberType = memberAccessNode.MemberName;
                    return null;
                case CollectionIndexNode collectionIndexNode:
                case ReturnNode returnNode: // 返回内容

                default:
                    break;
                    //throw new SereinSciptException(node, $"解释器 EvaluateAsync() 未实现{node}节点行为");
            }

            return null;
        }

        

        private SymbolInfo Analyse(ASTNode node, Type type)
        {
            if (node == null)
            {
                return null;
            }
            switch (node)
            {
                case IdentifierNode identifierNode: // 定义变量
                    return new SymbolInfo
                    {
                        Name = identifierNode.Name,
                        Node = node,
                        Type = type,
                    };
                case AssignmentNode assignmentNode: // 赋值行为
                    return new SymbolInfo
                    {
                        Name = assignmentNode.Variable,
                        Node = node,
                        Type = type,
                    };
                case BinaryOperationNode binOpNode: // 递归计算二元操作
                //case ClassTypeDefinitionNode classTypeDefinitionNode
                case ObjectInstantiationNode objectInstantiationNode: // 创建对象
                case FunctionCallNode callNode: // 调用方法
                case MemberFunctionCallNode memberFunctionCallNode: // 对象方法调用
                case MemberAccessNode memberAccessNode: // 对象成员访问
                case CollectionIndexNode collectionIndexNode:
                case ReturnNode returnNode: // 返回内容
                default:
                    break;
                    //throw new SereinSciptException(node, $"解释器 EvaluateAsync() 未实现{node}节点行为");
            }

            return null;
        }

        /*
         case NullNode nullNode: // 返回null
         case BooleanNode booleanNode: // 返回布尔
         case NumberIntNode numberNode: // 数值
         case StringNode stringNode: // 字符串
         case CharNode charNode: // char
         case IdentifierNode identifierNode: // 定义变量
         case AssignmentNode assignmentNode: // 赋值行为
         case BinaryOperationNode binOpNode: // 递归计算二元操作
         case ObjectInstantiationNode objectInstantiationNode: // 创建对象
         case FunctionCallNode callNode: // 调用方法
         case MemberFunctionCallNode memberFunctionCallNode: // 对象方法调用
         case MemberAccessNode memberAccessNode: // 对象成员访问
         case CollectionIndexNode collectionIndexNode:
         case ReturnNode returnNode: // 返回内容
         default:
             break;
        */

    }
}
