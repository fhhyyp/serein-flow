using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.Core.NodeFlow;
using Serein.Library.Entity;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace Serein.NodeFlow.Tool;

public static class MethodDetailsHelperTmp
{
    
    /// <summary>
    /// 生成方法信息
    /// </summary>
    /// <param name="serviceContainer"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    //public static List<MethodDetails> GetList(Type type)
    //{
    //    var methodDetailsDictionary = new List<MethodDetails>();
    //    var delegateDictionary = new List<Delegate>();
    //    var assemblyName = type.Assembly.GetName().Name;
    //    var methods = GetMethodsToProcess(type);

    //    foreach (var method in methods)
    //    {

    //        (var methodDetails,var methodDelegate) = CreateMethodDetails(type, method, assemblyName);
 
    //        methodDetailsDictionary.Add(methodDetails);
    //        delegateDictionary.Add(methodDelegate);
    //    }

    //    var mds = methodDetailsDictionary.OrderBy(it => it.MethodName).ToList();
    //    var dels = delegateDictionary;

    //    return mds;
    //}
   
    /// <summary>
    /// 获取处理方法
    /// </summary>
    public static IEnumerable<MethodInfo> GetMethodsToProcess(Type type)
    {
        return type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       .Where(m => m.GetCustomAttribute<NodeActionAttribute>()?.Scan == true);
    }
    /// <summary>
    /// 创建方法信息
    /// </summary>
    /// <returns></returns>
    public static (MethodDetails?,Delegate?) CreateMethodDetails(Type type, MethodInfo method, string assemblyName)
    {

        var methodName = method.Name;
        var attribute = method.GetCustomAttribute<NodeActionAttribute>();
        if(attribute is null)
        {
            return (null, null);
        }
        var explicitDataOfParameters = GetExplicitDataOfParameters(method.GetParameters());
        //// 生成委托
        var methodDelegate = GenerateMethodDelegate(type,   // 方法所在的对象类型
                                                    method, // 方法信息
                                                    method.GetParameters(),// 方法参数
                                                    method.ReturnType);// 返回值

        Type returnType;
        if (attribute?.MethodDynamicType == Library.Enums.NodeType.Flipflop)
        {
            // 触发器节点
            returnType = attribute.ReturnType;
        }
        else
        {
            returnType = method.ReturnType;
        }

        var dllTypeName = $"{assemblyName}.{type.Name}";
        // object instance = Activator.CreateInstance(type);
        var dllTypeMethodName = $"{assemblyName}.{type.Name}.{method.Name}";

        var md = new MethodDetails
        {
            ActingInstanceType = type,
            // ActingInstance = instance,
            MethodName = dllTypeMethodName,
            MethodDynamicType = attribute.MethodDynamicType,
            MethodLockName = attribute.LockName,
            MethodTips = attribute.MethodTips,
            ExplicitDatas = explicitDataOfParameters,
            ReturnType = returnType,
        };

        return (md, methodDelegate);

    }


    private static ConcurrentDictionary<string, (object, MethodInfo)> ConvertorInstance =[];

    /// <summary>
    /// 获取参数信息
    /// </summary>
    /// <param name="parameters"></param>
    /// <returns></returns>
    private static ExplicitData[] GetExplicitDataOfParameters(ParameterInfo[] parameters)
    {

        return parameters.Select((it, index) =>
        {
            Type paremType;
            
            if (it.GetCustomAttribute<EnumTypeConvertorAttribute>() is EnumTypeConvertorAttribute attribute1  && attribute1 is not null)
            {
                // 存在类型选择器
                paremType = attribute1.EnumType;
                return GetExplicitDataOfParameter(it, index, paremType, true);
            }
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

                Func<object, object> func = (enumValue) =>
                {
                    (var obj,var methodInfo) = ConvertorInstance[key];
                    return methodInfo?.Invoke(obj, [enumValue]);
                };
                // 确保实例实现了所需接口
                ExplicitData ed = GetExplicitDataOfParameter(it, index, paremType, true, func);

                return ed;
            }
            else
            {
                return GetExplicitDataOfParameter(it, index, it.ParameterType, it.HasDefaultValue);
            }
            //string explicitTypeName = GetExplicitTypeName(paremType);
            //var items = GetExplicitItems(paremType, explicitTypeName);
            //if ("Bool".Equals(explicitTypeName)) explicitTypeName = "Select"; // 布尔值 转为 可选类型
            //return new ExplicitData
            //{
            //    IsExplicitData = attribute is null ? it.HasDefaultValue: true,
            //    Index = index,
            //    ExplicitTypeName = explicitTypeName,
            //    ExplicitType = paremType,
            //    DataType = it.ParameterType,
            //    ParameterName = it.Name,
            //    DataValue = it.HasDefaultValue ? it?.DefaultValue?.ToString() : "",
            //    Items = items.ToArray(),
            //};
        }).ToArray();
    }

    private static ExplicitData GetExplicitDataOfParameter(ParameterInfo parameterInfo,
                                                           int index,
                                                           Type paremType,
                                                           bool isExplicitData,
                                                           Func<object, object> func = null)
    {

        string explicitTypeName = GetExplicitTypeName(paremType);
        var items = GetExplicitItems(paremType, explicitTypeName);
        if ("Bool".Equals(explicitTypeName)) explicitTypeName = "Select"; // 布尔值 转为 可选类型
        return new ExplicitData
        {
            IsExplicitData = isExplicitData, //attribute is null ? parameterInfo.HasDefaultValue : true,
            Index = index,
            ExplicitTypeName = explicitTypeName,
            ExplicitType = paremType,
            Convertor = func,
            DataType = parameterInfo.ParameterType,
            ParameterName = parameterInfo.Name,
            DataValue = parameterInfo.HasDefaultValue ? parameterInfo?.DefaultValue?.ToString() : "",
            Items = items.ToArray(),
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

    private static Delegate GenerateMethodDelegate(Type type, MethodInfo methodInfo, ParameterInfo[] parameters, Type returnType)
    {
        var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();
        var parameterCount = parameters.Length;

        if (returnType == typeof(void))
        {
            if (parameterCount == 0)
            {
                // 无返回值，无参数
                return ExpressionHelper.MethodCaller(type, methodInfo);
            }
            else
            {
                // 无返回值，有参数
                return ExpressionHelper.MethodCaller(type, methodInfo, parameterTypes);
            }
        }
        // else if (returnType == typeof(Task<FlipflopContext)) // 触发器
        else if (FlipflopFunc.IsTaskOfFlipflop(returnType)) // 触发器
        {
            if (parameterCount == 0)
            {
                // 有返回值，无参数
                return ExpressionHelper.MethodCallerAsync(type, methodInfo);
            }
            else
            {
                // 有返回值，有参数
                return ExpressionHelper.MethodCallerAsync(type, methodInfo, parameterTypes);
            }
        }
        else
        {
            if (parameterCount == 0)
            {
                // 有返回值，无参数
                return ExpressionHelper.MethodCallerHaveResult(type, methodInfo);
            }
            else
            {
                // 有返回值，有参数
                return ExpressionHelper.MethodCallerHaveResult(type, methodInfo, parameterTypes);
            }
        }
    }

}


