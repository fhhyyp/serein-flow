using Serein.Library.Api;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{
    /// <summary>
    /// 基于类型创建表达式树反射委托（目前已使用EmitHelper代替）
    /// </summary>
    public static class ExpressionHelper
    {
        /// <summary>
        /// 缓存表达式树反射方法
        /// </summary>
        private static ConcurrentDictionary<string, Delegate> Cache { get; } = new ConcurrentDictionary<string, Delegate>();


        #region 基于类型的表达式反射构建委托

        #region 属性、字段的委托创建（表达式反射）

        /// <summary>
        /// 动态获取属性值
        /// </summary>
        public static Delegate PropertyGetter(Type type, string propertyName)
        {
            string cacheKey = $"{type.FullName}.{propertyName}.Getter";
            return Cache.GetOrAdd(cacheKey, _ => CreateGetterDelegate(type, propertyName));
        }
        /// <summary>
        /// 动态获取属性值
        /// </summary>
        private static Delegate CreateGetterDelegate(Type type, string propertyName)
        {
            var parameter = Expression.Parameter(typeof(object), "instance");
            var property = Expression.Property(Expression.Convert(parameter, type), propertyName);
            var lambda = Expression.Lambda(Expression.Convert(property, typeof(object)), parameter);
            return lambda.Compile();
        }

        /// <summary>
        /// 动态设置属性值
        /// </summary>
        public static Delegate PropertySetter(Type type, string propertyName)
        {
            string cacheKey = $"{type.FullName}.{propertyName}.Setter";
            return Cache.GetOrAdd(cacheKey, _ => CreateSetterDelegate(type, propertyName));
        }

        /// <summary>
        /// 动态设置属性值
        /// </summary>
        private static Delegate CreateSetterDelegate(Type type, string propertyName)
        {
            var parameter = Expression.Parameter(typeof(object), "instance");
            var value = Expression.Parameter(typeof(object), "value");
            var property = Expression.Property(Expression.Convert(parameter, type), propertyName);
            var assign = Expression.Assign(property, Expression.Convert(value, property.Type));
            var lambda = Expression.Lambda(assign, parameter, value);
            return lambda.Compile();
        }

        /// <summary>
        /// 动态获取字段值
        /// </summary>
        public static Delegate FieldGetter(Type type, string fieldName)
        {
            string cacheKey = $"{type.FullName}.{fieldName}.FieldGetter";
            return Cache.GetOrAdd(cacheKey, _ => CreateFieldGetterDelegate(type, fieldName));
        }
        /// <summary>
        /// 动态获取字段值
        /// </summary>
        private static Delegate CreateFieldGetterDelegate(Type type, string fieldName)
        {
            var parameter = Expression.Parameter(typeof(object), "instance");
            var field = Expression.Field(Expression.Convert(parameter, type), fieldName);
            var lambda = Expression.Lambda(Expression.Convert(field, typeof(object)), parameter);
            return lambda.Compile();
        }

        /// <summary>
        /// 动态设置字段值
        /// </summary>
        public static Delegate FieldSetter(Type type, string fieldName)
        {
            string cacheKey = $"{type.FullName}.{fieldName}.FieldSetter";
            return Cache.GetOrAdd(cacheKey, _ => CreateFieldSetterDelegate(type, fieldName));
        }
        /// <summary>
        /// 动态设置字段值
        /// </summary>
        private static Delegate CreateFieldSetterDelegate(Type type, string fieldName)
        {
            var parameter = Expression.Parameter(typeof(object), "instance");
            var value = Expression.Parameter(typeof(object), "value");
            var field = Expression.Field(Expression.Convert(parameter, type), fieldName);
            var assign = Expression.Assign(field, Expression.Convert(value, field.Type));
            var lambda = Expression.Lambda(assign, parameter, value);
            return lambda.Compile();
        }

        #endregion



        /// <summary>
        /// 表达式树构建无参数，无返回值方法
        /// </summary>
        public static Delegate MethodCaller(Type type, MethodInfo methodInfo)
        {
            string cacheKey = $"{type.FullName}.{methodInfo.Name}.MethodCaller";
            return Cache.GetOrAdd(cacheKey, _ => CreateMethodCallerDelegate(type, methodInfo));
        }

        /// <summary>
        /// 表达式树构建无参数，无返回值方法
        /// </summary>
        private static Delegate CreateMethodCallerDelegate(Type type, MethodInfo methodInfo)
        {
            var parameter = Expression.Parameter(typeof(object), "instance");
            var methodCall = Expression.Call(Expression.Convert(parameter, type), methodInfo);
            var lambda = Expression.Lambda(methodCall, parameter);
            // Action<object>
            return lambda.Compile();
        }

        /// <summary>
        /// 表达式树构建无参数，有返回值方法
        /// </summary>
        public static Delegate MethodCallerHaveResult(Type type, MethodInfo methodInfo)
        {
            string cacheKey = $"{type.FullName}.{methodInfo.Name}.MethodCallerHaveResult";
            return Cache.GetOrAdd(cacheKey, _ => CreateMethodCallerDelegateHaveResult(type, methodInfo));
        }
        /// <summary>
        /// 表达式树构建无参数，有返回值方法
        /// </summary>
        private static Delegate CreateMethodCallerDelegateHaveResult(Type type, MethodInfo methodInfo)
        {
            var parameter = Expression.Parameter(typeof(object), "instance");
            var methodCall = Expression.Call(Expression.Convert(parameter, type), methodInfo);

            if (IsGenericTask(methodInfo.ReturnType, out var taskResult))
            {
                if (taskResult is null)
                {
                    var lambda = Expression.Lambda<Func<object, Task>>(Expression.Convert(methodCall, typeof(Task)), parameter);
                    return lambda.Compile();
                }
                else
                {
                    var lambda = Expression.Lambda<Func<object, Task<object>>>(Expression.Convert(methodCall, typeof(Task<object>)), parameter);
                    return lambda.Compile();
                }
            }
            else
            {
                var lambda = Expression.Lambda<Func<object, object>>(Expression.Convert(methodCall, typeof(object)), parameter);
                return lambda.Compile();
            }

        }


        /// <summary>
        /// 表达式树构建多个参数,无返回值的方法
        /// </summary>
        public static Delegate MethodCaller(Type type, MethodInfo methodInfo, params Type[] parameterTypes)
        {
            string cacheKey = $"{type.FullName}.{methodInfo.Name}.MethodCaller";
            return Cache.GetOrAdd(cacheKey, _ => CreateMethodCallerDelegate(type, methodInfo, parameterTypes));
        }

        /// <summary>
        /// 表达式树构建多个参数,无返回值的方法
        /// </summary>
        private static Delegate CreateMethodCallerDelegate(Type type, MethodInfo methodInfo, Type[] parameterTypes)
        {
            /* var parameter = Expression.Parameter(typeof(object), "instance");

             var arguments = parameterTypes.Select((t, i) => Expression.Parameter(typeof(object), $"arg{i}")).ToArray();

             var convertedArguments = arguments.Select((arg, i) => Expression.Convert(arg, parameterTypes[i])).ToArray();
             var methodCall = Expression.Call(Expression.Convert(parameter, type),
                                              methodInfo,
                                              convertedArguments);
             var lambda = Expression.Lambda(methodCall, new[] { parameter }.Concat(arguments));
             var tmpAction = lambda.Compile();

             // Action<object, object[]>
             return lambda.Compile();*/

            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var argsParam = Expression.Parameter(typeof(object[]), "args");

            // 创建参数表达式
            var convertedArgs = parameterTypes.Select((paramType, index) =>
                Expression.Convert(Expression.ArrayIndex(argsParam, Expression.Constant(index)), paramType)
            ).ToArray();


            // 创建方法调用表达式
            var methodCall = Expression.Call(
                Expression.Convert(instanceParam, type),
                methodInfo,
                convertedArgs
            );

            // 创建 lambda 表达式
            var lambda = Expression.Lambda(
                methodCall,
                instanceParam,
                argsParam
            );

            // Func<object, object[], object>
            return lambda.Compile();
        }

        /// <summary>
        /// 表达式树构建多个参数,有返回值的方法
        /// </summary>
        public static Delegate MethodCallerHaveResult(Type type, MethodInfo methodInfo, Type[] parameterTypes)
        {
            string cacheKey = $"{type.FullName}.{methodInfo.Name}.MethodCallerHaveResult";
            return Cache.GetOrAdd(cacheKey, _ => CreateMethodCallerDelegateHaveResult(type, methodInfo, parameterTypes));
        }
        /// <summary>
        /// 表达式树构建多个参数,有返回值的方法
        /// </summary>
        private static Delegate CreateMethodCallerDelegateHaveResult(Type type, MethodInfo methodInfo, Type[] parameterTypes)
        {
            /*var instanceParam = Expression.Parameter(typeof(object), "instance");
            var argsParam = Expression.Parameter(typeof(object[]), "args");

            // 创建参数表达式
            var convertedArgs = parameterTypes.Select((paramType, index) =>
                Expression.Convert(Expression.ArrayIndex(argsParam, Expression.Constant(index)), paramType)
            ).ToArray();


            // 创建方法调用表达式
            var methodCall = Expression.Call(
                Expression.Convert(instanceParam, type),
                methodInfo,
                convertedArgs
            );

            // 创建 lambda 表达式
            var lambda = Expression.Lambda(
                Expression.Convert(methodCall, typeof(object)),
                instanceParam,
                argsParam
            );

            // Func<object, object[], object>
            return lambda.Compile();*/

            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var argsParam = Expression.Parameter(typeof(object[]), "args");

            // 创建参数表达式
            var convertedArgs = parameterTypes.Select((paramType, index) =>
                Expression.Convert(Expression.ArrayIndex(argsParam, Expression.Constant(index)), paramType)
            ).ToArray();


            // 创建方法调用表达式
            var methodCall = Expression.Call(
                Expression.Convert(instanceParam, type),
                methodInfo,
                convertedArgs
            );

            if (IsGenericTask(methodInfo.ReturnType, out var taskResult))
            {
                if (taskResult is null)
                {
                    var lambda = Expression.Lambda<Func<object, object[], Task>>
                        (Expression.Convert(methodCall, typeof(Task)), instanceParam, argsParam);
                    return lambda.Compile();
                }
                else
                {
                    var lambda = Expression.Lambda<Func<object, object[], Task<object>>>
                        (Expression.Convert(methodCall, typeof(Task<object>)), instanceParam, argsParam);
                    return lambda.Compile();
                }
            }
            else
            {
                var lambda = Expression.Lambda<Func<object, object[], object>>
                        (Expression.Convert(methodCall, typeof(object)), instanceParam, argsParam);
                return lambda.Compile();
            }


        }


        /// <summary>
        /// 表达式树构建无参数，有返回值(Task<object>)的方法（触发器）
        /// </summary>
        public static Delegate MethodCallerAsync(Type type, MethodInfo methodInfo)
        {
            string cacheKey = $"{type.FullName}.{methodInfo.Name}.MethodCallerAsync";
            return Cache.GetOrAdd(cacheKey, _ => CreateMethodCallerDelegateAsync(type, methodInfo));
        }

        /// <summary>
        /// 表达式树构建无参数，有返回值(Task<object>)的方法（触发器）
        /// </summary>
        /// <param name="type"></param>
        /// <param name="methodInfo"></param>
        /// <returns></returns>
        private static Delegate CreateMethodCallerDelegateAsync(Type type, MethodInfo methodInfo)
        {
            var parameter = Expression.Parameter(typeof(object), "instance");
            var methodCall = Expression.Call(Expression.Convert(parameter, type), methodInfo);
            var lambda = Expression.Lambda<Func<object, Task<object>>>(
                Expression.Convert(methodCall, typeof(Task<object>)), parameter);
            // Func<object, Task<object>>
            return lambda.Compile();
        }



        /// <summary>
        /// 表达式树构建多个参数，有返回值(Task-object)的方法（触发器）
        /// </summary>
        public static Delegate MethodCallerAsync(Type type, MethodInfo method, params Type[] parameterTypes)
        {

            string cacheKey = $"{type.FullName}.{method.Name}.MethodCallerAsync";
            return Cache.GetOrAdd(cacheKey, _ => CreateMethodCallerDelegateAsync(type, method, parameterTypes));
        }

        /// <summary>
        /// 表达式树构建多个参数，有返回值(Task<object>)的方法（触发器）
        /// </summary>
        private static Delegate CreateMethodCallerDelegateAsync(Type type, MethodInfo methodInfo, Type[] parameterTypes)
        {
            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var argsParam = Expression.Parameter(typeof(object[]), "args");

            // 创建参数表达式
            var convertedArgs = parameterTypes.Select((paramType, index) =>
                Expression.Convert(Expression.ArrayIndex(argsParam, Expression.Constant(index)), paramType)
            ).ToArray();


            // 创建方法调用表达式
            var methodCall = Expression.Call(
                Expression.Convert(instanceParam, type),
                methodInfo,
                convertedArgs
            );

            // 创建 lambda 表达式
            var lambda = Expression.Lambda<Func<object, object[], Task<IFlipflopContext<object>>>>(
                Expression.Convert(methodCall, typeof(Task<IFlipflopContext<object>>)),
                instanceParam,
                argsParam
            );
            //获取返回类型
            //var returnType = methodInfo.ReturnType;
            //var lambda = Expression.Lambda(
            //        typeof(Func<,,>).MakeGenericType(typeof(object), typeof(object[]), returnType),
            //        Expression.Convert(methodCall, returnType),
            //        instanceParam,
            //        argsParam
            //    );


            //var resule = task.DynamicInvoke((object)[Activator.CreateInstance(type), [new DynamicContext(null)]]);
            return lambda.Compile();
        }





        public static bool IsGenericTask(Type returnType, out Type taskResult)
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

        public static Delegate AutoCreate(Type type, MethodInfo methodInfo)
        {
            Type returnType = methodInfo.ReturnType;
            var parameterTypes = methodInfo.GetParameters().Select(p => p.ParameterType).ToArray();
            var parameterCount = parameterTypes.Length;

            if (returnType == typeof(void))
            {
                if (parameterCount == 0)
                {
                    // 无返回值，无参数
                    return MethodCaller(type, methodInfo);
                }
                else
                {
                    // 无返回值，有参数
                    return MethodCaller(type, methodInfo, parameterTypes);
                }
            }
            else
            {
                if (parameterCount == 0)
                {
                    // 有返回值，无参数
                    return MethodCallerHaveResult(type, methodInfo);
                }
                else
                {
                    // 有返回值，有参数
                    return MethodCallerHaveResult(type, methodInfo, parameterTypes);
                }
            }

            #endregion



        }
    }
}
