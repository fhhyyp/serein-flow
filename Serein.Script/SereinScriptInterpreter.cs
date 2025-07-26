using Newtonsoft.Json.Linq;
using Serein.Library;
using Serein.Library.Utils;
using Serein.Script.Node;
using Serein.Script.Node.FlowControl;
using System;
using System.ComponentModel.Design;
using System.Reactive;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml.Linq;
namespace Serein.Script
{

    /// <summary>
    /// 脚本解释器，负责执行 Serein 脚本
    /// </summary>
    public class SereinScriptInterpreter
    {
        private readonly Dictionary<ASTNode, Type> symbolInfos;
        /// <summary>
        /// 缓存对象方法调用节点
        /// </summary>
        //private Dictionary<MemberFunctionCallNode, DelegateDetails> MethodNodeDelegateCaches { get; } = new Dictionary<MemberFunctionCallNode, DelegateDetails>();
        private static Dictionary<ASTNode, DelegateDetails> ASTDelegateDetails { get; } = new Dictionary<ASTNode, DelegateDetails>();


        public SereinScriptInterpreter(Dictionary<ASTNode, Type> symbolInfos)
        {
            this.symbolInfos = symbolInfos;
        }

        public async Task<object?> InterpreterAsync(IScriptInvokeContext context, ProgramNode programNode)
        {
            var nodes = programNode.Statements;
            object? result = null;
            foreach (var node in nodes)
            {
                result = await InterpretAsync(context, node); // 解释每个节点
                if (context.IsReturn) break; // 如果需要提前返回，则停止执行
            }
            return result; // 返回最后一个节点的结果
        }

