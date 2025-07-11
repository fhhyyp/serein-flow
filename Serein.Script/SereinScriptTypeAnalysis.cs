using Serein.Library;
using Serein.Library.Utils;
using Serein.Script.Node;
using Serein.Script.Symbol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reflection;
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

        public SereinScriptTypeAnalysis()
        {

        }

        public void AnalysisProgramNode(ProgramNode astNode)
        {
            NodeSymbolInfos.Clear();
            for (int i = 0; i < astNode.Statements.Count; i++)
            {
                var node = astNode.Statements[i];
                Analysis(node);
            }
            var returnNodes = astNode.Statements.Where(node => node is ReturnNode).ToArray();
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

            }

        }
       
        /// <summary>
        /// 符号表
        /// </summary>
        public Dictionary<ASTNode, Type> NodeSymbolInfos { get; } = new Dictionary<ASTNode, Type>();

        /// <summary>
        /// 类型分析、校验
        /// </summary>
        /// <param name="node"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void Analysis(ASTNode node)
        {
            switch (node)
            {
                case ProgramNode programNode: // 程序开始节点
                    break;
                case ReturnNode returnNode: // 程序退出节点
                    Evaluate(returnNode); // 解析变量定义的类型
                    break;
                case NullNode nullNode: // null
                case CharNode charNode: // char字面量
                case StringNode stringNode: // 字符串字面量
                case BooleanNode booleanNode: // 布尔值字面量
                case NumberIntNode numberIntNode: // int整型数值字面量
                case NumberLongNode numberLongNode: // long整型数值字面量
                case NumberFloatNode numberFloatNode: // float浮点数值字面量
                case NumberDoubleNode numberDoubleNode: // double浮点数值字面量
                    Evaluate(node);
                    break;
                case IdentifierNode identifierNode: // 变量定义
                    void AnalysisIdentifierNode(IdentifierNode identifierNode)
                    {
                        Evaluate(identifierNode); // 解析变量定义的类型
                    }
                    AnalysisIdentifierNode(identifierNode);
                    break;
                case IfNode ifNode: // if语句结构
                    void AnalysisIfNode(IfNode ifNode)
                    {
                        Evaluate(ifNode);
                        var conditionType = NodeSymbolInfos[ifNode.Condition]; // 获取条件部分的返回类型
                        if (conditionType != typeof(bool?) && conditionType != typeof(bool))
                        {
                            throw new NotImplementedException("if...else...条件返回值不为布尔类型变量");
                        }
                    }
                    AnalysisIfNode(ifNode);
                    break;
                case WhileNode whileNode: // while语句结构
                    void AnalysisWhileNode(WhileNode whileNode)
                    {
                        Evaluate(whileNode);
                        var conditionType = NodeSymbolInfos[whileNode.Condition]; // 获取条件部分的返回类型
                        if (conditionType != typeof(bool?) && conditionType != typeof(bool))
                        {
                            throw new NotImplementedException("if...else...条件返回值不为布尔类型变量");
                        }
                    }
                    AnalysisWhileNode(whileNode);
                    break;                
                case AssignmentNode assignmentNode: // 对象赋值语句（let x;默认赋值null。默认类型object）
                    void AnalysisAssignmentNode(AssignmentNode assignmentNode)
                    {
                        Evaluate(assignmentNode);
                    }
                    AnalysisAssignmentNode(assignmentNode);
                    break;                
                case BinaryOperationNode binaryOperationNode: // 二元运算操作
                    void AnalysisBinaryOperationNode(BinaryOperationNode binaryOperationNode)
                    {
                        Evaluate(binaryOperationNode);
                    }
                    AnalysisBinaryOperationNode(binaryOperationNode);
                    break;                
                case CollectionIndexNode collectionIndexNode: // 集合类型操作
                    void AnalysisCollectionIndexNode(CollectionIndexNode collectionIndexNode)
                    {
                        Evaluate(collectionIndexNode);
                        /*Analysis(collectionIndexNode.Collection); // 分析集合类型（变量，对象成员）
                        Analysis(collectionIndexNode.Index); // 分析索引类型

                        var collectionType = NodeSymbolInfos[collectionIndexNode.Collection];
                        var indexExprType = NodeSymbolInfos[collectionIndexNode.Index];

                        if (!TryGetIndexerType(collectionType, out var expectedIndexType, out var resultType))
                            throw new Exception($"类型 {collectionType} 不支持索引操作");

                        if (!expectedIndexType.IsAssignableFrom(indexExprType))
                            throw new Exception($"索引类型不匹配：需要 {expectedIndexType}，实际为 {indexExprType}");
                        NodeSymbolInfos[collectionIndexNode] = resultType;*/
                    }
                    AnalysisCollectionIndexNode(collectionIndexNode);
                    break;                
                case ClassTypeDefinitionNode classTypeDefinitionNode: // 类型定义
                    Evaluate(classTypeDefinitionNode);
                    break;
                case ObjectInstantiationNode objectInstantiationNode: // 类型实例化
                    Evaluate(objectInstantiationNode);
                    break;
                case ObjectMemberExpressionNode objectMemberExpressionNode: // 类型表达式（链式调用）
                    Evaluate(objectMemberExpressionNode);
                    break;
                case MemberAccessNode memberAccessNode: // 对象成员访问
                    Evaluate(memberAccessNode);
                    break;                
                case MemberAssignmentNode memberAssignmentNode: // 对象成员赋值
                    void AnalysisMemberAssignmentNode(MemberAssignmentNode memberAssignmentNode)
                    {
                        Evaluate(memberAssignmentNode);
                    }
                    AnalysisMemberAssignmentNode(memberAssignmentNode);
                    break;                
                case MemberFunctionCallNode memberFunctionCallNode: // 对象方法调用
                    Evaluate(memberFunctionCallNode);
                    break;
                case FunctionCallNode functionCallNode: // 外部挂载的函数调用
                    Evaluate(functionCallNode);
                    break;
                default: // 未定义的节点类型
                    break;
            }
        }


        /// <summary>
        /// 类型获取
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        /// <exception cref="NotImplementedException"></exception>
        private Type Evaluate(ASTNode node)
        {
            switch (node)
            {
                case ProgramNode programNode: // 程序开始节点
                    NodeSymbolInfos[programNode] = typeof(void);
                    return typeof(void);
                case ReturnNode returnNode: // 程序退出节点
                    Type EvaluateReturnNode(ReturnNode returnNode)
                    {
                        var resultType = Evaluate(returnNode.Value);
                        NodeSymbolInfos[returnNode.Value] = resultType;
                        NodeSymbolInfos[returnNode] = resultType;
                        return resultType;
                    }
                    return EvaluateReturnNode(returnNode);
                case NullNode nullNode: // null
                    NodeSymbolInfos[nullNode] = typeof(object);
                    return typeof(object);
                case CharNode charNode: // char字面量
                    NodeSymbolInfos[charNode] = typeof(char);
                    return typeof(char);
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
                    Type EvaluateIdentifierNode(IdentifierNode identifierNode)
                    {
                        var cacheNode = NodeSymbolInfos.Keys.FirstOrDefault(n => n is IdentifierNode idNode && idNode.Name == identifierNode.Name);
                        Type type = cacheNode is null ? typeof(object) : NodeSymbolInfos[cacheNode]; 
                        NodeSymbolInfos[identifierNode] = type;
                        return type;
                    }
                    return EvaluateIdentifierNode(identifierNode);
                case IfNode ifNode: // if语句结构
                    Type EvaluateIfNode(IfNode ifNode)
                    {
                        var conditionType = Evaluate(ifNode.Condition); // 获取条件语句部分的返回类型
                        NodeSymbolInfos[ifNode.Condition] = conditionType;
                        if (conditionType == typeof(bool?) || conditionType == typeof(bool))
                        {
                            foreach (var item in ifNode.TrueBranch)
                            {
                                var itemType = Evaluate(item); // 解析真分支的语句块
                                NodeSymbolInfos[item] = itemType;
                            }
                            foreach (var item in ifNode.FalseBranch)
                            {
                                var itemType = Evaluate(item); // 解析假分支的语句块
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
                    return EvaluateIfNode(ifNode);
                case WhileNode whileNode: // while语句结构
                    Type EvaluateWhileNode(WhileNode whileNode)
                    {
                        var conditionType = Evaluate(whileNode.Condition); // 获取条件语句部分的返回类型
                        NodeSymbolInfos[whileNode.Condition] = conditionType;
                        if (conditionType == typeof(bool?) || conditionType == typeof(bool))
                        {
                            foreach (var item in whileNode.Body)
                            {
                                var itemType = Evaluate(item); // 解析真分支的语句块
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
                    return EvaluateWhileNode(whileNode);
                case AssignmentNode assignmentNode:
                    // 对象赋值语句（let x;默认赋值null。默认类型object）
                    Type EvaluateAssignmentNode(AssignmentNode assignmentNode)
                    {
                        var targetType = Evaluate(assignmentNode.Target);
                        var valueType = Evaluate (assignmentNode.Value);
                        if (!targetType.IsAssignableFrom(valueType))
                            throw new Exception($"索引类型不匹配：需要 {targetType}，实际为 {valueType}");
                        NodeSymbolInfos[assignmentNode.Value] = valueType;
                        NodeSymbolInfos[assignmentNode.Target] = valueType;
                        NodeSymbolInfos[assignmentNode] = typeof(void); // 赋值语句不产生类型
                        return targetType;
                    }
                    return EvaluateAssignmentNode(assignmentNode);
                case BinaryOperationNode binaryOperationNode: // 二元运算操作
                    Type EvaluateBinaryOperationNode(BinaryOperationNode binaryOperationNode)
                    {
                        var leftType = Evaluate(binaryOperationNode.Left); // 递归判断左值类型
                        var rightType = Evaluate(binaryOperationNode.Right); // 递归判断右值类型
                        var op = binaryOperationNode.Operator;
                        var resultType = BinaryOperationEvaluator.EvaluateType(leftType, op, rightType);
                        NodeSymbolInfos[binaryOperationNode.Left] = leftType;
                        NodeSymbolInfos[binaryOperationNode.Right] = rightType;
                        NodeSymbolInfos[binaryOperationNode] = resultType; 
                        return resultType;
                    }
                    return EvaluateBinaryOperationNode(binaryOperationNode);
                case CollectionIndexNode collectionIndexNode: // 集合类型操作，获取集合操作后返回的类型
                    Type EvaluateCollectionIndexNode(CollectionIndexNode collectionIndexNode)
                    {
                        var collectionType = Evaluate(collectionIndexNode.Collection); // 分析集合类型（变量，对象成员）
                        var indexExprType  = Evaluate(collectionIndexNode.Index); // 分析索引类型
                        if (!TryGetIndexerType(collectionType, out var expectedIndexType, out var resultType))
                            throw new Exception($"类型 {collectionType} 不支持索引操作");

                        if (!expectedIndexType.IsAssignableFrom(indexExprType))
                            throw new Exception($"索引类型不匹配：需要 {expectedIndexType}，实际为 {indexExprType}");
                        NodeSymbolInfos[collectionIndexNode.Collection] = collectionType;
                        NodeSymbolInfos[collectionIndexNode.Index] = indexExprType;
                        NodeSymbolInfos[collectionIndexNode] = resultType;
                        return resultType; 
                    }
                    return EvaluateCollectionIndexNode(collectionIndexNode);
                case ClassTypeDefinitionNode classTypeDefinitionNode: // 类型定义
                    Type EvaluateClassTypeDefinitionNode(ClassTypeDefinitionNode classTypeDefinitionNode)
                    {
                        var classType = DynamicObjectHelper.GetCacheType(classTypeDefinitionNode.ClassName);
                        if (classType is null)
                            classType = DynamicObjectHelper.CreateTypeWithProperties(classTypeDefinitionNode.Fields, classTypeDefinitionNode.ClassName);
                        NodeSymbolInfos[classTypeDefinitionNode] = classType;
                        return classType;
                    }
                    return EvaluateClassTypeDefinitionNode(classTypeDefinitionNode);
                case ObjectInstantiationNode objectInstantiationNode: // 类型实例化
                    Type EvaluateObjectInstantiationNode(ObjectInstantiationNode objectInstantiationNode)
                    {
                        Type? resultType = null;
                        try
                        {
                            resultType = Type.GetType(objectInstantiationNode.TypeName); // 从命名空间查询类型
                        }
                        finally
                        {
                            if (resultType is null)
                            {
                                resultType = DynamicObjectHelper.GetCacheType(objectInstantiationNode.TypeName); // 从自定义类型查询类型
                            }
                        }
                        NodeSymbolInfos[objectInstantiationNode] = resultType;
                        return resultType;
                    }
                    return EvaluateObjectInstantiationNode(objectInstantiationNode);
                case ObjectMemberExpressionNode objectMemberExpressionNode: // 类型表达式（链式调用）
                    Type EvaluateObjectMemberExpressionNode(ObjectMemberExpressionNode objectMemberExpressionNode)
                    {
                        // 1. 对象成员获取 MemberAccessNode
                        // 2. 对象方法调用 MemberFunctionCallNode
                        // 3. 对象集合成员获取 CollectionIndexNode
                        Type? resultType = Evaluate(objectMemberExpressionNode.Value);
                        NodeSymbolInfos[objectMemberExpressionNode.Value] = resultType;
                        NodeSymbolInfos[objectMemberExpressionNode] = resultType;
                        return resultType;
                    }
                    return EvaluateObjectMemberExpressionNode(objectMemberExpressionNode);
                case MemberAccessNode memberAccessNode: // 对象成员访问
                    Type EvaluateMemberAccessNode(MemberAccessNode memberAccessNode)
                    {
                        var objectType = Evaluate(memberAccessNode.Object);
                        var property = objectType.GetProperty(memberAccessNode.MemberName);
                        if (property is null)
                            throw new Exception($"类型 {objectType} 没有成员 {memberAccessNode.MemberName}");
                        NodeSymbolInfos[memberAccessNode.Object] = objectType;
                        NodeSymbolInfos[memberAccessNode] = property.PropertyType;
                        return property.PropertyType;
                    }
                    return EvaluateMemberAccessNode(memberAccessNode);
                case MemberAssignmentNode memberAssignmentNode: // 对象成员赋值
                    Type EvaluateMemberAssignmentNode(MemberAssignmentNode memberAssignmentNode)
                    {
                        var objectType = Evaluate(memberAssignmentNode.Object);
                        var property = objectType.GetProperty(memberAssignmentNode.MemberName);
                        if(property is null)
                            throw new Exception($"类型异常：类型 {objectType} 没有成员 {memberAssignmentNode.MemberName}");
                        var propertyType = property.PropertyType;
                        var valueType = Evaluate(memberAssignmentNode.Value);
                        if (!propertyType.IsAssignableFrom(valueType))
                            throw new Exception($"类型异常：赋值需要 {propertyType}，实际为 {valueType}");
                        NodeSymbolInfos[memberAssignmentNode.Object] = propertyType;
                        NodeSymbolInfos[memberAssignmentNode.Value] = valueType;
                        NodeSymbolInfos[memberAssignmentNode] = typeof(void);
                        return typeof(void);  // 对象成员赋值语句不产生类型
                    }
                    return EvaluateMemberAssignmentNode(memberAssignmentNode);
                case MemberFunctionCallNode memberFunctionCallNode: // 对象方法调用
                    Type EvaluateMemberFunctionCallNode(MemberFunctionCallNode memberFunctionCallNode)
                    {
                        var objectType = Evaluate(memberFunctionCallNode.Object);
                        var types = memberFunctionCallNode.Arguments.Select(arg => Evaluate(arg)).ToArray();
                        var methodInfo = objectType.GetMethod(memberFunctionCallNode.FunctionName, types);
                        if (methodInfo is null)
                            throw new Exception($"类型 {objectType} 没有方法 {memberFunctionCallNode.FunctionName}");
                        for (int index = 0; index < memberFunctionCallNode.Arguments.Count; index++)
                        {
                            ASTNode argNode = memberFunctionCallNode.Arguments[index];
                            Type argType = types[index];
                            NodeSymbolInfos[argNode] = argType;
                        }
                        NodeSymbolInfos[memberFunctionCallNode.Object] = objectType;
                        NodeSymbolInfos[memberFunctionCallNode] = methodInfo.ReturnType;
                        return methodInfo.ReturnType;
                    }
                    return EvaluateMemberFunctionCallNode(memberFunctionCallNode);
                case FunctionCallNode functionCallNode: // 外部挂载的函数调用
                    Type EvaluateFunctionCallNode(FunctionCallNode functionCallNode)
                    {
                        if(!SereinScriptInterpreter.FunctionInfoTable.TryGetValue(functionCallNode.FunctionName, out var methodInfo))
                        {
                            throw new Exception($"脚本没有挂载方法 {functionCallNode.FunctionName}");
                        }
                        var types = functionCallNode.Arguments.Select(arg => Evaluate(arg)).ToArray();
                        for (int index = 0; index < functionCallNode.Arguments.Count; index++)
                        {
                            ASTNode argNode = functionCallNode.Arguments[index];
                            Type argType = types[index];
                            NodeSymbolInfos[argNode] = argType;
                        }
                        NodeSymbolInfos[functionCallNode] = methodInfo.ReturnType;
                        return methodInfo.ReturnType;
                    }
                    return EvaluateFunctionCallNode(functionCallNode);
                default: // 未定义的节点类型
                    break;
            }
            throw new NotImplementedException();
        }










        private void Analysis2(ASTNode node)
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
                case CollectionIndexNode collectionIndexNode: // 集合类型操作
                    break;
                case ClassTypeDefinitionNode classTypeDefinitionNode: // 类型定义
                    break;
                case ObjectInstantiationNode objectInstantiationNode: // 类型实例化
                    break;
                case ObjectMemberExpressionNode objectMemberExpressionNode: // 类型表达式（链式调用）
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
        /// 获取某个集合类型支持的索引参数类型
        /// </summary>
        /// <param name="collectionType">集合类型</param>
        /// <param name="indexType">索引</param>
        /// <param name="resultType">获取到的类型</param>
        /// <returns></returns>
        public static bool TryGetIndexerType(Type collectionType, out Type indexType, out Type resultType)
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
                var param = indexer.GetIndexParameters()[0];
                indexType = param.ParameterType;
                resultType = indexer.PropertyType;
                return true;
            }

            return false;
        }









        #region 初始化符号表
        /// <summary>
        /// 符号表
        /// </summary>
        public Dictionary<string, SymbolInfo> SymbolInfos { get; } = new Dictionary<string, SymbolInfo>();


       /* public SereinScriptTypeAnalysis(ProgramNode programNode)
        {
            SymbolInfos.Clear(); // 清空符号表

            // 初始化符号表
            foreach (ASTNode astNode in programNode.Statements)
            {
                var type = Trace(astNode);
                if (type is null) continue;
                var info = Analyse(astNode, type);
                if (info != null)
                {
                    SymbolInfos[info.Name] = info;
                }
            }

            // 类型分析
            foreach (ASTNode astNode in programNode.Statements)
            {
            }
        }
*/

        private Type? GetTypeOnMemberFunctionCallNode(ASTNode objectNode)
        {
            Type objectType = null;
            if (objectNode is IdentifierNode identifierNode)
            {
                if (SymbolInfos.TryGetValue(identifierNode.Name, out var symbolInfo))
                {
                    objectType = symbolInfo.Type;
                }
            }
            return objectType;
        }

        private Type? GetMethodReturnType(Type type, string methodName)
        {
            if (type is null) return null;
            var methodInfos = type.GetMethods();
            var methodInfo = methodInfos.FirstOrDefault(md => md.Name == methodName);
            var returnType = methodInfo?.ReturnType;
            return returnType;
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
                    if(SymbolInfos.TryGetValue(identifierNode.Name, out var varSymbolInfo))
                    {
                        return varSymbolInfo.Type; // 返回定义的类型
                    }
                    return typeof(object); // 默认为 object
                case AssignmentNode assignmentNode: // 赋值行为
                    var targetType = Trace(assignmentNode.Target);
                    var valueType = Trace(assignmentNode.Value);
                    if (targetType.IsAssignableFrom(valueType))
                    {
                        if(assignmentNode.Target is IdentifierNode identifierNode
                            && !SymbolInfos.ContainsKey(identifierNode.Name))
                        {
                            return valueType;
                        }
                        return targetType;
                    }
                    else
                    {
                        throw new Exception("无法转换类型");
                    }
                case BinaryOperationNode binOpNode: // 递归计算二元操作
                    var leftType = Trace(binOpNode.Left);
                    var op = binOpNode.Operator;
                    var rightType = Trace(binOpNode.Right);
                    var resultType = BinaryOperationEvaluator.EvaluateType(leftType, op, rightType);
                    return resultType;
                case ClassTypeDefinitionNode classTypeDefinitionNode:
                    var definitionType = DynamicObjectHelper.CreateTypeWithProperties(classTypeDefinitionNode.Fields, classTypeDefinitionNode.ClassName);
                    return definitionType;
                case ObjectInstantiationNode objectInstantiationNode: // 创建对象
                    var typeName = objectInstantiationNode.TypeName;
                    var objectType = Type.GetType(typeName);
                    objectType ??= DynamicObjectHelper.GetCacheType(typeName);
                    return objectType;
                case FunctionCallNode callNode: // 调用方法
                    return null;
                case MemberAssignmentNode memberAssignmentNode:
                    var leftValueType = Trace(memberAssignmentNode.Object);
                    var propertyType = leftValueType.GetProperty(memberAssignmentNode.MemberName)?.PropertyType;
                    var rightValueType = Trace(memberAssignmentNode.Value);
                    if (propertyType is not null && !propertyType.IsAssignableFrom(rightValueType))
                    {
                        throw new Exception("无法转换类型");
                    }
                    break;
                case MemberFunctionCallNode memberFunctionCallNode: // 对象方法调用
                    var objectNode = memberFunctionCallNode.Object;
                    var objType = GetTypeOnMemberFunctionCallNode(objectNode);
                    var methodName = memberFunctionCallNode.FunctionName;
                    return GetMethodReturnType(objType, methodName);
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
                    if(assignmentNode.Target is IdentifierNode identifierNode1)
                    {
                        return new SymbolInfo
                        {
                            Name = identifierNode1.Name,
                            Node = node,
                            Type = type,
                        };
                    }
                    break;
                    
                case BinaryOperationNode binOpNode: // 递归计算二元操作
                    break;
                //case ClassTypeDefinitionNode classTypeDefinitionNode
                case ObjectInstantiationNode objectInstantiationNode: // 创建对象
                    break;
                case FunctionCallNode callNode: // 调用方法
                    break;
                case MemberFunctionCallNode memberFunctionCallNode: // 对象方法调用
                    break;
                case MemberAccessNode memberAccessNode: // 对象成员访问
                    break;
                case CollectionIndexNode collectionIndexNode:
                    break;
                case ReturnNode returnNode: // 返回内容
                    break;
                default:
                    break;
                    //throw new SereinSciptException(node, $"解释器 EvaluateAsync() 未实现{node}节点行为");
            }

            return null;
        }

        #endregion

       
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
