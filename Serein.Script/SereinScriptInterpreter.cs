using Newtonsoft.Json.Linq;
using Serein.Library;
using Serein.Library.Utils;
using Serein.Script.Node;
using Serein.Script.Node.FlowControl;
using System.ComponentModel.Design;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Serein.Script
{

    /// <summary>
    /// 脚本解释器，负责解析和执行 Serein 脚本
    /// </summary>
    public class SereinScriptInterpreter
    {
        private readonly Dictionary<ASTNode, Type> symbolInfos;

        public SereinScriptInterpreter(Dictionary<ASTNode, Type> symbolInfos)
        {
            this.symbolInfos = symbolInfos;
        }

 


        /// <summary>
        /// 入口节点
        /// </summary>
        /// <param name="programNode"></param>
        /// <returns></returns>
        private async Task<object?> ExecutionProgramNodeAsync(IScriptInvokeContext context,ProgramNode programNode) 
        {
            // 加载变量
            ASTNode statement = null;
            try
            {
                // 遍历 ProgramNode 中的所有语句并执行它们
                for (int index = 0; index < programNode.Statements.Count; index++)
                {
                    statement = programNode.Statements[index];
                    // 直接退出
                    if (statement is ReturnNode returnNode) // 遇到 Return 语句 提前退出
                    {
                        return await EvaluateAsync(context, statement);
                    }
                    else
                    {
                        var result = await InterpretAsync(context, statement);
                        if (context.IsNeedReturn)
                        {
                            return result;
                        }
                    }
                }
                return null;
            }
            catch (Exception ex )
            {
                if(statement is not null)
                {
                    SereinEnv.WriteLine(InfoType.ERROR, $"脚本异常发生在[行{statement.Row}]：{ex.Message}{Environment.NewLine}\t{statement.Code}");
                }
                throw;
            }
        }

        /// <summary>
        /// 类型定义
        /// </summary>
        /// <param name="programNode"></param>
        /// <returns></returns>
        private void ExecutionClassTypeDefinitionNode(ClassTypeDefinitionNode classTypeDefinitionNode)
        {
            var className = classTypeDefinitionNode.ClassType.TypeName;
            if (SereinScript.MountType.ContainsKey(className) && !classTypeDefinitionNode.IsOverlay)
            {
                //SereinEnv.WriteLine(InfoType.WARN, $"异常信息 : 类型重复定义，代码在第{classTypeDefinitionNode.Row}行: {classTypeDefinitionNode.Code.Trim()}");
                return;
            }
            if(DynamicObjectHelper.GetCacheType(className) == null)
            {
                var propertyTypes = classTypeDefinitionNode.Propertys.ToDictionary(p => p.Key, p => symbolInfos[p.Value]);
                var type = DynamicObjectHelper.CreateTypeWithProperties(propertyTypes, className); // 覆盖
                SereinScript.MountType[className] = type; // 定义对象
            }
            
        }

        /// <summary>
        /// IF...ELSE... 语句块
        /// </summary>
        /// <param name="ifNode"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task<object?> ExecutionIfNodeAsync(IScriptInvokeContext context, IfNode ifNode)
        {
            var result = await EvaluateAsync(context, ifNode.Condition) ?? throw new SereinSciptException(ifNode, $"条件语句返回了 null");

            if (result is not bool condition)
            {
                throw new SereinSciptException(ifNode, "条件语句返回值不为 bool 类型");
            }

            var branchNodes = condition ? ifNode.TrueBranch : ifNode.FalseBranch;
            if(branchNodes is null || branchNodes.Count < 1)
            {
                return null;
            }
            else
            {
                
                foreach (var branchNode in branchNodes)
                {
                    if (branchNode is ReturnNode) // 遇到 Return 语句 提前退出
                    {
                        var reulst = await EvaluateAsync(context, branchNode);
                        context.IsNeedReturn = true;
                        return reulst;
                    }
                    else
                    {
                        await InterpretAsync(context, branchNode);
                    }
                }
                return null;
            }
            
        }

        /// <summary>
        /// WHILE(){...} 语句块
        /// </summary>
        /// <param name="whileNode"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task<object?> ExectutionWhileNodeAsync(IScriptInvokeContext context, WhileNode whileNode)
        {

            while (true)
            {
                if (context.IsReturn) // 停止流程
                {
                    throw new SereinSciptException(whileNode, $"while循环已由外部主动停止");
                }
                var result = await EvaluateAsync(context, whileNode.Condition) ?? throw new SereinSciptException(whileNode, $"条件语句返回了 null");
                if (result is not bool condition)
                {
                    throw new SereinSciptException(whileNode, $"条件语句返回值不为 bool 类型（当前返回值类型为 {result.GetType()})");
                }
                if (!condition)
                {
                    break;
                }
                foreach(var branchNode in whileNode.Body)
                {
                    if (branchNode is ReturnNode) // 遇到 Return 语句 提前退出
                    {
                        var reulst = await EvaluateAsync(context, branchNode);
                        context.IsNeedReturn = true;
                        return reulst;
                    }
                    else
                    {
                        await InterpretAsync(context, branchNode);
                    }
                    //await InterpretAsync(context, node);
                }
            }
            return null;
        }

        /// <summary>
        /// 操作节点
        /// </summary>
        /// <param name="assignmentNode"></param>
        /// <returns></returns>
        private async Task ExecutionAssignmentNodeAsync(IScriptInvokeContext context, AssignmentNode assignmentNode)
        {
            if(assignmentNode.Target is IdentifierNode identifierNode)
            {
                var value = await EvaluateAsync(context, assignmentNode.Value);
                if (value is not null)
                {
                    context.SetVarValue(identifierNode.Name, value);
                }
            }
            else
            {

            }
            /*var targetObject = await EvaluateAsync(context, assignmentNode.TargetNode);
            var value = await EvaluateAsync(context, assignmentNode.Value);
            if(value is not null)
            {
                context.SetVarValue(targetObject, value);
            }*/
        }

        

        private async Task<object> InterpretFunctionCallAsync(IScriptInvokeContext context, FunctionCallNode functionCallNode)
        {
            if (functionCallNode.FunctionName.Equals("GetFlowContext", StringComparison.OrdinalIgnoreCase))
            {
                return context.FlowContext;
            }

            // 评估函数参数
            var arguments = new object?[functionCallNode.Arguments.Count];
            for (int i = 0; i < functionCallNode.Arguments.Count; i++)
            {
                ASTNode? arg = functionCallNode.Arguments[i];
                arguments[i] = await EvaluateAsync(context, arg);  // 评估每个参数
            }

            var funcName = functionCallNode.FunctionName;

            object? instance = null; // 静态方法不需要传入实例，所以可以传入null 


            // 查找并执行对应的函数
            if (SereinScript.FunctionDelegates .TryGetValue(funcName, out DelegateDetails? function))
            {
                if (!function.EmitMethodInfo.IsStatic)
                {
                    if(SereinScript.DelegateInstances.TryGetValue(funcName, out var action))
                    {
                        instance = action.Invoke();// 非静态的方法需要获取相应的实例

                        if (instance is null)
                        {
                            throw new SereinSciptException(functionCallNode, $"函数 {funcName} 尝试获取实例时返回了 null ");
                        }
                    }
                    else
                    {
                        throw new SereinSciptException(functionCallNode, $"挂载函数 {funcName} 时需要同时给定获取实例的 Func<object>");
                    }
                }
                
                var result = await function.InvokeAsync(instance,arguments);
                return result;
            }
            else
            {
                throw new Exception($"Unknown function: {functionCallNode.FunctionName}");
            }
        }


        /// <summary>
        /// 解释操作
        /// </summary>
        /// <param name="context"></param>
        /// <param name="node"></param>
        /// <returns></returns>
        /// <exception cref="SereinSciptException"></exception>
        public async Task<object?> InterpretAsync(IScriptInvokeContext context, ASTNode node)
        {
            if(node == null)
            {
                return null;
            }
            
            switch (node)
            {
                case ProgramNode programNode: // AST树入口

                    var scritResult = await ExecutionProgramNodeAsync(context, programNode); 
                    return scritResult; // 遍历 ProgramNode 中的所有语句并执行它们
                case ClassTypeDefinitionNode classTypeDefinitionNode: // 定义类型
                    ExecutionClassTypeDefinitionNode(classTypeDefinitionNode);
                    break;
                case AssignmentNode assignment: // 出现在 = 右侧的表达式
                    await ExecutionAssignmentNodeAsync(context, assignment);
                    break;
                case CollectionAssignmentNode collectionAssignmentNode:
                    await SetCollectionValue(context,collectionAssignmentNode);
                    break;
                case ExpressionNode objectMemberExpressionNode:
                    break;
                case MemberAssignmentNode memberAssignmentNode: // 设置对象属性
                    await SetMemberValue(context, memberAssignmentNode);
                    break;
                case MemberFunctionCallNode memberFunctionCallNode: // 对象方法调用
                    return await CallMemberFunction(context, memberFunctionCallNode);
                case IfNode ifNode: // 执行 if...else... 语句块
                    return await ExecutionIfNodeAsync(context, ifNode); 
                    break;
                case WhileNode whileNode: // 循环语句块
                    return await ExectutionWhileNodeAsync(context, whileNode);
                    break;
                case FunctionCallNode functionCallNode: // 方法调用节点
                    return await InterpretFunctionCallAsync(context, functionCallNode);
                case ReturnNode returnNode:
                    return await EvaluateAsync(context, returnNode);
                default:
                    throw new SereinSciptException(node, "解释器 InterpretAsync() 未实现节点行为");
            }
            return null;
        }

        /// <summary>
        /// 评估
        /// </summary>
        /// <param name="context"></param>
        /// <param name="node"></param>
        /// <returns></returns>
        /// <exception cref="SereinSciptException"></exception>
        private async Task<object?> EvaluateAsync(IScriptInvokeContext context, ASTNode node)
        {
            if(node == null)
            {
                return null;
            }
            switch (node)
            {
                case NullNode nullNode:
                    return null;
                case BooleanNode booleanNode:
                    return booleanNode.Value; // 返回数值
                case NumberIntNode numberNode:
                    return numberNode.Value; // 返回 int 整型数
                case NumberLongNode numberNode:
                    return numberNode.Value; // 返回 long 整型数
                case NumberFloatNode numberNode:
                    return numberNode.Value; // 返回 float 浮点型
                case NumberDoubleNode numberNode:
                    return numberNode.Value; // 返回 double 浮点型
                case StringNode stringNode:
                    return stringNode.Value; // 返回字符串值
                case CharNode charNode:
                    return charNode.Value; // 返回Char
                case IdentifierNode identifierNode:
                    return context.GetVarValue(identifierNode.Name);
                    //throw new SereinSciptException(identifierNode, "尝试使用值为null的变量");
                    //throw new SereinSciptException(identifierNode, "尝试使用未声明的变量");
                case BinaryOperationNode binOpNode:
                    // 递归计算二元操作
                    var left = await EvaluateAsync(context, binOpNode.Left);
                    //if (left == null ) throw new SereinSciptException(binOpNode.Left, $"左值尝试使用 null");
                    var right = await EvaluateAsync(context, binOpNode.Right);
                    //if (right == null) throw new SereinSciptException(binOpNode.Right, "右值尝试使用计算 null");
                    return EvaluateBinaryOperation(left, binOpNode.Operator, right);
                case ObjectInstantiationNode objectInstantiationNode:  // 对象实例化
                    if (!SereinScript.MountType.TryGetValue(objectInstantiationNode.Type.TypeName, out var type))
                    {
                        type = symbolInfos[objectInstantiationNode.Type];
                        if (type is null)
                        {
                            throw new SereinSciptException(objectInstantiationNode, $"使用了未定义的类型\"{objectInstantiationNode.Type.TypeName}\"");
                        }
                    }
                    object?[] args = new object[objectInstantiationNode.Arguments.Count];
                    for (int i = 0; i < objectInstantiationNode.Arguments.Count; i++)
                    {
                        var argNode = objectInstantiationNode.Arguments[i];
                        args[i] = await EvaluateAsync(context, argNode);
                    }
                    var obj = Activator.CreateInstance(type, args: args);// 创建对象
                    if (obj == null)
                    {
                        throw new SereinSciptException(objectInstantiationNode, $"类型创建失败\"{objectInstantiationNode.Type.TypeName}\"");
                    }
                    for (int i = 0; i < objectInstantiationNode.CtorAssignments.Count; i++)
                    {
                        var ctorAssignmentNode = objectInstantiationNode.CtorAssignments[i];
                        var propertyName = ctorAssignmentNode.MemberName;
                        var value = await EvaluateAsync(context, ctorAssignmentNode.Value);
                        SetPropertyValue(obj, propertyName, value);
                    }

                    return obj;
                case FunctionCallNode callNode: // 调用方法
                    return await InterpretFunctionCallAsync(context, callNode); // 调用方法返回函数的返回值
                case MemberFunctionCallNode memberFunctionCallNode: // 对象方法调用
                    return await CallMemberFunction(context, memberFunctionCallNode);
                case MemberAccessNode memberAccessNode: // 对象成员访问
                    return await GetMemberValue(context, memberAccessNode);
                case CollectionIndexNode collectionIndexNode:
                    return await GetCollectionValue(context, collectionIndexNode);
                case ReturnNode returnNode: // 返回内容
                    return await EvaluateAsync(context, returnNode.Value); // 直接返回响应的内容
                case ExpressionNode expressionNode: // 表达式
                    return await EvaluateAsync(context, expressionNode.Value);
                default:
                    throw new SereinSciptException(node, $"解释器 EvaluateAsync() 未实现{node}节点行为");
            }
        }

        private object EvaluateBinaryOperation(object left, string op, object right)
        {
            return BinaryOperationEvaluator.EvaluateValue(left, op, right);

            // 根据运算符执行不同的运算
            switch (op)
            {
                case "+":
                    if (left is string || right is string)
                    {
                        return left?.ToString() + right?.ToString();  // 字符串拼接
                    }
                    else if (left is int leftInt && right is int rightInt)
                    {
                        return leftInt + rightInt;  // 整数加法
                    }
                    else if (left is long leftLong && right is long rightLong)
                    {
                        return leftLong + rightLong;  // 整数加法
                    }
                    else if (left is double leftDouble && right is double rightDouble)
                    {
                        return leftDouble + rightDouble;  // 整数加法
                    }
                    else
                    {
                        dynamic leftValue = Convert.ToDouble(left);
                        dynamic rightValue = Convert.ToDouble(right);
                        return leftValue + rightValue;
                    }
                    throw new Exception("Invalid types for + operator");
                case "-":
                    return (int)left - (int)right;
                case "*":
                    return (int)left * (int)right;
                case "/":
                    return (int)left / (int)right;

                case ">":
                    return (int)left > (int)right;
                case "<":
                    return (int)left < (int)right;
                case "==":
                    return Equals(left, right);
                case "!=":
                    return !Equals(left, right);

                default:
                    throw new NotImplementedException("未定义的操作符: " + op);
            }
        }


        /// <summary>
        /// 设置对象成员
        /// </summary>
        /// <param name="memberAssignmentNode"></param>
        /// <returns></returns>
        /// <exception cref="SereinSciptException"></exception>
        public async Task SetMemberValue(IScriptInvokeContext context, MemberAssignmentNode memberAssignmentNode)
        {
            var target = await EvaluateAsync(context, memberAssignmentNode.Object);
            var value = await EvaluateAsync(context, memberAssignmentNode.Value);
            // 设置值
            var lastMember = memberAssignmentNode.MemberName;

            var lastProperty = target?.GetType().GetProperty(lastMember);
            if (lastProperty is null)
            {
                var lastField = target?.GetType().GetRuntimeField(lastMember);
                if (lastField is null)
                {
                    throw new SereinSciptException(memberAssignmentNode, $"对象没有成员\"{memberAssignmentNode.MemberName}\"");
                }
                else
                {
                    var convertedValue = Convert.ChangeType(value, lastField.FieldType);
                    lastField.SetValue(target, convertedValue);
                }
            }
            else
            {
                if(value is null)
                {
                    lastProperty.SetValue(target, null);
                    return;
                }
                var valueTtpe = value.GetType();
                if (lastProperty.PropertyType.IsAssignableFrom(valueTtpe))
                {
                    lastProperty.SetValue(target, value);
                }
                else if (lastProperty.PropertyType.FullName == valueTtpe.FullName)
                {
                    lastProperty.SetValue(target, value);
                }
                else {
                    throw new SereinSciptException(memberAssignmentNode, $"对象成员赋值时类型异常：\"{memberAssignmentNode.MemberName}\"");
                }
               //var convertedValue = Convert.ChangeType(value, );
            }
        }

        public void SetPropertyValue(object target, string memberName, object? value)
        {
            var targetType = target?.GetType();
            if (targetType is null) return;
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
                    var convertedValue = Convert.ChangeType(value, fieldInfo.FieldType);
                    fieldInfo.SetValue(target, convertedValue);
                }
            }
            else
            {
                if (value is null)
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
                }
                //var convertedValue = Convert.ChangeType(value, );
            }
        }

        /// <summary>
        /// 获取对象成员
        /// </summary>
        /// <param name="memberAccessNode"></param>
        /// <returns></returns>
        /// <exception cref="SereinSciptException"></exception>
        public async Task<object?> GetMemberValue(IScriptInvokeContext context, MemberAccessNode memberAccessNode)
        {
            var target = await EvaluateAsync(context, memberAccessNode.Object);
            var lastMember = memberAccessNode.MemberName;

            var lastProperty = target?.GetType().GetProperty(lastMember);
            if (lastProperty is null)
            {
                var lastField = target?.GetType().GetRuntimeField(lastMember);
                if (lastField is null)
                {
                    throw new SereinSciptException(memberAccessNode, $"对象没有成员\"{memberAccessNode.MemberName}\"");
                }
                else
                {
                    return lastField.GetValue(target);
                }
            }
            else
            {
                return lastProperty.GetValue(target);
            }
        }
        
        /// <summary>
        /// 获取集合中的成员
        /// </summary>
        /// <param name="memberAccessNode"></param>
        /// <returns></returns>
        /// <exception cref="SereinSciptException"></exception>
        public async Task<object?> GetCollectionValue(IScriptInvokeContext context, CollectionIndexNode collectionIndexNode)
        {
            var target = await EvaluateAsync(context, collectionIndexNode.Collection); // 获取对象
            if (target is null)
            {
                throw new ArgumentNullException($"解析{collectionIndexNode}节点时，TargetValue返回空。");
            }
            
            // 解析数组/集合名与索引部分
            var targetType = target.GetType(); // 目标对象的类型
            #region 处理键值对
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                // 目标是键值对
                var method = targetType.GetMethod("get_Item", BindingFlags.Public | BindingFlags.Instance);
                if (method is not null)
                {
                    var key = await EvaluateAsync(context, collectionIndexNode.Index); // 获取索引值;
                    var result = method.Invoke(target, new object[] { key });
                    return result;
                }
            }
            #endregion
            #region 处理集合对象
            else
            {
                var indexValue = await EvaluateAsync(context, collectionIndexNode.Index); // 获取索引值
                object? result;
                if (indexValue is int index)
                {
                    // 获取数组或集合对象
                    // 访问数组或集合中的指定索引
                    if (target is Array array)
                    {
                        if (index < 0 || index >= array.Length)
                        {
                            throw new ArgumentException($"解析{collectionIndexNode}节点时，数组下标越界。");
                        }
                        result = array.GetValue(index);
                        return result;
                    }
                    else if (target is IList<object> list)
                    {
                        if (index < 0 || index >= list.Count)
                        {
                            throw new ArgumentException($"解析{collectionIndexNode}节点时，数组下标越界。");
                        }
                        result = list[index];
                        return result;
                    }
                    else if (target is string chars)
                    {
                        return chars[index];
                    }
                    else
                    {
                        throw new ArgumentException($"解析{collectionIndexNode}节点时，左值并非有效集合。");
                    }
                }
            }
            #endregion

            throw new ArgumentException($"解析{collectionIndexNode}节点时，左值并非有效集合。");

        }
        
        /// <summary>
        /// 设置集合中的成员
        /// </summary>
        /// <param name="memberAccessNode"></param>
        /// <returns></returns>
        /// <exception cref="SereinSciptException"></exception>
        public async Task SetCollectionValue(IScriptInvokeContext context, CollectionAssignmentNode collectionAssignmentNode)
        {
            var collectionValue = await EvaluateAsync(context, collectionAssignmentNode.Collection.Collection);
            var indexValue = await EvaluateAsync(context, collectionAssignmentNode.Collection.Index);

            if (collectionValue is null)
            {
                throw new ArgumentNullException($"解析{collectionAssignmentNode}节点时，集合返回空。");
            } 
            if (indexValue is null)
            {
                throw new ArgumentNullException($"解析{collectionAssignmentNode}节点时，索引返回空。");
            }
            
            // 解析数组/集合名与索引部分
            var targetType = collectionValue.GetType(); // 目标对象的类型
            #region 处理键值对
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                // 目标是键值对
                var method = targetType.GetMethod("set_Item", BindingFlags.Public | BindingFlags.Instance);
                if (method is not null)
                {
                    var valueValue = await EvaluateAsync(context, collectionAssignmentNode.Value);
                    method.Invoke(collectionValue, [indexValue, valueValue]);
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
                        var valueValue = await EvaluateAsync(context, collectionAssignmentNode.Value);
                        array.SetValue(valueValue, index);
                        return;
                    }
                    else if (collectionValue is IList<object> list)
                    {
                        if (index < 0 || index >= list.Count)
                        {
                            throw new ArgumentException($"解析{collectionValue}节点时，数组下标越界。");
                        }
                        var valueValue = await EvaluateAsync(context, collectionAssignmentNode.Value);
                        list[index] = valueValue;
                        return;
                    }
                    else
                    {
                        throw new ArgumentException($"解析{collectionValue}节点时，左值并非有效集合。");
                    }
                }
            }
            #endregion

            throw new ArgumentException($"解析{collectionValue}节点时，左值并非有效集合。");

        }

        /// <summary>
        /// 缓存method委托
        /// </summary>
        private Dictionary<long, DelegateDetails> MethodToDelegateCaches { get; } = new Dictionary<long, DelegateDetails>();

        public async Task<object?> CallMemberFunction(IScriptInvokeContext context, MemberFunctionCallNode memberFunctionCallNode)
        {
            var target = await EvaluateAsync(context, memberFunctionCallNode.Object);
            var lastMember = memberFunctionCallNode.FunctionName;
            if(lastMember == "ToUpper")
            {

            }
            MethodInfo? methodInfo = null;
            if (memberFunctionCallNode.Arguments.Count == 0)
            {
                
                // 查询无参方法
                methodInfo = target?.GetType().GetMethod(lastMember, []) ?? throw new SereinSciptException(memberFunctionCallNode, $"对象没有方法\"{memberFunctionCallNode.FunctionName}\"");
            }
            else
            {
                // 获取参数列表的类型
                methodInfo = target?.GetType().GetMethod(lastMember) ?? throw new SereinSciptException(memberFunctionCallNode, $"对象没有方法\"{memberFunctionCallNode.FunctionName}\"");

            }

            /*Type[] paramTypes = new
            {

            }*/

            if (!MethodToDelegateCaches.TryGetValue(methodInfo.MetadataToken, out DelegateDetails? delegateDetails))
            {
                delegateDetails = new DelegateDetails(methodInfo);
                MethodToDelegateCaches[methodInfo.MetadataToken] = delegateDetails;
            }

            if(memberFunctionCallNode.Arguments.Count == 0)
            {
                var reuslt = await delegateDetails.InvokeAsync(target, []);
                return reuslt;
            }
            else
            {
                var arguments = new object?[memberFunctionCallNode.Arguments.Count];
                for (int i = 0; i < memberFunctionCallNode.Arguments.Count; i++)
                {
                    ASTNode? arg = memberFunctionCallNode.Arguments[i];
                    arguments[i] = await EvaluateAsync(context, arg);  // 评估每个参数
                }
                var reuslt = await delegateDetails.InvokeAsync(target, arguments);
                return reuslt;
            }

        }


    }
}