        private async Task<object?> InterpretAsync(IScriptInvokeContext context, ASTNode node)
        {
            switch (node)
            {
                case ProgramNode programNode: // 程序开始节点
                    throw new Exception();
                case ReturnNode returnNode: // 程序退出节点
                    async Task<object?> InterpreterReturnNodeAsync(IScriptInvokeContext context, ReturnNode returnNode)
                    {
                        object? returnValue = await InterpretAsync(context, returnNode.Value);
                        context.IsNeedReturn = true; 
                        return returnValue;
                    }
                    return await InterpreterReturnNodeAsync(context, returnNode);
                #region 字面量节点
                case NullNode nullNode: // null
                    return null; // 返回 null
                case CharNode charNode: // char字面量
                    return charNode.Value; // 返回字符值
                case StringNode stringNode: // 字符串字面量
                    return stringNode.Value; // 返回字符串值
                case BooleanNode booleanNode: // 布尔值字面量
                    return booleanNode.Value; // 返回布尔值
                case NumberIntNode numberIntNode: // int整型数值字面量
                    return numberIntNode.Value; // 返回 int 整型数值
                case NumberLongNode numberLongNode: // long整型数值字面量
                    return numberLongNode.Value; // 返回 long 整型数值
                case NumberFloatNode numberFloatNode: // float浮点数值字面量
                    return numberFloatNode.Value; // 返回 float 浮点数值
                case NumberDoubleNode numberDoubleNode: // double浮点数值字面量
                    return numberDoubleNode.Value; // 返回 double 浮点数值
                #endregion
                case IdentifierNode identifierNode: // 变量定义
                    return context.GetVarValue(identifierNode.Name);
                case IfNode ifNode: // if语句结构
                    async Task<object?> InterpreterIfNodeAsync(IScriptInvokeContext context, IfNode ifNode)
                    {
                        var result = await InterpretAsync(context, ifNode.Condition) ?? throw new SereinSciptParserException(ifNode, $"条件语句返回了 null");
                        if (result is not bool condition) throw new SereinSciptParserException(ifNode, "条件语句返回值不为 bool 类型");
                        var branchNodes = condition ? ifNode.TrueBranch : ifNode.FalseBranch;
                        if (branchNodes.Count  == 0) return default;
                        object? data = default;
                        foreach (var branchNode in branchNodes)
                        {
                            data = await InterpretAsync(context, branchNode);
                            if (branchNode is ReturnNode) // 遇到 Return 语句 提前退出
                            {
                                context.IsNeedReturn = true;
                                break;
                            }
                        }
                        return data;
                    }

                    return await InterpreterIfNodeAsync(context, ifNode);
                case WhileNode whileNode: // while语句结构
                    async Task<object?> InterpreterWhileNodeAsync(IScriptInvokeContext context, WhileNode whileNode)
                    {
                        object? data = default;
                        while (true)
                        {
                            var result = await InterpretAsync(context, whileNode.Condition) ?? throw new SereinSciptParserException(whileNode, $"循环节点条件返回了 null");
                            if (result is not bool condition) throw new SereinSciptParserException(whileNode, "循环节点条件返回值不为 bool 类型");
                            if (!condition) break;
                            if (whileNode.Body.Count == 0) break;
                            foreach (var node in whileNode.Body)
                            {
                                data = await InterpretAsync(context, node);
                                if (node is ReturnNode) // 遇到 Return 语句 提前退出
                                {
                                    context.IsNeedReturn = true;
                                    break ;
                                }
                            }
                        }
                        return data;
                    }
                    await InterpreterWhileNodeAsync(context, whileNode);
                    return default;
                case AssignmentNode assignmentNode: // 变量赋值语句
                    async Task InterpreterAssignmentNodeAsync(IScriptInvokeContext context, AssignmentNode assignmentNode)
                    {
                        if (assignmentNode.Target is IdentifierNode identifierNode)
                        {
                            var value = await InterpretAsync(context, assignmentNode.Value);
                            context.SetVarValue(identifierNode.Name, value);
                        }
                    }
                    await InterpreterAssignmentNodeAsync(context, assignmentNode);
                    return default;
                case BinaryOperationNode binaryOperationNode: // 二元运算操作
                    async Task<object?> InterpreterBinaryOperationNodeAsync(IScriptInvokeContext context, BinaryOperationNode binaryOperationNode)
                    {
                        // 递归计算二元操作
                        var left = await InterpretAsync(context, binaryOperationNode.Left);
                        if (left == null ) throw new SereinSciptParserException(binaryOperationNode.Left, $"左值尝试使用 null");
                        var right = await InterpretAsync(context, binaryOperationNode.Right);
                        if (right == null) throw new SereinSciptParserException(binaryOperationNode.Right, "右值尝试使用计算 null");
                        var op = binaryOperationNode.Operator;
                        var result = BinaryOperationEvaluator.EvaluateValue(left, op, right);
                        return result;
                    }
                    return await InterpreterBinaryOperationNodeAsync(context, binaryOperationNode);
                case CollectionAssignmentNode collectionAssignmentNode: // 集合赋值节点
                    async Task InterpreterCollectionAssignmentNodeAsync(IScriptInvokeContext context, CollectionAssignmentNode collectionAssignmentNode)
                    {
                        var collectionValue = await InterpretAsync(context, collectionAssignmentNode.Collection.Collection);
                        if (collectionValue is null)
                        {
                            throw new ArgumentNullException($"解析{collectionAssignmentNode}节点时，集合返回空。");
                        }
                        var indexValue = await InterpretAsync(context, collectionAssignmentNode.Collection.Index);
                        if (indexValue is null)
                        {
                            throw new ArgumentNullException($"解析{collectionAssignmentNode}节点时，索引返回空。");
                        }
                        var valueValue = await InterpretAsync(context, collectionAssignmentNode.Value);
                        await SetCollectionValueAsync(collectionAssignmentNode, collectionValue, indexValue, valueValue);
                    }
                    await InterpreterCollectionAssignmentNodeAsync(context, collectionAssignmentNode);
                    return default; 
                case CollectionIndexNode collectionIndexNode: // 集合获取索引对应值
                    async Task<object?> InterpreterCollectionIndexNodeAsync(IScriptInvokeContext context, CollectionIndexNode collectionIndexNode)
                    {
                        var collectionValue = await InterpretAsync(context, collectionIndexNode.Collection);
                        if (collectionValue is null)
                        {
                            throw new ArgumentNullException($"解析{collectionIndexNode}节点时，集合返回空。");
                        }
                        var indexValue = await InterpretAsync(context, collectionIndexNode.Index);
                        if (indexValue is null)
                        {
                            throw new ArgumentNullException($"解析{collectionIndexNode}节点时，索引返回空。");
                        }
                        var result = await GetCollectionValueAsync(collectionIndexNode, collectionValue, indexValue);
                        return result;
                    }
                    return await InterpreterCollectionIndexNodeAsync(context, collectionIndexNode);
                case ClassTypeDefinitionNode classTypeDefinitionNode: // 类型定义
                    void InterpreterClassTypeDefinitionNode(IScriptInvokeContext context, ClassTypeDefinitionNode classTypeDefinitionNode)
                    {
                        var className = classTypeDefinitionNode.ClassType.TypeName;
                        if (SereinScript.MountType.ContainsKey(className))
                        {
                            //SereinEnv.WriteLine(InfoType.WARN, $"异常信息 : 类型重复定义，代码在第{classTypeDefinitionNode.Row}行: {classTypeDefinitionNode.Code.Trim()}");
                            return;
                        }
                        if (DynamicObjectHelper.GetCacheType(className) == null)
                        {
                            var propertyTypes = classTypeDefinitionNode.Propertys.ToDictionary(p => p.Key, p => symbolInfos[p.Value]);
                            var type = DynamicObjectHelper.CreateTypeWithProperties(propertyTypes, className); // 实例化新的类型
                            SereinScript.MountType[className] = type; // 定义对象
                        }
                    }
                    InterpreterClassTypeDefinitionNode(context, classTypeDefinitionNode);
                    return default;
                case TypeNode typeNode: // 类型
                    return default;
                case ObjectInstantiationNode objectInstantiationNode: // 类型实例化
                    async Task<object?> InterpreterObjectInstantiationNodeAsync(IScriptInvokeContext context, ObjectInstantiationNode objectInstantiationNode)
                    {
                        if (!SereinScript.MountType.TryGetValue(objectInstantiationNode.Type.TypeName, out var type))
                        {
                            type = symbolInfos[objectInstantiationNode.Type];
                            if (type is null)
                            {
                                throw new SereinSciptParserException(objectInstantiationNode, $"使用了未定义的类型\"{objectInstantiationNode.Type.TypeName}\"");
                            }
                        }

                        // 获取参数
                        var args = objectInstantiationNode.Arguments.Count == 0 ? [] :
                                    (await objectInstantiationNode.Arguments.SelectAsync(
                                        async argNode => await InterpretAsync(context, argNode)))
                                    .ToArray();

                        var obj = Activator.CreateInstance(type, args: args);// 创建对象
                        if (obj is null)
                        {
                            throw new SereinSciptParserException(objectInstantiationNode, $"类型创建失败\"{objectInstantiationNode.Type.TypeName}\"");
                        }

                        for (int i = 0; i < objectInstantiationNode.CtorAssignments.Count; i++)
                        {
                            var ctorAssignmentNode = objectInstantiationNode.CtorAssignments[i];
                            var propertyName = ctorAssignmentNode.MemberName;
                            var value = await InterpretAsync(context, ctorAssignmentNode.Value);
                            await SetPropertyValueAsync(ctorAssignmentNode, obj, propertyName, value);
                        }
                        return obj;
                    }
                    return await InterpreterObjectInstantiationNodeAsync(context, objectInstantiationNode);
                case CtorAssignmentNode ctorAssignmentNode:
                    return default;
                /*case ExpressionNode expressionNode: // 类型表达式（链式调用）
                    return await InterpretAsync(context, expressionNode.Value); // 直接计算表达式的值*/
                case MemberAccessNode memberAccessNode: // 对象成员访问
                    async Task<object?> InterpreterMemberAccessNodeAsync(IScriptInvokeContext context, MemberAccessNode memberAccessNode)
                    {
                        var target = await InterpretAsync(context, memberAccessNode.Object);
                        var memberName = memberAccessNode.MemberName;
                        if(target is null) throw new SereinSciptParserException(memberAccessNode, $"无法获取成员，对象为 null \"{memberAccessNode.Object.Code}\"");
                        var value = await GetPropertyValueAsync(memberAccessNode, target, memberName);
                        return value;
                    }
                    return await InterpreterMemberAccessNodeAsync(context, memberAccessNode);
                case MemberAssignmentNode memberAssignmentNode: // 对象成员赋值
                    async Task<object?> InterpreterMemberAssignmentNodeAsync(IScriptInvokeContext context, MemberAssignmentNode memberAssignmentNode)
                    {
                        var target = await InterpretAsync(context, memberAssignmentNode.Object);
                        var memberName = memberAssignmentNode.MemberName;
                        var value = await InterpretAsync(context, memberAssignmentNode.Value);
                        if (target is null) throw new SereinSciptParserException(memberAssignmentNode, $"无法设置成员，对象为 null \"{memberAssignmentNode.Object.Code}\"");
                        await SetPropertyValueAsync(memberAssignmentNode, target, memberName, value);
                        return value;
                    }
                    return await InterpreterMemberAssignmentNodeAsync(context, memberAssignmentNode);
                case MemberFunctionCallNode memberFunctionCallNode: // 对象方法调用
                    async Task<object?> InterpreterMemberFunctionCallNodeAsync(IScriptInvokeContext context, MemberFunctionCallNode memberFunctionCallNode)
                    {
                        var target = await InterpretAsync(context, memberFunctionCallNode.Object);
                        if (!ASTDelegateDetails.TryGetValue(memberFunctionCallNode, out DelegateDetails? delegateDetails))
                        {
                            var methodName = memberFunctionCallNode.FunctionName;
                            var methodInfo = memberFunctionCallNode.Arguments.Count == 0 ? target?.GetType().GetMethod(methodName, []) : target?.GetType().GetMethod(methodName);// 获取参数列表的类型
                            if (methodInfo is null) throw new SereinSciptParserException(memberFunctionCallNode, $"对象没有方法\"{memberFunctionCallNode.FunctionName}\"");
                            delegateDetails = new DelegateDetails(methodInfo);
                            ASTDelegateDetails[memberFunctionCallNode] = delegateDetails;
                        } // 查询是否有缓存


                        // 获取参数
                        var arguments = memberFunctionCallNode.Arguments.Count == 0 ? [] :
                                               (await memberFunctionCallNode.Arguments.SelectAsync(
                                                   async argNode => await InterpretAsync(context, argNode)))
                                               .ToArray();

                        // 调用方法
                        var reuslt = await delegateDetails.InvokeAsync(target, arguments);
                        return reuslt;
                    }
                    return await InterpreterMemberFunctionCallNodeAsync(context, memberFunctionCallNode);
                case FunctionCallNode functionCallNode: // 外部挂载的函数调用
                    async Task<object?> InterpreterFunctionCallNodeAsync(IScriptInvokeContext context, FunctionCallNode functionCallNode)
                    {
                        // 获取流程上下文
                        if (context.FlowContext != null && functionCallNode.FunctionName.Equals("getFlowApi", StringComparison.OrdinalIgnoreCase))
                        {
                            return context.FlowContext;
                        }
                        else if (functionCallNode.FunctionName.Equals("getScriptApi", StringComparison.OrdinalIgnoreCase))
                        {
                            return context;
                        }

                        // 获取参数
                        var arguments = functionCallNode.Arguments.Count == 0 ? [] :
                                               (await functionCallNode.Arguments.SelectAsync(
                                                   async argNode => await InterpretAsync(context, argNode)))
                                               .ToArray();


                        var funcName = functionCallNode.FunctionName;

                        object? instance = null; // 静态方法不需要传入实例，所以可以传入null 

                        // 查找并执行对应的函数
                        if (!SereinScript.FunctionDelegates.TryGetValue(funcName, out DelegateDetails? function))
                            throw new SereinSciptParserException(functionCallNode, $"没有挂载方法\"{functionCallNode.FunctionName}\""); 

                       /* if (!function.EmitMethodInfo.IsStatic)
                        {
                            if (!SereinScript.DelegateInstances.TryGetValue(funcName, out var action))
                            {
                                throw new SereinSciptException(functionCallNode, $"挂载方法 {funcName} 时需要同时给定获取实例的 Func<object>");
                            }

                            instance = action.Invoke();// 非静态的方法需要获取相应的实例
                            if (instance is null)
                            {
                                throw new SereinSciptException(functionCallNode, $"函数 {funcName} 尝试获取实例时返回了 null ");
                            }
                        }*/

                        var result = await function.InvokeAsync(instance, arguments);
                        return result;
                    }
                    return await InterpreterFunctionCallNodeAsync(context, functionCallNode);
                default: // 未定义的节点类型
                    throw new Exception($"解释器未实现的节点类型 {node.GetType()}");
            }
        }


        
        /// <summary>
        /// 设置对象成员
        /// </summary>
        /// <param name="node">节点，用于缓存委托避免重复反射</param>
        /// <param name="target">对象</param>
        /// <param name="memberName">属性名称</param>
        /// <param name="value">属性值</param>
        /// <exception cref="Exception"></exception>
        private async Task SetPropertyValueAsync(ASTNode node, object target, string memberName, object? value)
        {
            if(ASTDelegateDetails.TryGetValue(node ,out DelegateDetails? delegateDetails))
            {
                await delegateDetails.InvokeAsync(target, [value]);
                return;
            }

            var targetType = target?.GetType();
            if (targetType is null) return;
            var propertyInfo = targetType.GetProperty(memberName);
            if (propertyInfo is null)
            {
                FieldInfo? fieldInfo = target?.GetType().GetRuntimeField(memberName);
                if (fieldInfo is null)
                {
                    throw new Exception($"类型 {targetType} 对象没有成员\"{memberName}\"");
                }
                else
                {
                    delegateDetails = new DelegateDetails(fieldInfo, DelegateDetails.GSType.Set);
                    ASTDelegateDetails[node] = delegateDetails; // 缓存委托
                    await delegateDetails.InvokeAsync(target, [value]);

                    //var convertedValue = Convert.ChangeType(value, fieldInfo.FieldType);
                    //fieldInfo.SetValue(target, convertedValue);
                }
            }
            else
            {
                delegateDetails = new DelegateDetails(propertyInfo, DelegateDetails.GSType.Set);
                ASTDelegateDetails[node] = delegateDetails; // 缓存委托
                await delegateDetails.InvokeAsync(target, [value]);

                /*if (value is null)
                {
                    propertyInfo.SetValue(target, null);
                    return;
                }
                var valueTtpe = value.GetType();
                if (propertyInfo.PropertyType.IsAssignableFrom(valueTtpe))
                {
                    propertyInfo.SetValue(target, value);
                }
                else if (propertyInfo.PropertyType.FullName == valueTtpe.FullName)
                {
                    propertyInfo.SetValue(target, value);
                }
                else
                {
                    throw new Exception($"类型 {targetType} 对象成员\"{memberName}\" 赋值时异常");
                }*/
            }
        }

