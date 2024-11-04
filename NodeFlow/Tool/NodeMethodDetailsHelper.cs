using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.Library;
using System.Collections.Concurrent;
using System.Reflection;
using Serein.Library.FlowNode;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Serein.NodeFlow.Tool;

public static class NodeMethodDetailsHelper
{

    /// <summary>
    /// 获取处理方法
    /// </summary>
    public static IEnumerable<MethodInfo> GetMethodsToProcess(Type type)
    {
        return type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       .Where(m => m.GetCustomAttribute<NodeActionAttribute>()?.Scan == true);
    }

    /// <summary>
    /// 创建方法信息/委托信息
    /// </summary>
    /// <param name="type">方法所属的类型</param>
    /// <param name="methodInfo">方法信息</param>
    /// <param name="assemblyName">方法所属的程序集名称</param>
    /// <param name="methodDetails">创建的方法描述，用来生成节点信息</param>
    /// <param name="delegateDetails">方法对应的Emit动态委托</param>
    /// <returns>指示是否创建成功</returns>
    public static bool TryCreateDetails(Type type, 
                                        MethodInfo methodInfo,  
                                        string assemblyName,
                                        [MaybeNullWhen(false)]  out MethodDetails methodDetails,
                                        [MaybeNullWhen(false)]  out DelegateDetails delegateDetails)
    {
        

        var attribute = methodInfo.GetCustomAttribute<NodeActionAttribute>();
        if(attribute is null || attribute.Scan == false)
        {
            methodDetails = null;
            delegateDetails = null;
            return false;
        }
        
        var methodName = $"{assemblyName}.{type.Name}.{methodInfo.Name}"; 
        Console.WriteLine("loading method : " + methodName);

        // 创建参数信息
        var explicitDataOfParameters = GetExplicitDataOfParameters(methodInfo.GetParameters());

        

        //// 通过表达式树生成委托
        //var methodDelegate = GenerateMethodDelegate(type,   // 方法所在的对象类型
        //                                            method, // 方法信息
        //                                            method.GetParameters(),// 方法参数
        //                                            method.ReturnType);// 返回值

        

        Type? returnType;
        bool isTask = IsGenericTask(methodInfo.ReturnType, out var taskResult);

        if (attribute.MethodDynamicType == Library.NodeType.Flipflop)
        {
            if (methodInfo.ReturnType.IsGenericType && methodInfo.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                // 获取 Task<> 的泛型参数类型
                var innerType = methodInfo.ReturnType.GetGenericArguments()[0];
                if (innerType.IsGenericType && innerType.GetGenericTypeDefinition() == typeof(IFlipflopContext<>))
                {
                    var flipflopType = innerType.GetGenericArguments()[0];
                    returnType = flipflopType;
                }
                else
                {
                    Console.WriteLine($"[{methodName}]跳过创建，返回类型非预期的Task<IFlipflopContext<TResult>>。");
                    methodDetails = null;
                    delegateDetails = null;
                    return false;
                }
            }
            else
            {
                Console.WriteLine($"[{methodName}]跳过创建，因为触发器方法的返回值并非Task<>，将无法等待。");
                methodDetails = null;
                delegateDetails = null;
                return false;
            }
              
            //if (!isTask || taskResult != typeof(IFlipflopContext<object>))
            //{
            //    
            //}
            
        }
        else if(isTask)
        {
            returnType = taskResult is null ? typeof(Task) : taskResult;
        }
        else
        {
            returnType = methodInfo.ReturnType;
        }

        if (string.IsNullOrEmpty(attribute.AnotherName)){
            attribute.AnotherName = methodInfo.Name;
        }


       
        var asyncPrefix = "[异步]"; // IsGenericTask(returnType) ? "[async]" : ;
        var methodMethodAnotherName = isTask ? asyncPrefix + attribute.AnotherName : attribute.AnotherName;

        bool hasParamsArg = false;
        if (explicitDataOfParameters.Length > 0)
        {
            hasParamsArg = explicitDataOfParameters[^1].IsParams; // 取最后一个参数描述，判断是否为params 入参
        }

        var md = new MethodDetails() // 从DLL生成方法描述（元数据）
        {
            ActingInstanceType = type,
            // ActingInstance = instance, 
            MethodName = methodName,
            AssemblyName = assemblyName,
            MethodDynamicType = attribute.MethodDynamicType,
            MethodLockName = attribute.LockName,
            MethodAnotherName = methodMethodAnotherName,
            ParameterDetailss = explicitDataOfParameters,
            ReturnType = returnType,
            // 如果存在可变参数，取最后一个元素的下标，否则为-1；
            ParamsArgIndex = hasParamsArg ? explicitDataOfParameters.Length - 1 : -1,
        };

        //var emitMethodType = EmitHelper.CreateDynamicMethod(methodInfo, out var methodDelegate);// 返回值


        var dd = new DelegateDetails(methodInfo) ; // 构造委托

        methodDetails = md;
        delegateDetails = dd;
        return true;

    }

