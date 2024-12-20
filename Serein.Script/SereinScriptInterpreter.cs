using Newtonsoft.Json.Linq;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.Script.Node;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Serein.Script
{
    public sealed class SereinSciptException : Exception
    {
        //public ASTNode Node { get; }
        public override string Message { get; }

        public SereinSciptException(ASTNode node,    string message)
        {
            //this.Node = node;
            Message = $"异常信息 : {message} ，代码在第{node.Row}行: {node.Code.Trim()}";
        }
    }


    public class SereinScriptInterpreter
    {
        /// <summary>
        /// 定义的变量
        /// </summary>
        private Dictionary<string, object> _variables = new Dictionary<string, object>();

        /// <summary>
        /// 挂载的函数
        /// </summary>
        private static Dictionary<string, DelegateDetails> _functionTable = new Dictionary<string, DelegateDetails>();

        /// <summary>
        /// 挂载的函数调用的对象（用于函数需要实例才能调用的场景）
        /// </summary>
        private static Dictionary<string, Func<object>> _callFuncOfGetObjects = new Dictionary<string, Func<object>>();

        /// <summary>
        /// 定义的类型
        /// </summary>
        private static Dictionary<string, Type> _classDefinition = new Dictionary<string, Type>();

        /// <summary>
        /// 重置的变量
        /// </summary>
        public void ResetVar()
        {
            foreach (var nodeObj in _variables.Values)
            {
                if (nodeObj is not null)
                {
                    if (typeof(IDisposable).IsAssignableFrom(nodeObj?.GetType()) && nodeObj is IDisposable disposable)
                    {
                        disposable?.Dispose();
                    }
                }
                else
                {

                }
            }
            _variables.Clear();
        }

        /// <summary>
        /// 挂载函数
        /// </summary>
        /// <param name="functionName">函数名称</param>
        /// <param name="methodInfo">方法信息</param>
        public static void AddFunction(string functionName, MethodInfo methodInfo, Func<object>? callObj = null)
        {
            //if (!_functionTable.ContainsKey(functionName))
            //{
            //    _functionTable[functionName] = new DelegateDetails(methodInfo);
            //}
            if(!methodInfo.IsStatic && callObj is null)
            {
                SereinEnv.WriteLine(InfoType.WARN, "函数挂载失败：试图挂载非静态的函数，但没有传入相应的获取实例的方法。");
                return;
            }

            
            if(!methodInfo.IsStatic && callObj is not null)
            { 
                // 静态函数不需要
                _callFuncOfGetObjects.Add(functionName, callObj);
            }
            _functionTable[functionName] = new DelegateDetails(methodInfo);
        }

        /// <summary>
        /// 挂载类型
        /// </summary>
        /// <param name="typeName">函数名称</param>
        /// <param name="type">方法信息</param>
        public static void AddClassType(Type type , string typeName = "")
        {
            if (string.IsNullOrEmpty(typeName))
            {
                typeName = type.Name;
            }
            if (!_classDefinition.ContainsKey(typeName))
            {
                _classDefinition[typeName] = type;
            }
        }


        /// <summary>
        /// 入口节点
        /// </summary>
        /// <param name="programNode"></param>
        /// <returns></returns>
        private async Task<object?> ExecutionProgramNodeAsync(ProgramNode programNode) 
        {
            // 遍历 ProgramNode 中的所有语句并执行它们
            foreach (var statement in programNode.Statements)
            {
                // 直接退出
                if (statement is ReturnNode returnNode) // 遇到 Return 语句 提前退出
                {
                    return await EvaluateAsync(statement);
                }
                else
                {
                    await InterpretAsync(statement);
                }
            }

            return null;
        }

        /// <summary>
        /// 类型定义
        /// </summary>
        /// <param name="programNode"></param>
        /// <returns></returns>
        private void ExecutionClassTypeDefinitionNode(ClassTypeDefinitionNode classTypeDefinitionNode)
        {
            if (_classDefinition.ContainsKey(classTypeDefinitionNode.ClassName))
            {
                //SereinEnv.WriteLine(InfoType.WARN, $"异常信息 : 类型重复定义，代码在第{classTypeDefinitionNode.Row}行: {classTypeDefinitionNode.Code.Trim()}");
                return;
            }
            var type = DynamicObjectHelper.CreateTypeWithProperties(classTypeDefinitionNode.Fields, classTypeDefinitionNode.ClassName);
            _classDefinition[classTypeDefinitionNode.ClassName] = type; // 定义对象
        }

        /// <summary>
        /// IF...ELSE... 语句块
        /// </summary>
        /// <param name="ifNode"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task ExecutionIfNodeAsync(IfNode ifNode)
        {
            var result = await EvaluateAsync(ifNode.Condition) ?? throw new SereinSciptException(ifNode, $"条件语句返回了 null");

            if (result is not bool condition)
            {
                throw new SereinSciptException(ifNode, "条件语句返回值不为 bool 类型");
            }

            if (condition)
            {
                foreach (var trueNode in ifNode.TrueBranch)
                {
                    await InterpretAsync(trueNode);
                }
            }
            else
            {
                foreach (var falseNode in ifNode.FalseBranch)
                {
                    await InterpretAsync(falseNode);
                }
            }
        }

        /// <summary>
        /// WHILE(){...} 语句块
        /// </summary>
        /// <param name="whileNode"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task ExectutionWhileNodeAsync(WhileNode whileNode)
        {
            while (true)
            {
                var result = await EvaluateAsync(whileNode.Condition) ?? throw new SereinSciptException(whileNode, $"条件语句返回了 null");
                if (result is not bool condition)
                {
                    throw new SereinSciptException(whileNode, $"条件语句返回值不为 bool 类型（当前返回值类型为 {result.GetType()})");
                }
                if (!condition)
                {
                    break;
                }
                foreach(var node in whileNode.Body)
                {
                    await InterpretAsync(node);
                }
            }
        }

        /// <summary>
        /// 操作节点
        /// </summary>
        /// <param name="assignmentNode"></param>
        /// <returns></returns>
        private async Task ExecutionAssignmentNodeAsync(AssignmentNode assignmentNode)
        {
            var tmp = await EvaluateAsync(assignmentNode.Value);
            _variables[assignmentNode.Variable] = tmp;
        }
        private async Task<object> InterpretFunctionCallAsync(FunctionCallNode functionCallNode)
        {
            // 评估函数参数
            var arguments = new object?[functionCallNode.Arguments.Count];
            for (int i = 0; i < functionCallNode.Arguments.Count; i++)
            {
                ASTNode? arg = functionCallNode.Arguments[i];
                arguments[i] = await EvaluateAsync(arg);  // 评估每个参数
            }

            var funcName = functionCallNode.FunctionName;

            object? instance = null; // 静态方法不需要传入实例，所以可以传入null 

            // 查找并执行对应的函数
            if (_functionTable.TryGetValue(funcName, out DelegateDetails? function))
            {
                if (!function.EmitMethodInfo.IsStatic)
                {
                    if(_callFuncOfGetObjects.TryGetValue(funcName, out var action))
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




        public async Task<object?> InterpretAsync(ASTNode node)
        {
            if(node == null)
            {
                return null;
            }
            
            switch (node)
            {
                case ProgramNode programNode: // AST树入口
                    var scritResult = await ExecutionProgramNodeAsync(programNode); 
                    return scritResult; // 遍历 ProgramNode 中的所有语句并执行它们
                case ClassTypeDefinitionNode classTypeDefinitionNode: // 定义类型
                    ExecutionClassTypeDefinitionNode(classTypeDefinitionNode);
                    break;
                case AssignmentNode assignment: // 出现在 = 右侧的表达式
                    await ExecutionAssignmentNodeAsync(assignment);
                    break;
                case MemberAssignmentNode memberAssignmentNode: // 设置对象属性
                    await SetMemberValue(memberAssignmentNode);
                    break;
                case MemberFunctionCallNode memberFunctionCallNode:
                    return await CallMemberFunction(memberFunctionCallNode);
                    break;
                case IfNode ifNode: // 执行 if...else... 语句块
                    await ExecutionIfNodeAsync(ifNode);
                    break;
                case WhileNode whileNode: // 循环语句块
                    await ExectutionWhileNodeAsync(whileNode);
                    break;
                case FunctionCallNode functionCallNode: // 方法调用节点
                    return await InterpretFunctionCallAsync(functionCallNode);
                case ReturnNode returnNode:
                    return await EvaluateAsync(returnNode);
                default:
                    throw new SereinSciptException(node, "解释器 InterpretAsync() 未实现节点行为");
            }
            return null;
        }

        
        private async Task<object?> EvaluateAsync(ASTNode node)
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
                case NumberNode numberNode:
                    return numberNode.Value; // 返回数值
                case StringNode stringNode:
                    return stringNode.Value; // 返回字符串值
                case IdentifierNode identifierNode:
                    if (_variables.TryGetValue(identifierNode.Name, out var result))
                    {
                        //if(result == null)
                        //{
                        //    throw new SereinSciptException(identifierNode, "尝试使用值为null的变量");
                        //}
                        return result; // 获取变量值
                    }
                    else
                    {
                        throw new SereinSciptException(identifierNode, "尝试使用未声明的变量");
                    }
                case BinaryOperationNode binOpNode:
                    // 递归计算二元操作
                    var left = await EvaluateAsync(binOpNode.Left);
                    //if (left == null )
                    //{
                    //    throw new SereinSciptException(binOpNode.Left, $"左值尝试使用 null");
                    //}

                    var right = await EvaluateAsync(binOpNode.Right);
                    //if (right == null)
                    //{
                    //    throw new SereinSciptException(binOpNode.Right, "右值尝试使用计算 null");
                    //}
                    return EvaluateBinaryOperation(left, binOpNode.Operator, right);
                case ObjectInstantiationNode objectInstantiationNode:
                    if (_classDefinition.TryGetValue(objectInstantiationNode.TypeName,out var type ))
                    {
                        object?[] args = new object[objectInstantiationNode.Arguments.Count];
                        for (int i = 0; i < objectInstantiationNode.Arguments.Count; i++)
                        {
                            var argNode = objectInstantiationNode.Arguments[i];
                            args[i] = await EvaluateAsync(argNode);
                        }
                        var obj = Activator.CreateInstance(type,args: args);// 创建对象
                        if (obj == null)
                        {
                            throw new SereinSciptException(objectInstantiationNode, $"类型创建失败\"{objectInstantiationNode.TypeName}\"");
                        }
                        return obj;
                    }
                    else
                    {
                        
                        throw new SereinSciptException(objectInstantiationNode, $"使用了未定义的类型\"{objectInstantiationNode.TypeName}\"");

                    }
                case FunctionCallNode callNode:
                    return await InterpretFunctionCallAsync(callNode); // 调用方法返回函数的返回值
                case MemberAccessNode memberAccessNode:
                    return await GetValue(memberAccessNode);
                case ReturnNode returnNode: // 
                    return await EvaluateAsync(returnNode.Value); // 直接返回响应的内容
                default:
                    throw new SereinSciptException(node, "解释器 EvaluateAsync() 未实现节点行为");
            }
        }

        private object EvaluateBinaryOperation(object left, string op, object right)
        {
          


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
        public async Task SetMemberValue(MemberAssignmentNode memberAssignmentNode)
        {
            var target = await EvaluateAsync(memberAssignmentNode.Object);
            var value = await EvaluateAsync(memberAssignmentNode.Value);
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
               var convertedValue = Convert.ChangeType(value, lastProperty.PropertyType);
               lastProperty.SetValue(target, convertedValue);
            }
        }

        /// <summary>
        /// 获取对象成员
        /// </summary>
        /// <param name="memberAccessNode"></param>
        /// <returns></returns>
        /// <exception cref="SereinSciptException"></exception>
        public async Task<object?> GetValue(MemberAccessNode memberAccessNode)
        {
            var target = await EvaluateAsync(memberAccessNode.Object);
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
        /// 缓存method委托
        /// </summary>
        private Dictionary<string, DelegateDetails> MethodToDelegateCaches { get; } = new Dictionary<string, DelegateDetails>();

        public async Task<object?> CallMemberFunction(MemberFunctionCallNode memberFunctionCallNode)
        {
            var target = await EvaluateAsync(memberFunctionCallNode.Object);
            var lastMember = memberFunctionCallNode.FunctionName;

            var methodInfo = target?.GetType().GetMethod(lastMember) ?? throw new SereinSciptException(memberFunctionCallNode, $"对象没有方法\"{memberFunctionCallNode.FunctionName}\"");
            if(!MethodToDelegateCaches.TryGetValue(methodInfo.Name, out DelegateDetails? delegateDetails))
            {
                delegateDetails = new DelegateDetails(methodInfo);
                MethodToDelegateCaches[methodInfo.Name] = delegateDetails;
            }



            var arguments = new object?[memberFunctionCallNode.Arguments.Count];
            for (int i = 0; i < memberFunctionCallNode.Arguments.Count; i++)
            {
                ASTNode? arg = memberFunctionCallNode.Arguments[i];
                arguments[i] = await EvaluateAsync(arg);  // 评估每个参数
            }

            return await delegateDetails.InvokeAsync(target, arguments);
        }


    }
}