        /// <summary>
        /// 从对象获取值
        /// </summary>
        /// <param name="node">节点，用于缓存委托避免重复反射</param>
        /// <param name="target">对象</param>
        /// <param name="memberName">成员名称</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<object?> GetPropertyValueAsync(ASTNode node, object target, string memberName)
        {
            if (ASTDelegateDetails.TryGetValue(node, out DelegateDetails? delegateDetails))
            {
                var result = await delegateDetails.InvokeAsync(target, []);
                return result;
            }

            var targetType = target?.GetType();
            if (targetType is null) return null;
            var propertyInfo = targetType.GetProperty(memberName);
            if (propertyInfo is null)
            {
                var fieldInfo = target?.GetType().GetRuntimeField(memberName);
                if (fieldInfo is null)
                {
                    throw new Exception($"类型 {targetType} 对象没有成员\"{memberName}\"");
                }
                else
                {
                    delegateDetails = new DelegateDetails(propertyInfo, DelegateDetails.GSType.Get);
                    ASTDelegateDetails[node] = delegateDetails; // 缓存委托
                    var result = await delegateDetails.InvokeAsync(target, []);
                    return result;

                    //return fieldInfo.GetValue(target);
                }
            }
            else
            {
                delegateDetails = new DelegateDetails(propertyInfo, DelegateDetails.GSType.Get);
                ASTDelegateDetails[node] = delegateDetails; // 缓存委托
                var result = await delegateDetails.InvokeAsync(target, []);
                return result;
                //return propertyInfo.GetValue(target);
            }
        }


