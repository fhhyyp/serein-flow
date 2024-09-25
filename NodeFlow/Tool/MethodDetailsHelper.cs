using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.Core.NodeFlow;
using Serein.Library.Entity;
using System.Collections.Concurrent;
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
    public static List<MethodDetails> GetList(Type type)
    {
        var methodDetailsDictionary = new List<MethodDetails>();
        var assemblyName = type.Assembly.GetName().Name;
        var methods = GetMethodsToProcess(type);

        foreach (var method in methods)
        {

            var methodDetails = CreateMethodDetails(type, method, assemblyName);
            methodDetailsDictionary.Add(methodDetails);
        }

        return methodDetailsDictionary.OrderBy(it => it.MethodName).ToList();
    }
    /// <summary>
    /// 获取处理方法
    /// </summary>
    private static IEnumerable<MethodInfo> GetMethodsToProcess(Type type)
    {
        return type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       .Where(m => m.GetCustomAttribute<NodeActionAttribute>()?.Scan == true);
    }
    /// <summary>
    /// 创建方法信息
    /// </summary>
    /// <returns></returns>
    private static MethodDetails CreateMethodDetails(Type type, MethodInfo method, string assemblyName)
    {

        var methodName = method.Name;
        var attribute = method.GetCustomAttribute<NodeActionAttribute>();
        if(attribute is null)
        {
            return null;
        }
        var explicitDataOfParameters = GetExplicitDataOfParameters(method.GetParameters());
        // 生成委托
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

        return new MethodDetails
        {
            ActingInstanceType = type,
            // ActingInstance = instance,
            MethodName = dllTypeMethodName,
            MethodDelegate = methodDelegate,
            MethodDynamicType = attribute.MethodDynamicType,
            MethodLockName = attribute.LockName,
            MethodTips = attribute.MethodTips,
            ExplicitDatas = explicitDataOfParameters,
            ReturnType = returnType,
        };

    }

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
            //var attribute = it.ParameterType.GetCustomAttribute<EnumConvertorAttribute>();
            //if (attribute is not null && attribute.Enum.IsEnum)
            //{
            //    // 存在选择器
            //    paremType = attribute.Enum;
            //}
            //else
            //{
            //    paremType = it.ParameterType;
            //}
            paremType = it.ParameterType;
            string explicitTypeName = GetExplicitTypeName(paremType);
            var items = GetExplicitItems(paremType, explicitTypeName);
            if ("Bool".Equals(explicitTypeName)) explicitTypeName = "Select"; // 布尔值 转为 可选类型
            return new ExplicitData
            {
                IsExplicitData = it.HasDefaultValue,
                Index = index,
                // ExplicitType = it.ParameterType,
                ExplicitTypeName = explicitTypeName,
                DataType = it.ParameterType,
                ParameterName = it.Name,
                DataValue = it.HasDefaultValue ? it.DefaultValue.ToString() : "",
                Items = items.ToArray(),
            };



        }).ToArray();
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


