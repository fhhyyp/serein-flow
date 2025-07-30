using Serein.Library.Api;
using Serein.Script.Node;
using Serein.Script.Node.FlowControl;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Serein.Script
{

    /*
        暂未想到如何编译出具备 await / async 功能的IL代码，暂时放弃
     */


    /// <summary>
    /// IL 代码生成结果
    /// </summary>
    internal record ILResult
    {
        /// <summary>
        /// 返回类型
        /// </summary>
        public Type? ReturnType { get; set; }

        /// <summary>
        /// 临时变量，可用于缓存值，避免重复计算
        /// </summary>
        public LocalBuilder? TempVar { get; set; } // 可用于缓存

        /// <summary>
        /// 发射 IL 代码的委托，接受 ILGenerator 参数
        /// </summary>
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public Action<ILGenerator> Emit { get; set; } // 用于推入值的代码
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    }


    /// <summary>
    /// 脚本编译IL代码
    /// </summary>
    internal class SereinScriptILCompiler
    {
        /// <summary>
        /// 符号表
        /// </summary>
        private readonly Dictionary<ASTNode, Type> symbolInfos;

        /// <summary>
        /// 发射IL
        /// </summary>
        //private ILGenerator _il;

        /// <summary>
        /// 创建的对应方法
        /// </summary>
        private DynamicMethod _method;

        /// <summary>
        /// 临时变量绑定
        /// </summary>
        private readonly Dictionary<string, LocalBuilder> _locals = new();

        /// <summary>
        /// 节点对应的委托缓存，避免重复编译同一节点
        /// </summary>
        private readonly Dictionary<ASTNode, Delegate> _nodeCache = new();

        /// <summary>
        /// IL映射
        /// </summary>
        Dictionary<ASTNode, ILResult> _ilResults = new Dictionary<ASTNode, ILResult>();


        private Label _methodExit;

        public SereinScriptILCompiler(Dictionary<ASTNode, Type> symbolInfos)
        {
            this.symbolInfos = symbolInfos;
        }

        /// <summary>
        /// 是否调用了异步方法
        /// </summary>
        //private bool _isUseAwait = false;

        private Dictionary<string, int> _parameterIndexes = new Dictionary<string, int>();

        public Delegate Compiler(string compilerMethodName, ProgramNode programNode, Dictionary<string, Type>? argTypes = null)
        {
            argTypes ??= new Dictionary<string, Type>();

            var parameterTypes = argTypes.Values.ToArray();
            var parameterNames = argTypes.Keys.ToArray();

            // 构建参数索引映射，方便 EmitNode 访问参数
            _parameterIndexes.Clear();
            for (int i = 0; i < parameterNames.Length; i++)
            {
                _parameterIndexes[parameterNames[i]] = i;
            }

            _method = new DynamicMethod(
                compilerMethodName,
                typeof(object),
                parameterTypes,
                typeof(SereinScriptILCompiler).Module,
                skipVisibility: true);

            var il = _method.GetILGenerator();

            _ilResults.Clear();

            var resultType = symbolInfos[programNode];

            _methodExit = il.DefineLabel();

            foreach (var node in programNode.Statements)
            {
                EmitNode(il, node);
            }

            il.MarkLabel(_methodExit);

            if (resultType == typeof(void))
            {
                il.Emit(OpCodes.Ldnull);
            }
            else if (resultType.IsValueType)
            {
                il.Emit(OpCodes.Box, resultType);
            }

            il.Emit(OpCodes.Ret);

            Type delegateType;
            if (parameterTypes.Length == 0)
            {
                delegateType = typeof(Func<object>);
            }
            else
            {
                var funcGenericTypeArgs = parameterTypes.Concat(new[] { typeof(object) }).ToArray();
                delegateType = Expression.GetFuncType(funcGenericTypeArgs);
            }

            var @delegate = _method.CreateDelegate(delegateType);
            return @delegate;
        }

        /// <summary>
        /// 用于将 GenerateIL() 返回的 ILResult 缓存到 _ilResults 中，避免在 GenerateIL() 中混乱生成逻辑
        /// </summary>
        /// <param name="il"></param>
        /// <param name="node"></param>
        /// <returns></returns>
        private ILResult EmitNode(ILGenerator il, ASTNode node)
        {
            ILResult result = GenerateIL(il, node);
            _ilResults[node] = result;
            return result;
        }

        private ILResult GenerateIL(ILGenerator il, ASTNode node)
        {
            switch (node)
            {
                case ProgramNode programNode: // 程序开始节点，不用分析IL
                    return new ILResult
                    {
                        ReturnType = symbolInfos[node],
                        Emit = il => { }
                    };
                case ReturnNode returnNode: // 程序退出节点
                    ILResult GenerateReturnNodeIL(ReturnNode returnNode)
                    {
                        if (returnNode.Value is null) // 没有返回值
                        {
                            return new ILResult
                            {
                                ReturnType = typeof(void),
                                Emit = il =>
                                {
                                    il.Emit(OpCodes.Ldnull); // 返回 null
                                    il.Emit(OpCodes.Br, _methodExit); // 跳转到程序退出
                                }
                            };
                        }
                        else // 有返回值
                        {
                            var valueResult = EmitNode(il, returnNode.Value);
                            return new ILResult
                            {
                                ReturnType = valueResult.ReturnType,
                                Emit = il =>
                                {
                                    valueResult.Emit(il); // 推入返回值
                                    il.Emit(OpCodes.Br, _methodExit); // 跳转到程序退出
                                }
                            };
                        }
                    }
                    return GenerateReturnNodeIL(returnNode);
                #region 字面量IL生成
                case NullNode nullNode: // null
                    return new ILResult
                    {
                        ReturnType = symbolInfos[node],
                        Emit = il => il.Emit(OpCodes.Ldnull)
                    };
                case CharNode charNode: // char字面量 加载 char（本质为 ushort / int）
                    return new ILResult
                    {
                        ReturnType = symbolInfos[node],
                        Emit = il =>
                        {
                            il.Emit(OpCodes.Ldc_I4, (int)charNode.Value);
                            il.Emit(OpCodes.Box, typeof(char));
                        }
                    };
                case StringNode stringNode: // 字符串字面量
                    return new ILResult
                    {
                        ReturnType = symbolInfos[node],
                        Emit = il => il.Emit(OpCodes.Ldstr, stringNode.Value)
                    };
                case BooleanNode booleanNode: // 布尔值字面量
                    return new ILResult
                    {
                        ReturnType = symbolInfos[node],
                        Emit = il =>
                        {
                            il.Emit(booleanNode.Value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                            il.Emit(OpCodes.Box, typeof(bool));
                        }
                    };
                case NumberIntNode numberIntNode: // int整型数值字面量
                    return new ILResult
                    {
                        ReturnType = symbolInfos[node],
                        Emit = il =>
                        {
                            il.Emit(OpCodes.Ldc_I4, numberIntNode.Value);
                            il.Emit(OpCodes.Box, typeof(int));
                        }
                    };
                case NumberLongNode numberLongNode: // long整型数值字面量
                    return new ILResult
                    {
                        ReturnType = symbolInfos[node],
                        Emit = il =>
                        {
                            il.Emit(OpCodes.Ldc_I8, numberLongNode.Value);
                            il.Emit(OpCodes.Box, typeof(long));
                        }
                    };
                case NumberFloatNode numberFloatNode: // float浮点数值字面量
                    return new ILResult
                    {
                        ReturnType = symbolInfos[node],
                        Emit = il =>
                        {
                            il.Emit(OpCodes.Ldc_R4, numberFloatNode.Value);
                            il.Emit(OpCodes.Box, typeof(float));
                        }
                    };
                case NumberDoubleNode numberDoubleNode: // double浮点数值字面量
                    return new ILResult
                    {
                        ReturnType = symbolInfos[node],
                        Emit = il =>
                        {
                            il.Emit(OpCodes.Ldc_R8, numberDoubleNode.Value);
                            il.Emit(OpCodes.Box, typeof(double));
                        }
                    };

                #endregion
                case IdentifierNode identifierNode:
                    ILResult GenerateIdentifierNodeIL(IdentifierNode identifierNode)
                    {
                        if (_parameterIndexes.TryGetValue(identifierNode.Name, out int paramIndex))
                        {
                            // 变量是方法参数，直接加载参数
                            return new ILResult
                            {
                                ReturnType = symbolInfos[identifierNode],
                                Emit = il =>
                                {
                                    switch (paramIndex)
                                    {
                                        case 0: il.Emit(OpCodes.Ldarg_0); break;
                                        case 1: il.Emit(OpCodes.Ldarg_1); break;
                                        case 2: il.Emit(OpCodes.Ldarg_2); break;
                                        case 3: il.Emit(OpCodes.Ldarg_3); break;
                                        default: il.Emit(OpCodes.Ldarg, paramIndex); break;
                                    }
                                }
                            };
                        }
                        else
                        {
                            // 变量是本地变量，声明并访问
                            if (!_locals.TryGetValue(identifierNode.Name, out var local))
                            {
                                var varType = symbolInfos.TryGetValue(identifierNode, out var t) ? t : typeof(object);
                                local = il.DeclareLocal(varType);
                                _locals[identifierNode.Name] = local;
                            }

                            return new ILResult
                            {
                                ReturnType = symbolInfos[identifierNode],
                                Emit = il =>
                                {
                                    il.Emit(OpCodes.Ldloc, local);
                                }
                            };
                        }
                    }
                    return GenerateIdentifierNodeIL(identifierNode);
                case IfNode ifNode: // if语句结构
                    ILResult GenerateIfNodeIL(IfNode ifNode)
                    {
                        var elseLabel = il.DefineLabel();
                        var endLabel = il.DefineLabel();
                        // 计算条件表达式
                        var conditionResult = EmitNode(il, ifNode.Condition);
                        conditionResult.Emit(il); // 条件结果入栈
                        // 弹出object装箱，转bool
                        il.Emit(OpCodes.Unbox_Any, typeof(bool));
                        // 条件为 false，跳转 else
                        il.Emit(OpCodes.Brfalse, elseLabel);
                        // 执行 TrueBranch
                        foreach (var stmt in ifNode.TrueBranch)
                        {
                            var stmtResult = EmitNode(il, stmt);
                            stmtResult.Emit(il);
                            if (stmt is ReturnNode) // 如果是返回语句，直接跳到结束标签
                            {
                                il.Emit(OpCodes.Br, endLabel);
                            }
                        }
                        // 跳转到 if 结束
                        il.Emit(OpCodes.Br, endLabel);
                        // 处理 else 分支
                        il.MarkLabel(elseLabel);
                        foreach (var stmt in ifNode.FalseBranch)
                        {
                            var stmtResult = EmitNode(il, stmt);
                            stmtResult.Emit(il);
                            if (stmt is ReturnNode) // 如果是返回语句，直接跳到结束标签
                            {
                                il.Emit(OpCodes.Br, endLabel);
                            }
                        }
                        // 结束标签
                        il.MarkLabel(endLabel);
                        return new ILResult
                        {
                            ReturnType = symbolInfos[node],
                            Emit = il => { } // 无需额外操作，已在各分支处理
                        };
                    }  
                    return GenerateIfNodeIL(ifNode);
                case WhileNode whileNode: // while语句结构
                    ILResult GenerateWhileNode(WhileNode whileNode)
                    {
                        // 帮我实现处理逻辑：
                        var startLabel = il.DefineLabel(); // 循环开始标签
                        var endLabel = il.DefineLabel(); // 循环结束标签
                        il.MarkLabel(startLabel); // 标记循环开始
                        // 计算条件表达式
                        var conditionResult = EmitNode(il, whileNode.Condition);
                        conditionResult.Emit(il); // 条件结果入栈
                        // 弹出object装箱，转bool
                        il.Emit(OpCodes.Unbox_Any, typeof(bool));
                        // 条件为 false，跳转到循环结束
                        il.Emit(OpCodes.Brfalse, endLabel);
                        foreach (var stmt in whileNode.Body)
                        {
                            var stmtResult = EmitNode(il, stmt);
                            stmtResult.Emit(il);
                            if (stmt is ReturnNode) // 如果是返回语句，直接跳到结束标签
                            {
                                il.Emit(OpCodes.Br, endLabel);
                            }
                        }
                        il.Emit(OpCodes.Br, startLabel);
                        il.MarkLabel(endLabel);
                        return new ILResult
                        {
                            ReturnType = symbolInfos[node],
                            Emit = il => { } // 无需额外操作，已在各分支处理
                        };

                    }
                    return GenerateWhileNode(whileNode);
                case AssignmentNode assignmentNode: // 赋值语句
                    ILResult GenerateAssignmentNodeIL(AssignmentNode assignmentNode)
                    {
                        if (assignmentNode.Target is not IdentifierNode identifierNode)
                            throw new Exception("AssignmentNode.Target 必须是 IdentifierNode");

                        if (!_locals.TryGetValue(identifierNode.Name, out var local))
                        {
                            var varType = symbolInfos.TryGetValue(identifierNode, out var t) ? t : typeof(object);
                            local = il.DeclareLocal(varType);
                            _locals[identifierNode.Name] = local;
                        }

                        var valueResult = EmitNode(il, assignmentNode.Value);

                        return new ILResult
                        {
                            ReturnType = valueResult.ReturnType,
                            Emit = il =>
                            {
                                valueResult.Emit(il); // 先把赋值值推入栈顶

                                var targetType = local.LocalType;
                                var sourceType = valueResult.ReturnType;
                                if (targetType != sourceType)
                                {
                                    EmitConvert(il, sourceType, targetType); // 尝试类型转换
                                }
                                 
                                il.Emit(OpCodes.Stloc, local);  // 然后保存到局部变量
                            }
                        };
                    }
                    return GenerateAssignmentNodeIL(assignmentNode); 
                case BinaryOperationNode binaryOperationNode: // 二元运算操作
                    ILResult GenerateBinaryOperationNodeIL(BinaryOperationNode binaryOperationNode)
                    {
                        void EmitShortCircuit(ILGenerator il, string op, ILResult left, ILResult right)
                        {
                            var labelEnd = il.DefineLabel();
                            var labelFalse = il.DefineLabel();
                            var labelTrue = il.DefineLabel();

                            if (op == "&&")
                            {
                                left.Emit(il);
                                il.Emit(OpCodes.Brfalse, labelFalse);
                                right.Emit(il);
                                il.Emit(OpCodes.Br, labelEnd);

                                il.MarkLabel(labelFalse);
                                il.Emit(OpCodes.Ldc_I4_0);

                                il.MarkLabel(labelEnd);
                            }
                            else if (op == "||")
                            {
                                left.Emit(il);
                                il.Emit(OpCodes.Brtrue, labelTrue);
                                right.Emit(il);
                                il.Emit(OpCodes.Br, labelEnd);

                                il.MarkLabel(labelTrue);
                                il.Emit(OpCodes.Ldc_I4_1);

                                il.MarkLabel(labelEnd);
                            }
                        }

                        var left = EmitNode(il, binaryOperationNode.Left);
                        var right = EmitNode(il, binaryOperationNode.Right);

                        return new ILResult
                        {
                            ReturnType = symbolInfos[node],
                            Emit = il =>
                            {
                                switch (binaryOperationNode.Operator)
                                {
                                    case "&&":
                                    case "||":
                                        EmitShortCircuit(il, binaryOperationNode.Operator, left, right);
                                        break;

                                    default:
                                        left.Emit(il);
                                        right.Emit(il);
                                        switch (binaryOperationNode.Operator)
                                        {
                                            case "+":
                                                il.Emit(OpCodes.Add);
                                                break;
                                            case "-":
                                                il.Emit(OpCodes.Sub);
                                                break;
                                            case "*":
                                                il.Emit(OpCodes.Mul);
                                                break;
                                            case "/":
                                                il.Emit(OpCodes.Div);
                                                break;
                                            case "==":
                                                il.Emit(OpCodes.Ceq);
                                                break;
                                            case "!=":
                                                il.Emit(OpCodes.Ceq);
                                                il.Emit(OpCodes.Ldc_I4_0);
                                                il.Emit(OpCodes.Ceq); // 取反
                                                break;
                                            case ">":
                                                il.Emit(OpCodes.Cgt);
                                                break;
                                            case "<":
                                                il.Emit(OpCodes.Clt);
                                                break;
                                            case ">=":
                                                il.Emit(OpCodes.Clt);
                                                il.Emit(OpCodes.Ldc_I4_0);
                                                il.Emit(OpCodes.Ceq); // !(a < b)
                                                break;
                                            case "<=":
                                                il.Emit(OpCodes.Cgt);
                                                il.Emit(OpCodes.Ldc_I4_0);
                                                il.Emit(OpCodes.Ceq); // !(a > b)
                                                break;
                                            default:
                                                throw new NotSupportedException($"未知的操作符: {binaryOperationNode.Operator}");
                                        }
                                        break;
                                }
                            }
                        };
                    }
                    return GenerateBinaryOperationNodeIL(binaryOperationNode);
                case CollectionAssignmentNode collectionAssignmentNode: // 集合赋值
                    ILResult GenerateCollectionAssignmentNodeIL(CollectionAssignmentNode collectionAssignmentNode)
                    {
                        // 加载集合、索引和值
                        var collectionIL = EmitNode(il, collectionAssignmentNode.Collection.Collection);
                        var indexIL = EmitNode(il, collectionAssignmentNode.Collection.Index);
                        var valueIL = EmitNode(il, collectionAssignmentNode.Value);
                        return new ILResult
                        {
                            ReturnType = typeof(void),
                            Emit = il =>
                            {
                                collectionIL.Emit(il); // 加载集合
                                indexIL.Emit(il);      // 加载索引
                                valueIL.Emit(il);      // 加载值
                                var collectionType = collectionIL.ReturnType;
                                var valueType = valueIL.ReturnType;
                                if (collectionType.IsArray && collectionType.GetArrayRank() == 1)
                                {
                                    var elementType = collectionType.GetElementType();

                                    // 如果值类型不匹配，尝试强转（可能需要扩展）
                                    if (elementType != valueType)
                                    {
                                        EmitConvert(il, valueType, elementType);
                                    }

                                    // 发射 Stelem 指令
                                    EmitStoreElement(il, elementType);
                                }
                                else
                                {
                                    // 对象集合，调用 set_Item 方法
                                    var indexerSetter = collectionType
                                        .GetMethods()
                                        .FirstOrDefault(m => m.Name == "set_Item" && m.GetParameters().Length == 2);

                                    if (indexerSetter == null)
                                        throw new InvalidOperationException($"集合类型 {collectionType} 不支持索引赋值");

                                    if (collectionType.IsValueType)
                                    {
                                        il.Emit(OpCodes.Constrained, collectionType);
                                    }

                                    il.EmitCall(OpCodes.Callvirt, indexerSetter, null);
                                }
                            }
                        };
                    }
                    return GenerateCollectionAssignmentNodeIL(collectionAssignmentNode);
                case CollectionIndexNode collectionIndexNode: // 集合取值
                    ILResult GenerateCollectionIndexNodeIL(CollectionIndexNode collectionIndexNode)
                    {
                        var collectionIL = EmitNode(il, collectionIndexNode.Collection);
                        var indexIL = EmitNode(il, collectionIndexNode.Index);
                        var resultType = symbolInfos[collectionIndexNode];

                        return new ILResult
                        {
                            ReturnType = resultType,
                            Emit = il =>
                            {
                                collectionIL.Emit(il);
                                indexIL.Emit(il);

                                var collectionType = collectionIL.ReturnType;

                                if (collectionType.IsArray)
                                {
                                    var elementType = collectionType.GetElementType();
                                    EmitLoadElement(il, elementType);
                                }
                                else
                                {
                                    // 尝试获取索引器 get_Item 方法（匹配参数类型）
                                    var indexerMethod = collectionType.GetMethod("get_Item", new[] { indexIL.ReturnType });
                                    if (indexerMethod == null)
                                    {
                                        // 退而求其次：只要名字匹配且参数个数相等的第一个
                                        indexerMethod = collectionType.GetMethods()
                                            .FirstOrDefault(m => m.Name == "get_Item" && m.GetParameters().Length == 1);
                                        if (indexerMethod == null)
                                            throw new InvalidOperationException($"集合类型 {collectionType} 没有索引器方法");
                                    }

                                    if (collectionType.IsValueType)
                                    {
                                        il.Emit(OpCodes.Constrained, collectionType);
                                    }
                                    il.EmitCall(OpCodes.Callvirt, indexerMethod, null);
                                }
                            }
                        };
                    }

                    return GenerateCollectionIndexNodeIL(collectionIndexNode);
                case ClassTypeDefinitionNode classTypeDefinitionNode: // 类型定义，在 Parser. 阶段已经处理，不生成 IL
                    return new ILResult
                    {
                        ReturnType = symbolInfos[node],
                        Emit = il => { }
                    };
                case TypeNode typeNode:// 指示类型的 FullName 由 ClassTypeDefinitionNode 实际使用，不生成 IL
                    return new ILResult
                    {
                        ReturnType = symbolInfos[node],
                        Emit = il => { }
                    };
                case ObjectInstantiationNode objectInstantiationNode: // 类型实例化
                    ILResult GenerateObjectInstantiationNodeIL(ObjectInstantiationNode objectInstantiationNode)
                    {
                        var type = symbolInfos[objectInstantiationNode.Type] ?? throw new Exception($"未找到类型 {objectInstantiationNode.Type.TypeName}");

                        var ctorArgTypes = objectInstantiationNode.Arguments
                            .Select(arg => symbolInfos[arg] ?? typeof(object))
                            .ToArray();

                        var ctor = type.GetConstructor(ctorArgTypes)
                                   ?? throw new Exception($"类型 {type} 找不到匹配的构造函数");

                        ILResult[] argIlResult = objectInstantiationNode.Arguments.Select(arg => EmitNode(il, arg)) .ToArray();

                        return new ILResult
                        {
                            ReturnType = type,
                            Emit = il =>
                            {
                                // 构造函数参数压栈
                                foreach (var argIL in argIlResult)
                                {
                                    argIL.Emit(il);
                                }

                                il.Emit(OpCodes.Newobj, ctor); // 调用构造函数，新对象入栈

                                if (objectInstantiationNode.CtorAssignments.Count == 0)
                                    return;

                                // 需要成员赋值，存入局部变量
                                var local = il.DeclareLocal(type);
                                il.Emit(OpCodes.Stloc, local); // 将对象保存到局部变量
                                il.Emit(OpCodes.Ldloc, local); // 再加载对象，保证栈顶仍是实例

                                // 遍历成员赋值
                                foreach (var assignmentNode in objectInstantiationNode.CtorAssignments)
                                {
                                    il.Emit(OpCodes.Ldloc, local); // 载入对象引用

                                    var valueIL = EmitNode(il, assignmentNode.Value);
                                    valueIL.Emit(il); // 载入赋值的值

                                    // 先查属性
                                    var prop = type.GetProperty(assignmentNode.MemberName, BindingFlags.Public | BindingFlags.Instance);
                                    if (prop != null && prop.CanWrite)
                                    {
                                        var setMethod = prop.GetSetMethod();
                                        il.EmitCall(OpCodes.Callvirt, setMethod, null);
                                    }
                                    else
                                    {
                                        // 查字段
                                        var field = type.GetField(assignmentNode.MemberName, BindingFlags.Public | BindingFlags.Instance);
                                        if (field != null)
                                        {
                                            il.Emit(OpCodes.Stfld, field);
                                        }
                                        else
                                        {
                                            throw new Exception($"类型 {type} 没有成员 {assignmentNode.MemberName}");
                                        }
                                    }
                                }

                                // 最终栈顶是实例对象，供后续调用
                                il.Emit(OpCodes.Ldloc, local);
                            }
                        };
                    }
                    return GenerateObjectInstantiationNodeIL(objectInstantiationNode);
                case CtorAssignmentNode ctorAssignmentNode: // 构造器赋值
                    ILResult GenerateCtorAssignmentNodeIL(CtorAssignmentNode ctorAssignmentNode)
                    {
                        var classType = symbolInfos[ctorAssignmentNode.Class] ?? throw new Exception($"未找到类型 {ctorAssignmentNode.Class.TypeName}");

                        return new ILResult
                        {
                            ReturnType = typeof(void),
                            Emit = il =>
                            {
                                // 栈顶已有对象引用，先发射赋值值
                                var valueIL = EmitNode(il, ctorAssignmentNode.Value);
                                valueIL.Emit(il);

                                // 查找成员
                                var prop = classType.GetProperty(ctorAssignmentNode.MemberName, BindingFlags.Public | BindingFlags.Instance);
                                var field = prop == null ? classType.GetField(ctorAssignmentNode.MemberName, BindingFlags.Public | BindingFlags.Instance) : null;

                                Type memberType = null;
                                if (prop != null && prop.CanWrite)
                                {
                                    memberType = prop.PropertyType;
                                }
                                else if (field != null)
                                {
                                    memberType = field.FieldType;
                                }
                                else
                                {
                                    throw new Exception($"类型 {classType} 没有成员 {ctorAssignmentNode.MemberName}");
                                }

                                // 类型转换
                                if (memberType != null && valueIL.ReturnType != memberType)
                                {
                                    EmitConvert(il, valueIL.ReturnType, memberType);
                                }

                                // 赋值
                                if (prop != null && prop.CanWrite)
                                {
                                    var setMethod = prop.GetSetMethod();
                                    il.EmitCall(OpCodes.Callvirt, setMethod, null);
                                }
                                else if (field != null)
                                {
                                    il.Emit(OpCodes.Stfld, field);
                                }
                            }
                        };
                    }
                    return GenerateCtorAssignmentNodeIL(ctorAssignmentNode);
                case MemberAccessNode memberAccessNode: // 对象成员访问
                    ILResult GenerateMemberAccessNodeIL(MemberAccessNode memberAccessNode)
                    {
                        var targetIL = EmitNode(il, memberAccessNode.Object);

                        var targetType = targetIL.ReturnType;
                        var propInfo = targetType.GetProperty(memberAccessNode.MemberName,
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new InvalidOperationException($"属性 {memberAccessNode.MemberName} 不存在");

                        if (propInfo.GetMethod is null)
                            throw new InvalidOperationException($"属性 {memberAccessNode.MemberName} 无法获取 MethodInfo");

                        return new ILResult
                        {
                            ReturnType = propInfo.PropertyType,
                            Emit = il =>
                            {
                                targetIL.Emit(il);
                                if (targetType.IsValueType)
                                {
                                    il.Emit(OpCodes.Constrained, targetType);
                                    il.EmitCall(OpCodes.Callvirt, propInfo.GetMethod, null);
                                }
                                else
                                {
                                    il.EmitCall(OpCodes.Callvirt, propInfo.GetMethod, null);
                                }
                            }
                        };
                    }

                    return GenerateMemberAccessNodeIL(memberAccessNode);
                case MemberAssignmentNode memberAssignmentNode:
                    ILResult GenerateMemberAssignmentNodeIL(MemberAssignmentNode memberAssignmentNode)
                    {
                        var objectResult = EmitNode(il, memberAssignmentNode.Object);
                        var classType = objectResult.ReturnType;

                        return new ILResult
                        {
                            ReturnType = typeof(void),
                            Emit = il =>
                            {
                                objectResult.Emit(il); // 先入栈对象实例
                                var valueResult = EmitNode(il, memberAssignmentNode.Value);
                                valueResult.Emit(il);  // 再入栈赋值值

                                var prop = classType.GetProperty(memberAssignmentNode.MemberName, BindingFlags.Public | BindingFlags.Instance);
                                var field = prop == null ? classType.GetField(memberAssignmentNode.MemberName, BindingFlags.Public | BindingFlags.Instance) : null;

                                Type memberType = prop != null ? prop.PropertyType : field?.FieldType;
                                if (memberType != null && valueResult.ReturnType != memberType)
                                {
                                    EmitConvert(il, valueResult.ReturnType, memberType);
                                }

                                if (prop != null && prop.CanWrite)
                                {
                                    if (classType.IsValueType)
                                    {
                                        il.Emit(OpCodes.Constrained, classType);
                                    }
                                    var setMethod = prop.GetSetMethod();
                                    il.EmitCall(OpCodes.Callvirt, setMethod, null);
                                }
                                else if (field != null)
                                {
                                    il.Emit(OpCodes.Stfld, field);
                                }
                                else
                                {
                                    throw new Exception($"类型 {classType} 没有成员 {memberAssignmentNode.MemberName}");
                                }
                            }
                        };
                    }

                    return GenerateMemberAssignmentNodeIL(memberAssignmentNode);
                case MemberFunctionCallNode memberFunctionCallNode: // 对象方法调用
                    ILResult GenerateMemberFunctionCallNodeIL(MemberFunctionCallNode memberFunctionCallNode)
                    {
                        var targetIL = EmitNode(il, memberFunctionCallNode.Object);

                        var argTypes = memberFunctionCallNode.Arguments
                            .Select(arg => symbolInfos[arg] ?? typeof(object))
                            .ToArray();

                        var method = targetIL.ReturnType.GetMethod(memberFunctionCallNode.FunctionName, argTypes)
                            ?? throw new InvalidOperationException($"方法 {memberFunctionCallNode.FunctionName} 找不到匹配的重载");

                        var argResults = memberFunctionCallNode.Arguments.Select(arg => EmitNode(il, arg)).ToList();

                        return new ILResult
                        {
                            ReturnType = method.ReturnType,
                            Emit = il =>
                            {
                                targetIL.Emit(il);
                                for (int i = 0; i < argResults.Count; i++)
                                {
                                    argResults[i].Emit(il);
                                    var paramType = method.GetParameters()[i].ParameterType;
                                    var argType = argResults[i].ReturnType;
                                    if (paramType != argType)
                                    {
                                        EmitConvert(il, argType, paramType);
                                    }
                                }

                                if (targetIL.ReturnType.IsValueType)
                                {
                                    il.Emit(OpCodes.Constrained, targetIL.ReturnType);
                                    il.EmitCall(OpCodes.Callvirt, method, null);
                                }
                                else
                                {
                                    il.EmitCall(OpCodes.Callvirt, method, null);
                                }
                            }
                        };
                    }

                    return GenerateMemberFunctionCallNodeIL(memberFunctionCallNode);
                case FunctionCallNode functionCallNode: // 外部挂载的函数调用
                    ILResult GenerateFunctionCallNode(FunctionCallNode functionCallNode)
                    {
                        if (!SereinScript.FunctionInfos.TryGetValue(functionCallNode.FunctionName, out var methodInfo))
                            throw new InvalidOperationException($"没有挂载 {functionCallNode.FunctionName} 方法信息");

                        var argResults = functionCallNode.Arguments.Select(arg => EmitNode(il, arg)).ToList();

                        return new ILResult
                        {
                            ReturnType = methodInfo.ReturnType,
                            Emit = il =>
                            {
                                // 挂载函数为静态方法，不推入实例

                                for (int i = 0; i < argResults.Count; i++)
                                {
                                    argResults[i].Emit(il);
                                    var paramType = methodInfo.GetParameters()[i].ParameterType;
                                    var argType = argResults[i].ReturnType;
                                    if (paramType != argType)
                                    {
                                        EmitConvert(il, argType, paramType);
                                    }
                                }

                                if (methodInfo.IsStatic)
                                    il.EmitCall(OpCodes.Call, methodInfo, null);
                                else
                                    il.EmitCall(OpCodes.Callvirt, methodInfo, null);
                            }
                        };
                    }

                    return GenerateFunctionCallNode(functionCallNode);
                default: // 未定义的节点类型
                    break;
            }
            throw new InvalidOperationException($"");
        }

        /// <summary>
        /// 元素读取指令
        /// </summary>
        /// <param name="il"></param>
        /// <param name="elementType"></param>
        void EmitLoadElement(ILGenerator il, Type elementType)
        {
            if (elementType.IsValueType)
            {
                if (elementType == typeof(int))
                    il.Emit(OpCodes.Ldelem_I4);
                else if (elementType == typeof(float))
                    il.Emit(OpCodes.Ldelem_R4);
                else if (elementType == typeof(double))
                    il.Emit(OpCodes.Ldelem_R8);
                else if (elementType == typeof(bool))
                    il.Emit(OpCodes.Ldelem_I1);
                else if (elementType == typeof(char))
                    il.Emit(OpCodes.Ldelem_U2);
                else
                    il.Emit(OpCodes.Ldelem, elementType); // 对于其它值类型，通用形式
            }
            else
            {
                // 引用类型
                il.Emit(OpCodes.Ldelem_Ref);
            }
        }

        /// <summary>
        /// 元素发射指令
        /// </summary>
        /// <param name="il"></param>
        /// <param name="elementType"></param>
        void EmitStoreElement(ILGenerator il, Type elementType)
        {
            if (!elementType.IsValueType)
            {
                il.Emit(OpCodes.Stelem_Ref);
            }
            else if (elementType == typeof(int))
            {
                il.Emit(OpCodes.Stelem_I4);
            }
            else if (elementType == typeof(long))
            {
                il.Emit(OpCodes.Stelem_I8);
            }
            else if (elementType == typeof(float))
            {
                il.Emit(OpCodes.Stelem_R4);
            }
            else if (elementType == typeof(double))
            {
                il.Emit(OpCodes.Stelem_R8);
            }
            else if (elementType == typeof(short))
            {
                il.Emit(OpCodes.Stelem_I2);
            }
            else if (elementType == typeof(byte))
            {
                il.Emit(OpCodes.Stelem_I1);
            }
            else
            {
                // 泛型结构体类型
                il.Emit(OpCodes.Stelem, elementType);
            }
        }

        /// <summary>
        /// 类型转换指令
        /// </summary>
        /// <param name="il"></param>
        /// <param name="fromType"></param>
        /// <param name="toType"></param>
        void EmitConvert(ILGenerator il, Type fromType, Type toType)
        {
            if (toType.IsAssignableFrom(fromType))
                return;

            if (toType.IsValueType)
                il.Emit(OpCodes.Unbox_Any, toType);
            else
                il.Emit(OpCodes.Castclass, toType);
        }


        /*private void EmitIfNode(IScriptInvokeContext context, ILGenerator il, IfNode ifNode)
        {
            // Labels
            var elseLabel = il.DefineLabel();
            var endLabel = il.DefineLabel();
            var loopBreakLabel = il.DefineLabel();

            // 1. 计算 condition，栈顶为 object
            EmitNode(il, ifNode.Condition);

            // 弹出object装箱，转bool
            il.Emit(OpCodes.Unbox_Any, typeof(bool));

            // 条件为 false，跳转 else
            il.Emit(OpCodes.Brfalse, elseLabel);

            // 2. TrueBranch执行
            foreach (var stmt in ifNode.TrueBranch)
            {
                EmitNode(il, stmt);

                if (stmt is ReturnNode)
                {
                    // 设置 context.IsNeedReturn = true
                    il.Emit(OpCodes.Ldarg_0); // context 参数
                    il.Emit(OpCodes.Ldc_I4_1);
                    var isNeedReturnSetter = typeof(IScriptInvokeContext).GetProperty("IsNeedReturn").GetSetMethod();
                    il.Emit(OpCodes.Callvirt, isNeedReturnSetter);

                    // 跳出循环（直接跳到endLabel）
                    il.Emit(OpCodes.Br, endLabel);
                }
            }

            // 跳转到 if 结束
            il.Emit(OpCodes.Br, endLabel);

            // 3. else branch
            il.MarkLabel(elseLabel);
            foreach (var stmt in ifNode.FalseBranch)
            {
                EmitNode( il, stmt);

                if (stmt is ReturnNode)
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldc_I4_1);
                    var setter = typeof(IScriptInvokeContext).GetProperty("IsNeedReturn").GetSetMethod();
                    il.Emit(OpCodes.Callvirt, setter);
                    il.Emit(OpCodes.Br, endLabel);
                }
            }

            // 4. 结束标签
            il.MarkLabel(endLabel);

            // 如果方法签名需要返回值，这里可放置默认 return 语句或用局部变量存储返回值
        }*/

        private static bool IsGenericTask(Type returnType,[NotNullWhen(true)] out Type? taskResult)
        {
            // 判断是否为 Task 类型或泛型 Task<T>
            if (returnType == typeof(Task))
            {
                taskResult = typeof(void);
                return true;
            }
            else if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                // 获取泛型参数类型
                Type genericArgument = returnType.GetGenericArguments()[0];
                taskResult = genericArgument;
                return true;
            }
            else
            {
                taskResult = null;
                return false;

            }
        }


    }
}


/*
 
一、压入 IL 栈顶指令。
    指令	        含义	                                        示例值（压栈）
    ldc.i4	        加载 4 字节整数	                                ldc.i4 123
    ldc.i4.s        加载 1 字节小整数（-128~127）	                ldc.i4.s 42
    ldc.i8	        加载 8 字节整数	                                ldc.i8 123456789L
    ldc.r4	        加载 4 字节浮点数（float）	                    ldc.r4 3.14f
    ldc.r8	        加载 8 字节浮点数（double）	                    ldc.r8 3.14159
    ldstr	        加载字符串	                                    ldstr "hello"
    ldnull	        加载 null	                                    ldnull
    ldarg, ldloc	加载参数或局部变量	                            ldarg.0, ldloc.1
    ldfld	        加载实例字段	                                ldfld int32 MyField
    ldsfld	        加载静态字段	                                ldsfld string StaticVal
    call, callvirt	方法调用的返回值会压栈	                        call int32 MyFunc()
    newobj	        创建对象实例，压入对象引用	                    newobj instance class MyClass::.ctor()

二、取栈指令（Pop 或消耗栈顶），这些指令从栈中取出值（或多个值）进行使用或丢弃。
    指令	        含义	                                        示例用途
    pop	            弹出并丢弃栈顶元素	                            pop
    stloc, starg	从栈顶弹出值，存入变量或参数	                stloc.0, starg.1
    stfld	        设置对象字段，先 pop 对象，再 pop 值	        stfld int32 MyField
    stsfld	        设置静态字段	                                stsfld string StaticVal
    stelem.*	    设置数组元素	                                stelem.i4 等
    call, callvirt	方法调用会消费入参数，栈中应提前压好所有参数
    ret	            从方法返回，会 pop 返回值（如有）	            ret

一、算术运算指令（Arithmetic）
对栈顶两个数进行操作，结果压回栈顶：

    指令	        含义	                                        示例（计算 a + b）
    add	            加法	                                        ldloc.0, ldloc.1, add
    sub	            减法	                                        ldloc.0, ldloc.1, sub
    mul	            乘法	                                        ldloc.0, ldloc.1, mul
    div	            除法（整除）	                                ldloc.0, ldloc.1, div
    div.un	        无符号除法	用于无符号整数
    rem	            取模	                                        ldloc.0, ldloc.1, rem
    rem.un	        无符号取模	
    neg	            取负数	                                        ldloc.0, neg
    inc	            不存在，需自己实现	                            ldloc.0, ldc.i4.1, add

二、逻辑/位运算指令（Bitwise & Logical）
    指令	        含义	                                        示例
    and	            位与（&）	                                    ldloc.0, ldloc.1, and
    or	            位或（|）	                                    ldloc.0, ldloc.1, or
    xor	            位异或（^）
    not	            没有，需要自己实现	一般使用 ldc -1 xor 实现
    shl	            左移（<<）	                                    ldloc.0, ldc.i4.2, shl
    shr	            右移（>>）	
    shr.un	        无符号右移	

三、比较指令（Comparison）
栈顶两个值比较，结果为 int32（0=false，1=true），通常用于判断表达式，配合 brtrue, brfalse 使用：

    指令	        含义
    ceq	            相等比较（==）
    cgt	            大于比较（signed）
    cgt.un	        大于比较（unsigned）
    clt	            小于比较（signed）
    clt.un	        小于比较（unsigned）


四、条件跳转指令（Branch）
    指令	        含义
    br label	    无条件跳转
    brtrue label	若栈顶为 true（非0）则跳转
    brfalse label	若栈顶为 false（0）则跳转
    beq label	    相等时跳转（会 pop 两个值）
    bne.un label	不等时跳转
    bgt, blt, bge, ble 等	常见关系跳转

五、类型转换指令（Cast / Convert）
    指令	        含义
    conv.i4	        转换为 int32
    conv.r8	        转为 double
    box	            装箱（值类型 -> object）
    unbox.any	    拆箱为值类型（object -> T）
    castclass	    强转引用类型
    isinst	        类型判断（返回非 null 代表是该类型）

六、对象相关指令（Object / Field / Property）
    指令	        含义
    newobj	        构造对象
    call	        调用静态或实例方法（非虚拟）
    callvirt	    调用虚拟方法（或接口方法）
    ldfld	        加载字段值
    stfld	        设置字段值
    ldsfld	        加载静态字段值
    stsfld	        设置静态字段值
    ldftn	        加载函数指针
    ldvirtftn	    加载虚拟函数指针
    constrained	    修饰后续 callvirt 以支持值类型调用
    
七、数组操作指令
    指令	        含义
    newarr	        创建一维数组
    ldlen	        获取数组长度
    ldelem.*	    读取数组元素
    stelem.*	    写入数组元素

八、局部变量和参数指令
    指令	        含义
    ldloc.*	        加载局部变量
    stloc.*	        设置局部变量
    ldarg.*	        加载参数
    starg.*	        设置参数

九、异常处理（配合 .try/.catch/.finally）
    指令	        含义
    .try, catch, finally	定义块
    throw	        抛出异常
    rethrow	        重新抛出当前异常
    leave	        离开 try 块
    endfinally	    finally 块结束

十、调试与无操作
    指令	        含义
    nop	            无操作，占位
    break	        触发调试中断
 */