        /// <summary>
        /// 设置集合成员
        /// </summary>
        /// <param name="node">节点，用于缓存委托避免重复反射</param>
        /// <param name="collectionValue"></param>
        /// <param name="indexValue"></param>
        /// <param name="valueValue"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task SetCollectionValueAsync(ASTNode node, object collectionValue, object indexValue, object valueValue)
        {
            if (ASTDelegateDetails.TryGetValue(node, out DelegateDetails? delegateDetails))
            {
                await delegateDetails.InvokeAsync(collectionValue, [indexValue, valueValue]);
                return;
            }
            else
            {
                var collectionType = collectionValue.GetType(); // 目标对象的类型
                delegateDetails = new DelegateDetails(collectionType,  DelegateDetails.EmitType.CollectionSetter);
                ASTDelegateDetails[node] = delegateDetails; // 缓存委托
                await delegateDetails.InvokeAsync(collectionValue, [indexValue, valueValue]);
                return;
            }

#if false

            // 解析数组/集合名与索引部分
            var targetType = collectionValue.GetType(); // 目标对象的类型
            #region 处理键值对
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                // 目标是键值对
                var methodInfo = targetType.GetMethod("set_Item", BindingFlags.Public | BindingFlags.Instance);
                if (methodInfo is not null)
                {

                    methodInfo.Invoke(collectionValue, [indexValue, valueValue]);
                }
            }
            #endregion
            #region 处理集合对象
            else
            {
                if (indexValue is int index)
                {
                    // 获取数组或集合对象
                    // 访问数组或集合中的指定索引
                    if (collectionValue is Array array)
                    {
                        if (index < 0 || index >= array.Length)
                        {
                            throw new ArgumentException($"解析{collectionValue}节点时，数组下标越界。");
                        }
                        array.SetValue(valueValue, index);
                        return;
                    }
                    else if (collectionValue is IList<object> list)
                    {
                        if (index < 0 || index >= list.Count)
                        {
                            throw new ArgumentException($"解析{collectionValue}节点时，数组下标越界。");
                        }
                        list[index] = valueValue;
                        return;
                    }
                }
            }
            #endregion
            throw new ArgumentException($"解析异常， {collectionValue} 并非有效集合。"); 
#endif
        }