    public static bool IsGenericTask(Type returnType, out Type? taskResult)
    {
        // 判断是否为 Task 类型或泛型 Task<T>
        if (returnType == typeof(Task))
        {
            taskResult = null;
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


    private static ConcurrentDictionary<string, (object, MethodInfo)> ConvertorInstance =[];

    /// <summary>
    /// 获取参数信息
    /// </summary>
    /// <param name="parameters"></param>
    /// <returns></returns>
    private static ParameterDetails[] GetExplicitDataOfParameters(ParameterInfo[] parameters)
    {

        var tempParams =  parameters.Select((it, index) =>
        {
            Type paremType;

            #region 存在“枚举=>类型”转换器
            if (it.GetCustomAttribute<EnumTypeConvertorAttribute>() is EnumTypeConvertorAttribute attribute1 && attribute1 is not null)
            {
                // 存在类型选择器
                paremType = attribute1.EnumType;
                return GetExplicitDataOfParameter(it, index, paremType, true); // “枚举=>类型”转换器 获取参数
            }
            #endregion
            #region 存在自定义的转换器
            else if (it.GetCustomAttribute<BindConvertorAttribute>() is BindConvertorAttribute attribute2 && attribute2 is not null)
            {
                paremType = attribute2.EnumType;

                string key = typeof(IEnumConvertor<,>).FullName + attribute2.EnumType.FullName + attribute2.ConvertorType.FullName;

                if (!ConvertorInstance.ContainsKey(key))
                {
                    Type enumConvertorType = typeof(IEnumConvertor<,>);
                    // 定义具体类型
                    Type specificType = enumConvertorType.MakeGenericType(attribute2.EnumType, it.ParameterType);
                    // 获取实现类的类型
                    Type implementorType = attribute2.ConvertorType;
                    // 创建实现类的实例
                    object instance = Activator.CreateInstance(implementorType);
                    // 调用 Convert 方法
                    MethodInfo convertMethod = implementorType.GetMethod("Convertor");
                    ConvertorInstance[key] = (instance, convertMethod);
                }

                object func(object enumValue)
                {
                    (var obj, var methodInfo) = ConvertorInstance[key];
                    return methodInfo?.Invoke(obj, [enumValue]);
                }
                // 确保实例实现了所需接口
                ParameterDetails ed = GetExplicitDataOfParameter(it, index, paremType, true, func);  // 自定义的转换器 获取参数

                return ed;
            }
            #endregion
            #region 常规方法的获取参数
            else
            {
                var tmp = GetExplicitDataOfParameter(it, index, it.ParameterType, it.HasDefaultValue); // 常规方法的获取参数
                return tmp;
            } 
            #endregion
        }).ToArray();

       
        return tempParams;
    }

    private static ParameterDetails GetExplicitDataOfParameter(ParameterInfo parameterInfo,
                                                               int index,
                                                               Type explicitParemType,
                                                               bool isExplicitData,
                                                               Func<object, object> func = null)
    {

        bool hasParams = parameterInfo.IsDefined(typeof(ParamArrayAttribute)); // 判断是否为可变参数
        Type dataType;
        if (hasParams && parameterInfo.ParameterType.GetElementType() is Type paramsArgType) // 获取可变参数的子项类型
        {
            // 可选参数为 Array 类型，所以需要获取子项类型
            // 如果 hasParams 为 true ，说明一定存在可选参数，所以 paramsArgType 一定不为 null
            dataType = paramsArgType;
            explicitParemType = paramsArgType;
        }
        else
        {
            dataType = parameterInfo.ParameterType;
        }

        string explicitTypeName = GetExplicitTypeName(explicitParemType);
        var items = GetExplicitItems(explicitParemType, explicitTypeName);
        if ("Bool".Equals(explicitTypeName)) explicitTypeName = "Select"; // 布尔值 转为 可选类型
        return new ParameterDetails
        {
            IsExplicitData = isExplicitData, //attribute is null ? parameterInfo.HasDefaultValue : true,
            Index = index, // 索引
            ExplicitTypeName = explicitTypeName, // Select/Bool/Value
            ExplicitType = explicitParemType,// 显示的入参类型
            Convertor = func, // 转换器
            DataType = dataType, // 实际的入参类型
            Name = parameterInfo.Name,
            DataValue = parameterInfo.HasDefaultValue ? parameterInfo?.DefaultValue?.ToString() : "", // 如果存在默认值，则使用默认值
            Items = items.ToArray(), // 如果是枚举值入参，则获取枚举类型的字面量
            IsParams = hasParams,  // 判断是否为可变参数
        };

    }



    /// <summary>
    /// 判断使用输入器还是选择器
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private static string GetExplicitTypeName(Type type)
    {
        
        return type switch
        {
            Type t when t.IsEnum => "Select",
            Type t when t == typeof(bool) => "Bool",
            Type t when t == typeof(string) => "Value",
            Type t when t == typeof(int) => "Value",
            Type t when t == typeof(double) => "Value",
            _ => "Value"
        };
    }


    /// <summary>
    /// 获取参数列表选项
    /// </summary>
    /// <param name="type"></param>
    /// <param name="explicitTypeName"></param>
    /// <returns></returns>
    private static IEnumerable<string> GetExplicitItems(Type type, string explicitTypeName)
    {
        IEnumerable<string>  items =  explicitTypeName switch
        {
            "Select" => Enum.GetNames(type),
            "Bool" => ["True", "False"],
            _ => []
        };
        return items;
    }

    //private static Delegate GenerateMethodDelegate(Type type, MethodInfo methodInfo, ParameterInfo[] parameters, Type returnType)
    //{
    //    var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();
    //    var parameterCount = parameters.Length;

    //    if (returnType == typeof(void))
    //    {
    //        if (parameterCount == 0)
    //        {
    //            // 无返回值，无参数
    //            return ExpressionHelper.MethodCaller(type, methodInfo);
    //        }
    //        else
    //        {
    //            // 无返回值，有参数
    //            return ExpressionHelper.MethodCaller(type, methodInfo, parameterTypes);
    //        }
    //    }
    //    // else if (returnType == typeof(Task<FlipflopContext)) // 触发器
    //    else if (FlipflopFunc.IsTaskOfFlipflop(returnType)) // 触发器
    //    {
    //        if (parameterCount == 0)
    //        {
    //            // 有返回值，无参数
    //            return ExpressionHelper.MethodCallerAsync(type, methodInfo);
    //        }
    //        else
    //        {
    //            // 有返回值，有参数
    //            return ExpressionHelper.MethodCallerAsync(type, methodInfo, parameterTypes);
    //        }
    //    }
    //    else
    //    {
    //        if (parameterCount == 0)
    //        {
    //            // 有返回值，无参数
    //            return ExpressionHelper.MethodCallerHaveResult(type, methodInfo);
    //        }
    //        else
    //        {
    //            // 有返回值，有参数
    //            return ExpressionHelper.MethodCallerHaveResult(type, methodInfo, parameterTypes);
    //        }
    //    }
    //}

}


