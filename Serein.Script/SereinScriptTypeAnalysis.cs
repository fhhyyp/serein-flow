using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.Script.Node;
using Serein.Script.Node.FlowControl;
using Serein.Script.Symbol;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Serein.Script
{

    /// <summary>
    /// 脚本类型分析
    /// </summary>
    public class SereinScriptTypeAnalysis
    {
        /// <summary>
        /// 符号表
        /// </summary>
        public Dictionary<ASTNode, Type> NodeSymbolInfos { get; } = new Dictionary<ASTNode, Type>();

        /// <summary>
        /// 记录方法节点是否需要进行异步调用
        /// </summary>
        public Dictionary<ASTNode, bool> AsyncMethods { get; } = new Dictionary<ASTNode, bool>();

        public void Reset()
        {
            NodeSymbolInfos.Clear(); // 清空符号表
            AsyncMethods.Clear(); 
        }

        public void LoadSymbol(Dictionary<string,Type> identifierNodes)
        {
            foreach(var kvp in identifierNodes)
            {
                var name = kvp.Key;
                var type = kvp.Value;
                var identifierNode = new IdentifierNode(name);
                NodeSymbolInfos[identifierNode] = type;
            }
        }


        public void Analysis(ProgramNode astNode)
        {
            //NodeSymbolInfos.Clear();
            for (int i = 0; i < astNode.Statements.Count; i++)
            {
                var node = astNode.Statements[i];
                Analysis(node);
            }

            
            var returnNodes = NodeSymbolInfos.Keys.Where(node => node is ReturnNode).ToArray();
            if (returnNodes.Length == 0)
            {
                NodeSymbolInfos[astNode] = typeof(void); // 程序无返回值
            }
            else if (returnNodes.Length == 1)
            {
                var ifNodes = astNode.Statements.Where(node => node is IfNode).ToArray();
                
                NodeSymbolInfos[astNode] = NodeSymbolInfos[returnNodes[0]]; // 确定的返回值
            }
            else
            {
                var firstReturnType = NodeSymbolInfos[returnNodes[0]]; // 第一个返回值
                foreach(var item in returnNodes)
                {
                    if(NodeSymbolInfos[item] != firstReturnType)
                    {
                        throw new Exception("类型检查异常，存在不同分支返回值类型不一致");
                    }
                }
                NodeSymbolInfos[astNode] = NodeSymbolInfos[returnNodes[0]]; // 确定的返回值
            }
        }

       

        /// <summary>
        /// 类型获取
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        /// <exception cref="NotImplementedException"></exception>
        private Type Analysis(ASTNode node)
        {
            switch (node)
            {
                case UsingNode usingNode:
                    // 引用命名空间节点，不产生类型
                    NodeSymbolInfos[usingNode] = typeof(void);
                    return typeof(void);
                case ProgramNode programNode: // 程序开始节点
                    NodeSymbolInfos[programNode] = typeof(void);
                    return typeof(void);
                case ReturnNode returnNode: // 程序退出节点
                    Type AnalysisReturnNode(ReturnNode returnNode)
                    {
                        if(returnNode.Value is null)
                        {
                            var resultType = typeof(void);
                            NodeSymbolInfos[returnNode] = resultType;
                            return resultType;
                        }
                        else
                        {
                            var resultType = Analysis(returnNode.Value);
                            NodeSymbolInfos[returnNode.Value] = resultType;
                            NodeSymbolInfos[returnNode] = resultType;
                            return resultType;
                        }
                        
                    }
                    return AnalysisReturnNode(returnNode);
                case NullNode nullNode: // null
                    NodeSymbolInfos[nullNode] = typeof(object);
                    return typeof(object);
                case CharNode charNode: // char字面量
                    NodeSymbolInfos[charNode] = typeof(char);
                    return typeof(char);
                case RawStringNode rawStringNode: // 原始字符串字面量（多行字符串）
                    NodeSymbolInfos[rawStringNode] = typeof(string);
                    return typeof(string);
                case StringNode stringNode: // 字符串字面量
                    NodeSymbolInfos[stringNode] = typeof(string);
                    return typeof(string);
                case BooleanNode booleanNode: // 布尔值字面量
                    NodeSymbolInfos[booleanNode] = typeof(bool);
                    return typeof(bool);
                case NumberIntNode numberIntNode: // int整型数值字面量
                    NodeSymbolInfos[numberIntNode] = typeof(int);
                    return typeof(int);
                case NumberLongNode numberLongNode: // long整型数值字面量
                    NodeSymbolInfos[numberLongNode] = typeof(long);
                    return typeof(long);
                case NumberFloatNode numberFloatNode: // float浮点数值字面量
                    NodeSymbolInfos[numberFloatNode] = typeof(float);
                    return typeof(float);
                case NumberDoubleNode numberDoubleNode: // double浮点数值字面量
                    NodeSymbolInfos[numberDoubleNode] = typeof(double);
                    return typeof(double);
                case IdentifierNode identifierNode: // 变量定义
                    Type AnalysisIdentifierNode(IdentifierNode identifierNode)
                    {
                        var cacheNode = NodeSymbolInfos.Keys.FirstOrDefault(n => n is IdentifierNode idNode && idNode.Name == identifierNode.Name);
                        Type type = cacheNode is null ? typeof(object) : NodeSymbolInfos[cacheNode]; 
                        NodeSymbolInfos[identifierNode] = type;
                        return type;
                    }
                    return AnalysisIdentifierNode(identifierNode);
                case IfNode ifNode: // if语句结构
                    Type AnalysisIfNode(IfNode ifNode)
                    {
                        var conditionType = Analysis(ifNode.Condition); // 获取条件语句部分的返回类型
                        NodeSymbolInfos[ifNode.Condition] = conditionType;
                        if (conditionType == typeof(bool?) || conditionType == typeof(bool))
                        {
                            foreach (var item in ifNode.TrueBranch)
                            {
                                var itemType = Analysis(item); // 解析真分支的语句块
                                NodeSymbolInfos[item] = itemType;
                            }
                            foreach (var item in ifNode.FalseBranch)
                            {
                                var itemType = Analysis(item); // 解析假分支的语句块
                                NodeSymbolInfos[item] = itemType;
                            }
                            NodeSymbolInfos[ifNode] = typeof(void);
                            return typeof(void); // if语句不产生类型
                        }
                        else
                        {
                            throw new NotImplementedException("if...else...条件返回值不为布尔类型变量");
                        }
                    }
                    return AnalysisIfNode(ifNode);
                case WhileNode whileNode: // while语句结构
                    Type AnalysisWhileNode(WhileNode whileNode)
                    {
                        var conditionType = Analysis(whileNode.Condition); // 获取条件语句部分的返回类型
                        NodeSymbolInfos[whileNode.Condition] = conditionType;
                        if (conditionType == typeof(bool?) || conditionType == typeof(bool))
                        {
                            foreach (var item in whileNode.Body)
                            {
                                var itemType = Analysis(item); // 解析真分支的语句块
                                NodeSymbolInfos[item] = itemType;
                            }
                            NodeSymbolInfos[whileNode] = typeof(void);  // while流程不产生类型
                            return typeof(void); // if语句不产生类型
                        }
                        else
                        {
                            throw new NotImplementedException("if...else...条件返回值不为布尔类型变量");
                        }
                    }
                    return AnalysisWhileNode(whileNode);
                case AssignmentNode assignmentNode:
                    // 对象赋值语句（let x;默认赋值null。默认类型object）
                    Type AnalysisAssignmentNode(AssignmentNode assignmentNode)
                    {
                        var targetType = Analysis(assignmentNode.Target);
                        var valueType = Analysis (assignmentNode.Value);
                        if (!targetType.IsAssignableFrom(valueType))
                            throw new Exception($"赋值类型不匹配：需要 {targetType}，实际为 {valueType}");
                        NodeSymbolInfos[assignmentNode.Value] = valueType;
                        NodeSymbolInfos[assignmentNode.Target] = valueType;
                        NodeSymbolInfos[assignmentNode] = typeof(void); // 赋值语句不产生类型
                        return targetType;
                    }
                    return AnalysisAssignmentNode(assignmentNode);
                case BinaryOperationNode binaryOperationNode: // 二元运算操作
                    Type AnalysisBinaryOperationNode(BinaryOperationNode binaryOperationNode)
                    {
                        var leftType = Analysis(binaryOperationNode.Left); // 递归判断左值类型
                        var rightType = Analysis(binaryOperationNode.Right); // 递归判断右值类型
                        var op = binaryOperationNode.Operator;
                        var resultType = BinaryOperationEvaluator.EvaluateType(leftType, op, rightType);
                        NodeSymbolInfos[binaryOperationNode.Left] = leftType;
                        NodeSymbolInfos[binaryOperationNode.Right] = rightType;
                        NodeSymbolInfos[binaryOperationNode] = resultType; 
                        return resultType;
                    }
                    return AnalysisBinaryOperationNode(binaryOperationNode);
                case CollectionIndexNode collectionIndexNode: // 集合类型操作，获取集合操作后返回的类型
                    Type AnalysisCollectionIndexNode(CollectionIndexNode collectionIndexNode)
                    {
                        var collectionType = Analysis(collectionIndexNode.Collection); // 分析集合类型（变量，对象成员）
                        var indexExprType  = Analysis(collectionIndexNode.Index); // 分析索引类型
                        if (!TryGetIndexerType(collectionType, out var expectedIndexType, out var resultType))
                            throw new Exception($"类型 {collectionType} 不支持索引操作");

                        if (!expectedIndexType.IsAssignableFrom(indexExprType))
                            throw new Exception($"索引类型不匹配：需要 {expectedIndexType}，实际为 {indexExprType}");
                        NodeSymbolInfos[collectionIndexNode.Collection] = collectionType;
                        NodeSymbolInfos[collectionIndexNode.Index] = indexExprType;
                        NodeSymbolInfos[collectionIndexNode] = resultType;
                        return resultType; 
                    }
                    return AnalysisCollectionIndexNode(collectionIndexNode);
                case CollectionAssignmentNode collectionAssignmentNode: // 集合赋值操作
                    Type AnalysisCollectionAssignmentNode(CollectionAssignmentNode collectionAssignmentNode)
                    {
                        var resultType = Analysis(collectionAssignmentNode.Collection); // 分析集合返回返回类型
                        var valueType = Analysis(collectionAssignmentNode.Value); // 分析赋值的类型
                        if (!resultType.IsAssignableFrom(valueType))
                            throw new Exception($"类型 {resultType} 不支持索引操作");

                        NodeSymbolInfos[collectionAssignmentNode.Collection] = resultType;
                        NodeSymbolInfos[collectionAssignmentNode.Value] = valueType;
                        NodeSymbolInfos[collectionAssignmentNode] = typeof(void); // 赋值语句不产生类型
                        return typeof(void);
                    }
                    return AnalysisCollectionAssignmentNode(collectionAssignmentNode);
                case ArrayDefintionNode arrayDefintionNode:
                    Type AnalysisArrayDefintionNode(ArrayDefintionNode arrayDefintionNode)
                    {
                        var elements = arrayDefintionNode.Elements;
                        if (elements.Count == 0)
                        {
                            return typeof(object[]);
                        }

                        Type[] types = new Type[elements.Count];
                        for (int i = 0; i < elements.Count; i++)
                        {
                            ASTNode? element = elements[i];
                            var elementType  = Analysis(element); // 分析元素类型
                            types[i] = elementType; 
                            NodeSymbolInfos[element] = elementType; // 添加到符号表
                        }


                        // 所有类型一致
                        if (types.All(t => t == types[0]))
                        {
                            var arrType = types[0].MakeArrayType();
                            NodeSymbolInfos[arrayDefintionNode] = arrType; // 添加到符号表
                            return arrType;
                        }
                        else
                        {
                            // 尝试找公共基类
                            Type? commonType = TypeHelper.FindCommonBaseType(types) ?? typeof(object);
                            var arrType = commonType.MakeArrayType();
                            NodeSymbolInfos[arrayDefintionNode] = arrType; // 添加到符号表
                            return arrType;
                        }
                            
                    }
                    return AnalysisArrayDefintionNode(arrayDefintionNode);
                case ClassTypeDefinitionNode classTypeDefinitionNode: // 类型定义
                    Type AnalysisClassTypeDefinitionNode(ClassTypeDefinitionNode classTypeDefinitionNode)
                    {

                        var classType = Analysis(classTypeDefinitionNode.ClassType); // 查询类型
                        NodeSymbolInfos[classTypeDefinitionNode] = classType;

                        foreach (var kvp in classTypeDefinitionNode.Propertys)
                        {
                            TypeNode propertyNode = kvp.Value;
                            var propertyType = Analysis(propertyNode); // 查询属性类型
                            NodeSymbolInfos[propertyNode] = propertyType;
                        }

                        NodeSymbolInfos[classTypeDefinitionNode] = classType;
                        return classType;
                    }
                    return AnalysisClassTypeDefinitionNode(classTypeDefinitionNode);
                case TypeNode typeNode:
                    Type AnalysisTypeNode(TypeNode typeNode)
                    {
                        // 类型搜寻优先级： 挂载类型 > 脚本中定义类型 > C#类型
                        Type resultType = GetTypeOfString(typeNode.TypeName); // 从自定义类型查询类型
                        NodeSymbolInfos[typeNode] = resultType;
                        return resultType;
                    }
                    return AnalysisTypeNode(typeNode);
                case ObjectInstantiationNode objectInstantiationNode: // 类型实例化
                    Type AnalysisObjectInstantiationNode(ObjectInstantiationNode objectInstantiationNode)
                    {
                        Type resultType = Analysis(objectInstantiationNode.Type);
                        foreach(var item in objectInstantiationNode.CtorAssignments)
                        {
                            Analysis(item);
                        }
                        NodeSymbolInfos[objectInstantiationNode] = resultType;
                        return resultType;
                    }
                    return AnalysisObjectInstantiationNode(objectInstantiationNode);
                case CtorAssignmentNode ctorAssignmentNode: // 构造器赋值
                    Type AnalysisCtorAssignmentNode(CtorAssignmentNode ctorAssignmentNode)
                    {
                        Type classType = Analysis(ctorAssignmentNode.Class);
                        Type valueType = Analysis(ctorAssignmentNode.Value);
                        //ctorAssignmentNode.MemberName
                        var property = classType.GetProperty(ctorAssignmentNode.MemberName);
                        if (property is null)
                            throw new Exception($"类型 {classType} 没有成员 {ctorAssignmentNode.MemberName}");
                        var propertyType = property.PropertyType;
                        if (!propertyType.IsAssignableFrom(valueType))
                            throw new Exception($"类型异常：构造器赋值需要 {propertyType}，实际为 {valueType}"); 

                        NodeSymbolInfos[ctorAssignmentNode.Class] = classType;
                        NodeSymbolInfos[ctorAssignmentNode.Value] = valueType;
                        NodeSymbolInfos[ctorAssignmentNode] = propertyType; 
                        return valueType;
                    }
                    return AnalysisCtorAssignmentNode(ctorAssignmentNode);
                /*case ExpressionNode expressionNode: // 类型表达式（链式调用）
                    Type AnalysisObjectMemberExpressionNode(ExpressionNode expressionNode)
                    {
                        // 1. 对象成员获取 MemberAccessNode
                        // 2. 对象方法调用 MemberFunctionCallNode
                        // 3. 对象集合成员获取 CollectionIndexNode
                        Type? resultType = Analysis(expressionNode.Value);
                        NodeSymbolInfos[expressionNode.Value] = resultType;
                        NodeSymbolInfos[expressionNode] = resultType;
                        return resultType;
                    }
                    return AnalysisObjectMemberExpressionNode(expressionNode);*/
                case MemberAccessNode memberAccessNode: // 对象成员访问
                    Type AnalysisMemberAccessNode(MemberAccessNode memberAccessNode)
                    {
                        var objectType = Analysis(memberAccessNode.Object);
                        var property = objectType.GetProperty(memberAccessNode.MemberName);
                        if (property is null)
                            throw new Exception($"类型 {objectType} 没有成员 {memberAccessNode.MemberName}");
                        NodeSymbolInfos[memberAccessNode.Object] = objectType;
                        NodeSymbolInfos[memberAccessNode] = property.PropertyType;
                        return property.PropertyType;
                    }
                    return AnalysisMemberAccessNode(memberAccessNode);
                case MemberAssignmentNode memberAssignmentNode: // 对象成员赋值
                    Type AnalysisMemberAssignmentNode(MemberAssignmentNode memberAssignmentNode)
                    {
                        var objectType = Analysis(memberAssignmentNode.Object);
                        if(objectType == typeof(object))
                        {
                            /*var property = objectType.GetProperty(memberAssignmentNode.MemberName);
                            if (property is null)
                                throw new Exception($"类型异常：类型 {objectType} 没有成员 {memberAssignmentNode.MemberName}");
                            var propertyType = property.PropertyType;
                            var valueType = Analysis(memberAssignmentNode.Value);
                            if (!propertyType.IsAssignableFrom(valueType))
                                throw new Exception($"类型异常：赋值需要 {propertyType}，实际为 {valueType}");*/
                            NodeSymbolInfos[memberAssignmentNode.Object] = typeof(object);
                            NodeSymbolInfos[memberAssignmentNode.Value] = typeof(object);
                            NodeSymbolInfos[memberAssignmentNode] = typeof(void);
                        }
                        else
                        {
                            var property = objectType.GetProperty(memberAssignmentNode.MemberName);
                            if (property is null)
                                throw new Exception($"类型异常：类型 {objectType} 没有成员 {memberAssignmentNode.MemberName}");
                            var propertyType = property.PropertyType;
                            var valueType = Analysis(memberAssignmentNode.Value);
                            if (!propertyType.IsAssignableFrom(valueType))
                                throw new Exception($"类型异常：赋值需要 {propertyType}，实际为 {valueType}");
                            NodeSymbolInfos[memberAssignmentNode.Object] = propertyType;
                            NodeSymbolInfos[memberAssignmentNode.Value] = valueType;
                            NodeSymbolInfos[memberAssignmentNode] = typeof(void);
                        }
                        
                        return typeof(void);  // 对象成员赋值语句不产生类型
                    }
                    return AnalysisMemberAssignmentNode(memberAssignmentNode);
                case MemberFunctionCallNode memberFunctionCallNode: // 对象方法调用
                    Type AnalysisMemberFunctionCallNode(MemberFunctionCallNode memberFunctionCallNode)
                    {
                        var objectType = Analysis(memberFunctionCallNode.Object);
                        var types = memberFunctionCallNode.Arguments.Select(arg => Analysis(arg)).ToArray();

                        
                        var methodInfo = objectType.GetMethod(memberFunctionCallNode.FunctionName, types);
                        if (methodInfo is null)
                        {
                            var t = objectType.GetMethods().Where(m => m.Name.Equals(memberFunctionCallNode.FunctionName)).ToArray();
                            if(t.Length > 0)
                            {
                                var content = string.Join($";{Environment.NewLine}", t.Select(m => m.ToString()));
                                throw new Exception($"类型 {objectType} 没有指定的重载方法 " +
                                    $"{memberFunctionCallNode.FunctionName}({string.Join(",", types.Select(t => t.Name))})，" +
                                    $"但存在其它重载方法：{content}");
                            }
                            else
                            {
                                throw new Exception($"类型 {objectType} 没有方法 {memberFunctionCallNode.FunctionName}");
                            }
                        }
                            
                        for (int index = 0; index < memberFunctionCallNode.Arguments.Count; index++)
                        {
                            ASTNode argNode = memberFunctionCallNode.Arguments[index];
                            Type argType = types[index];
                            NodeSymbolInfos[argNode] = argType;
                        }
                        var isAsync = EmitHelper.IsGenericTask(methodInfo.ReturnType, out var taskResult);
                        var methodReturnType = isAsync ? taskResult : methodInfo.ReturnType;
                        AsyncMethods[memberFunctionCallNode] = isAsync;
                        NodeSymbolInfos[memberFunctionCallNode.Object] = objectType;
                        NodeSymbolInfos[memberFunctionCallNode] = methodReturnType;
                        return methodReturnType;


                    }
                    return AnalysisMemberFunctionCallNode(memberFunctionCallNode);
                case FunctionCallNode functionCallNode: // 外部挂载的函数调用
                    Type AnalysisFunctionCallNode(FunctionCallNode functionCallNode)
                    {
                        // 获取流程上下文
                        if (functionCallNode.FunctionName.Equals("getFlowContext", StringComparison.OrdinalIgnoreCase))
                        {
                            return typeof(IFlowContext);
                        }
                        else if (functionCallNode.FunctionName.Equals("getScriptContext", StringComparison.OrdinalIgnoreCase))
                        {
                            return typeof(IScriptInvokeContext);
                        }

                        if (!SereinScript.FunctionInfos.TryGetValue(functionCallNode.FunctionName, out var methodInfo))
                        {
                            throw new Exception($"脚本没有挂载方法 {functionCallNode.FunctionName}");
                        }
                        var types = functionCallNode.Arguments.Select(arg => Analysis(arg)).ToArray();
                        for (int index = 0; index < functionCallNode.Arguments.Count; index++)
                        {
                            ASTNode argNode = functionCallNode.Arguments[index];
                            Type argType = types[index];
                            NodeSymbolInfos[argNode] = argType;
                        }
                        var isAsync = EmitHelper.IsGenericTask(methodInfo.ReturnType, out var taskResult);
                        var methodReturnType = isAsync  ? taskResult : methodInfo.ReturnType;
                        AsyncMethods[functionCallNode] = isAsync;
                        NodeSymbolInfos[functionCallNode] = methodReturnType;
                        return methodReturnType;
                    }
                    return AnalysisFunctionCallNode(functionCallNode);
                default: // 未定义的节点类型
                    break;
            }
            throw new NotImplementedException($"类型分析遇到未定义的节点 \"{node.GetType()}\", {node}");
        }



        /// <summary>
        /// 类型分析
        /// </summary>
        /// <param name="node"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void Analysis11111111111(ASTNode node)
        {
            switch (node)
            {
                case ProgramNode programNode: // 程序开始节点
                    break;
                case ReturnNode returnNode: // 程序退出节点
                    Analysis(returnNode); // 解析变量定义的类型
                    break;
                case NullNode nullNode: // null
                case CharNode charNode: // char字面量
                case RawStringNode rawStringNode:
                case StringNode stringNode: // 字符串字面量
                case BooleanNode booleanNode: // 布尔值字面量
                case NumberIntNode numberIntNode: // int整型数值字面量
                case NumberLongNode numberLongNode: // long整型数值字面量
                case NumberFloatNode numberFloatNode: // float浮点数值字面量
                case NumberDoubleNode numberDoubleNode: // double浮点数值字面量
                    Analysis(node);
                    break;
                case IdentifierNode identifierNode: // 变量定义
                    void AnalysisIdentifierNode(IdentifierNode identifierNode)
                    {
                        Analysis(identifierNode); // 解析变量定义的类型
                    }
                    AnalysisIdentifierNode(identifierNode);
                    break;
                case IfNode ifNode: // if语句结构
                    void AnalysisIfNode(IfNode ifNode)
                    {
                        Analysis(ifNode);

                    }
                    AnalysisIfNode(ifNode);
                    break;
                case WhileNode whileNode: // while语句结构
                    void AnalysisWhileNode(WhileNode whileNode)
                    {
                        Analysis(whileNode);
                    }
                    AnalysisWhileNode(whileNode);
                    break;
                case AssignmentNode assignmentNode: // 对象赋值语句（let x;默认赋值null。默认类型object）
                    void AnalysisAssignmentNode(AssignmentNode assignmentNode)
                    {
                        Analysis(assignmentNode);
                    }
                    AnalysisAssignmentNode(assignmentNode);
                    break;
                case BinaryOperationNode binaryOperationNode: // 二元运算操作
                    void AnalysisBinaryOperationNode(BinaryOperationNode binaryOperationNode)
                    {
                        Analysis(binaryOperationNode);
                    }
                    AnalysisBinaryOperationNode(binaryOperationNode);
                    break;
                case CollectionIndexNode collectionIndexNode: // 集合类型操作
                    void AnalysisCollectionIndexNode(CollectionIndexNode collectionIndexNode)
                    {
                        Analysis(collectionIndexNode);
                    }
                    AnalysisCollectionIndexNode(collectionIndexNode);
                    break;
                case CollectionAssignmentNode collectionAssignmentNode: // 集合赋值操作
                    void AnalysisCollectionAssignmentNode(CollectionAssignmentNode collectionAssignmentNode)
                    {
                        Analysis(collectionAssignmentNode);
                    }
                    AnalysisCollectionAssignmentNode(collectionAssignmentNode);
                    break;
                case ArrayDefintionNode arrayDefintionNode:
                    void AnalysisArrayDefintionNode(ArrayDefintionNode arrayDefintionNode)
                    {
                        Analysis(arrayDefintionNode);
                    }
                    AnalysisArrayDefintionNode(arrayDefintionNode);
                    break;
                case ClassTypeDefinitionNode classTypeDefinitionNode: // 类型定义
                    Analysis(classTypeDefinitionNode);
                    break;
                case ObjectInstantiationNode objectInstantiationNode: // 类型实例化
                    Analysis(objectInstantiationNode);
                    break;
               /* case ExpressionNode expressionNode: // 类型表达式（链式调用）
                    Analysis(expressionNode.Value);
                    break;*/
                case MemberAccessNode memberAccessNode: // 对象成员访问
                    Analysis(memberAccessNode);
                    break;
                case MemberAssignmentNode memberAssignmentNode: // 对象成员赋值
                    void AnalysisMemberAssignmentNode(MemberAssignmentNode memberAssignmentNode)
                    {
                        Analysis(memberAssignmentNode);
                    }
                    AnalysisMemberAssignmentNode(memberAssignmentNode);
                    break;
                case MemberFunctionCallNode memberFunctionCallNode: // 对象方法调用
                    Analysis(memberFunctionCallNode);
                    break;
                case FunctionCallNode functionCallNode: // 外部挂载的函数调用
                    Analysis(functionCallNode);
                    break;
                default: // 未定义的节点类型
                    break;
            }
        }


        private void ToILCompiler(ASTNode node)
        {
            switch (node)
            {
                case ProgramNode programNode: // 程序开始节点
                    break;
                case ReturnNode returnNode: // 程序退出节点
                    break;
                case NullNode nullNode: // null
                    break;
                case CharNode charNode: // char字面量
                    break;
                case StringNode stringNode: // 字符串字面量
                    break;
                case BooleanNode booleanNode: // 布尔值字面量
                    break;
                case NumberIntNode numberIntNode: // int整型数值字面量
                    break;
                case NumberLongNode numberLongNode: // long整型数值字面量
                    break;
                case NumberFloatNode numberFloatNode: // float浮点数值字面量
                    break;
                case NumberDoubleNode numberDoubleNode: // double浮点数值字面量
                    break;
                case IdentifierNode identifierNode: // 变量定义
                    break;
                case IfNode ifNode: // if语句结构
                    break;
                case WhileNode whileNode: // while语句结构
                    break;
                case AssignmentNode assignmentNode: // 对象赋值语句（let x;默认赋值null。默认类型object）
                    break;
                case BinaryOperationNode binaryOperationNode: // 二元运算操作
                    break;
                case CollectionAssignmentNode collectionAssignmentNode:
                    break;
                case CollectionIndexNode collectionIndexNode: // 集合类型操作
                    break;
                case ClassTypeDefinitionNode classTypeDefinitionNode: // 类型定义
                    break;
                case TypeNode typeNode: // 类型
                    break;
                case ObjectInstantiationNode objectInstantiationNode: // 类型实例化
                    break;
                case CtorAssignmentNode ctorAssignmentNode: // 构造器赋值
                    break;
                case MemberAccessNode memberAccessNode: // 对象成员访问
                    break;
                case MemberAssignmentNode memberAssignmentNode: // 对象成员赋值
                    break;
                case MemberFunctionCallNode memberFunctionCallNode: // 对象方法调用
                    break;
                case FunctionCallNode functionCallNode: // 外部挂载的函数调用
                    break;
                default: // 未定义的节点类型
                    break;
            }
        }

        /// <summary>
        /// 跳过名称搜索类型
        /// </summary>
        /// <param name="typeFullnameName"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static Type GetTypeOfString(string typeFullnameName)
        {
            Type? resultType = null;
            resultType = DynamicObjectHelper.GetCacheType(typeFullnameName); // 从自定义类型查询类型
            if (resultType != null)
            {
                return resultType;
            }
            
            resultType = Type.GetType(typeFullnameName); // 从命名空间查询类型
            if (resultType != null)
            {
                return resultType;
            }

            if(SereinEnv.Environment is not null && SereinEnv.Environment.FlowLibraryService is not null)
            {
                resultType = SereinEnv.Environment.FlowLibraryService.GetType(typeFullnameName);
                if (resultType != null)
                {
                    return resultType;
                }
            }

            throw new InvalidOperationException($"无法匹配类型 {typeFullnameName}");
        }

        

        /// <summary>
        /// 获取某个集合类型支持的索引参数类型
        /// </summary>
        /// <param name="collectionType">集合类型</param>
        /// <param name="indexType">索引</param>
        /// <param name="resultType">获取到的类型</param>
        /// <returns></returns>
        public static bool TryGetIndexerType(Type collectionType,  out Type indexType, out Type resultType)
        {
            indexType = null!;
            resultType = null!;

            // 检查是否是数组
            if (collectionType.IsArray)
            {
                indexType = typeof(int);
                resultType = collectionType.GetElementType()!;
                return true;
            }

            // 检查是否实现 IDictionary<K, V>
            var dictInterface = collectionType
                .GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

            if (dictInterface != null)
            {
                var args = dictInterface.GetGenericArguments();
                indexType = args[0];  // Key
                resultType = args[1]; // Value
                return true;
            }

            // 检查是否实现 IList<T>
            var listInterface = collectionType
                .GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));

            if (listInterface != null)
            {
                indexType = typeof(int);
                resultType = listInterface.GetGenericArguments()[0];
                return true;
            }

            // 检查是否有索引器属性
            var indexer = collectionType
                .GetDefaultMembers()
                .OfType<PropertyInfo>()
                .FirstOrDefault(p =>
                {
                    var args = p.GetIndexParameters();
                    return args.Length == 1;
                });

            if (indexer != null)
            {
                var @params = indexer.GetIndexParameters();
                var param = indexer.GetIndexParameters()[0];
                indexType = param.ParameterType;
                resultType = indexer.PropertyType;
                return true;
            }

            return false;
        }


       
    }
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
/* if (SymbolInfos.TryGetValue(varName, out var symbolInfo))
            {
                var state = symbolInfo.Type.IsAssignableFrom(type);
                if (!state)
                {
                    // 错误：变量[{varName}]赋值异常，[{type.FullName}]无法转换为[{symbolInfo.Type.FullName}]
                    //SereinEnv.WriteLine(InfoType.ERROR, $"[{type.FullName}]无法转化为[{symbolInfo.Type.FullName}]。源代码：{assignmentNode.Code.Replace(Environment.NewLine,"")} [行{assignmentNode.Row}]");
                    SereinEnv.WriteLine(InfoType.ERROR, $"类型异常：无法赋值变量[{varName}]，因为[{type.FullName}]无法转化为[{symbolInfo.Type.FullName}]。在[行{assignmentNode.Row}]:{assignmentNode.Code}");
                }
            }*/