        /// <summary>
        /// 获取集合中的成员
        /// </summary>
        /// <param name="node">节点，用于缓存委托避免重复反射</param>
        /// <param name="collectionValue"></param>
        /// <param name="indexValue"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<object?> GetCollectionValueAsync(ASTNode node, object collectionValue, object indexValue)
        {
            if (ASTDelegateDetails.TryGetValue(node, out DelegateDetails? delegateDetails))
            {
                var result = await delegateDetails.InvokeAsync(collectionValue, [indexValue]);
                return result;
            }
            else
            {
                // 针对 string 特化
                if(collectionValue is string chars && indexValue is int index)
                {
                    return chars[index];
                }
                var collectionType = collectionValue.GetType(); // 目标对象的类型
                delegateDetails = new DelegateDetails(collectionType, DelegateDetails.EmitType.CollectionGetter);
                ASTDelegateDetails[node] = delegateDetails; // 缓存委托
                var result =  await delegateDetails.InvokeAsync(collectionValue, [indexValue]);
                return result;
            }

#if false
            // 解析数组/集合名与索引部分
            var targetType = collectionValue.GetType(); // 目标对象的类型
            #region 处理键值对
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                // 目标是键值对
                var method = targetType.GetMethod("get_Item", BindingFlags.Public | BindingFlags.Instance);
                if (method is not null)
                {
                    var value = method.Invoke(collectionValue, [indexValue]);
                    return value;
                }
            }
            #endregion
            #region 处理集合对象
            else
            {
                if (indexValue is int index)
                {
                    // 获取数组或集合对象
                    // 访问数组或集合中的指定索引
                    if (collectionValue is Array array)
                    {
                        if (index < 0 || index >= array.Length)
                        {
                            throw new ArgumentException($"解析{collectionValue}节点时，数组下标越界。");
                        }

                        return array.GetValue(index);
                    }
                    else if (collectionValue is IList<object> list)
                    {
                        if (index < 0 || index >= list.Count)
                        {
                            throw new ArgumentException($"解析{collectionValue}节点时，数组下标越界。");
                        }
                        return list[index];
                    }
                }
            }
            #endregion

            throw new ArgumentException($"解析{collectionValue}节点时，左值并非有效集合。"); 
#endif
        }


    }
}
