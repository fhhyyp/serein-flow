using Serein.Library.Api;
using Serein.Library.Core.NodeFlow;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Serein.NodeFlow.Tool
{
    /// <summary>
    /// 对于实例创建的表达式树反射
    /// </summary>
    public static class ExpressionHelper
    {
        /// <summary>
        /// 缓存表达式树反射方法
        /// </summary>
        private static ConcurrentDictionary<string, Delegate> Cache { get; } = new ConcurrentDictionary<string, Delegate>();

        public static List<string> GetCacheKey()
        {
            return [.. Cache.Keys];
        }

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
            var lambda = Expression.Lambda(Expression.Convert(methodCall, typeof(object)), parameter);
            // Func<object, object>
            return lambda.Compile();
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

            // 创建 lambda 表达式
            var lambda = Expression.Lambda<Func<object, object[], object>>(
                Expression.Convert(methodCall, typeof(object)),
                instanceParam,
                argsParam
            );
            //var resule = task.DynamicInvoke((object)[Activator.CreateInstance(type), [new DynamicContext(null)]]);
            return lambda.Compile();
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
           var lambda = Expression.Lambda<Func<object, object[], Task<IFlipflopContext>>>(
               Expression.Convert(methodCall, typeof(Task<IFlipflopContext>)),
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



        #region 单参数

        /// <summary>
        /// 表达式树构建单参数,无返回值的方法
        /// </summary>
        public static Delegate MethodCaller(Type type, string methodName, Type parameterType)
        {
            string cacheKey = $"{type.FullName}.{methodName}.MethodCallerWithParam";
            return Cache.GetOrAdd(cacheKey, _ => CreateMethodCallerDelegate(type, methodName, parameterType));
        }
        /// <summary>
        /// 表达式树构建单参数,无返回值的方法
        /// </summary>
        private static Delegate CreateMethodCallerDelegate(Type type, string methodName, Type parameterType)
        {
            var parameter = Expression.Parameter(typeof(object), "instance");
            var argument = Expression.Parameter(typeof(object), "argument");
            var methodCall = Expression.Call(Expression.Convert(parameter, type),
                                             type.GetMethod(methodName, [parameterType])!,
                                             Expression.Convert(argument, parameterType));
            var lambda = Expression.Lambda(methodCall, parameter, argument);
            return lambda.Compile();
        }

        /// <summary>
        /// 表达式树构建单参数,有返回值的方法
        /// </summary>
        public static Delegate MethodCallerWithResult(Type type, string methodName, Type parameterType, Type returnType)
        {
            string cacheKey = $"{type.FullName}.{methodName}.MethodCallerWithResult";
            return Cache.GetOrAdd(cacheKey, _ => CreateMethodCallerDelegateWithResult(type, methodName, parameterType, returnType));
        }
        /// <summary>
        /// 表达式树构建单参数,有返回值的方法
        /// </summary>
        private static Delegate CreateMethodCallerDelegateWithResult(Type type, string methodName, Type parameterType, Type returnType)
        {
            var parameter = Expression.Parameter(typeof(object), "instance");
            var argument = Expression.Parameter(typeof(object), "argument");
            var methodCall = Expression.Call(Expression.Convert(parameter, type),
                                             type.GetMethod(methodName, [parameterType])!,
                                             Expression.Convert(argument, parameterType));
            var lambda = Expression.Lambda(Expression.Convert(methodCall, typeof(object)), parameter, argument);


            return lambda.Compile();
        }

        #endregion

        #endregion


        #region 泛型表达式反射构建方法（已注释）
        /*


                /// <summary>
                /// 动态获取属性值
                /// </summary>
                /// <typeparam name="T"></typeparam>
                /// <typeparam name="TProperty"></typeparam>
                /// <param name="propertyName"></param>
                /// <returns></returns>
                public static Func<T, TProperty> PropertyGetter<T, TProperty>(string propertyName)
                {
                    string cacheKey = $"{typeof(T).FullName}.{propertyName}.Getter";
                    return (Func<T, TProperty>)Cache.GetOrAdd(cacheKey, _ => CreateGetterDelegate<T, TProperty>(propertyName));
                }

                private static Func<T, TProperty> CreateGetterDelegate<T, TProperty>(string propertyName)
                {
                    var parameter = Expression.Parameter(typeof(T), "instance");
                    var property = Expression.Property(parameter, propertyName);
                    var lambda = Expression.Lambda<Func<T, TProperty>>(property, parameter);
                    return lambda.Compile();
                }

                /// <summary>
                /// 动态设置属性值
                /// </summary>
                /// <typeparam name="T"></typeparam>
                /// <typeparam name="TProperty"></typeparam>
                /// <param name="propertyName"></param>
                /// <returns></returns>
                public static Action<T, TProperty> PropertySetter<T, TProperty>(string propertyName)
                {
                    string cacheKey = $"{typeof(T).FullName}.{propertyName}.Setter";
                    return (Action<T, TProperty>)Cache.GetOrAdd(cacheKey, _ => CreateSetterDelegate<T, TProperty>(propertyName));
                }

                private static Action<T, TProperty> CreateSetterDelegate<T, TProperty>(string propertyName)
                {
                    var parameter = Expression.Parameter(typeof(T), "instance");
                    var value = Expression.Parameter(typeof(TProperty), "value");
                    var property = Expression.Property(parameter, propertyName);
                    var assign = Expression.Assign(property, value);
                    var lambda = Expression.Lambda<Action<T, TProperty>>(assign, parameter, value);
                    return lambda.Compile();
                }

                /// <summary>
                ///  动态获取字段值
                /// </summary>
                /// <typeparam name="T"></typeparam>
                /// <typeparam name="TField"></typeparam>
                /// <param name="fieldName"></param>
                /// <returns></returns>
                public static Func<T, TField> FieldGetter<T, TField>(string fieldName)
                {
                    string cacheKey = $"{typeof(T).FullName}.{fieldName}.FieldGetter";
                    return (Func<T, TField>)Cache.GetOrAdd(cacheKey, _ => CreateFieldGetterDelegate<T, TField>(fieldName));
                }

                private static Func<T, TField> CreateFieldGetterDelegate<T, TField>(string fieldName)
                {
                    var parameter = Expression.Parameter(typeof(T), "instance");
                    var field = Expression.Field(parameter, fieldName);
                    var lambda = Expression.Lambda<Func<T, TField>>(field, parameter);
                    return lambda.Compile();
                }

                /// <summary>
                /// 动态设置字段值
                /// </summary>
                /// <typeparam name="T"></typeparam>
                /// <typeparam name="TField"></typeparam>
                /// <param name="fieldName"></param>
                /// <returns></returns>
                public static Action<T, TField> FieldSetter<T, TField>(string fieldName)
                {
                    string cacheKey = $"{typeof(T).FullName}.{fieldName}.FieldSetter";
                    return (Action<T, TField>)Cache.GetOrAdd(cacheKey, _ => CreateFieldSetterDelegate<T, TField>(fieldName));
                }

                private static Action<T, TField> CreateFieldSetterDelegate<T, TField>(string fieldName)
                {
                    var parameter = Expression.Parameter(typeof(T), "instance");
                    var value = Expression.Parameter(typeof(TField), "value");
                    var field = Expression.Field(parameter, fieldName);
                    var assign = Expression.Assign(field, value);
                    var lambda = Expression.Lambda<Action<T, TField>>(assign, parameter, value);
                    return lambda.Compile();
                }





                /// <summary>
                /// 动态调用无参数方法
                /// </summary>
                /// <typeparam name="T"></typeparam>
                /// <param name="methodName"></param>
                /// <returns></returns>
                public static Action<T> MethodCaller<T>(string methodName)
                {
                    string cacheKey = $"{typeof(T).FullName}.{methodName}.MethodCaller";
                    return (Action<T>)Cache.GetOrAdd(cacheKey, _ => CreateMethodCallerDelegate<T>(methodName));
                }

                private static Action<T> CreateMethodCallerDelegate<T>(string methodName)
                {
                    var parameter = Expression.Parameter(typeof(T), "instance");
                    var methodCall = Expression.Call(parameter, typeof(T).GetMethod(methodName));
                    var lambda = Expression.Lambda<Action<T>>(methodCall, parameter);
                    return lambda.Compile();
                }

                /// <summary>
                /// 动态调用无参有返回值方法
                /// </summary>
                /// <typeparam name="T"></typeparam>
                /// <typeparam name="TResult"></typeparam>
                /// <param name="methodName"></param>
                /// <returns></returns>
                public static Func<T, TResult> MethodCallerHaveResul<T, TResult>(string methodName)
                {
                    string cacheKey = $"{typeof(T).FullName}.{methodName}.MethodCaller";
                    return (Func<T, TResult>)Cache.GetOrAdd(cacheKey, _ => CreateMethodCallerDelegateHaveResult<T, TResult>(methodName));
                }

                private static Func<T, TResult> CreateMethodCallerDelegateHaveResult<T, TResult>(string methodName)
                {
                    var parameter = Expression.Parameter(typeof(T), "instance");
                    var methodCall = Expression.Call(parameter, typeof(T).GetMethod(methodName));
                    var lambda = Expression.Lambda<Func<T, TResult>>(methodCall, parameter);
                    return lambda.Compile();
                }


                /// <summary>
                /// 动态调用单参数无返回值的方法
                /// </summary>
                /// <typeparam name="T"></typeparam>
                /// <typeparam name="TParam"></typeparam>
                /// <param name="methodName"></param>
                /// <returns></returns>
                public static Action<T, TParam> MethodCaller<T, TParam>(string methodName)
                {
                    string cacheKey = $"{typeof(T).FullName}.{methodName}.MethodCallerWithParam";
                    return (Action<T, TParam>)Cache.GetOrAdd(cacheKey, _ => CreateMethodCallerDelegate<T, TParam>(methodName));
                }

                private static Action<T, TParam> CreateMethodCallerDelegate<T, TParam>(string methodName)
                {
                    var parameter = Expression.Parameter(typeof(T), "instance");
                    var argument = Expression.Parameter(typeof(TParam), "argument");
                    var methodCall = Expression.Call(parameter, typeof(T).GetMethod(methodName), argument);
                    var lambda = Expression.Lambda<Action<T, TParam>>(methodCall, parameter, argument);
                    return lambda.Compile();
                }

                /// <summary>
                /// 动态调用单参数有返回值的方法
                /// </summary>
                /// <typeparam name="T"></typeparam>
                /// <typeparam name="TParam"></typeparam>
                /// <typeparam name="TResult"></typeparam>
                /// <param name="methodName"></param>
                /// <returns></returns>
                public static Func<T, TParam, TResult> MethodCallerWithResult<T, TParam, TResult>(string methodName)
                {
                    string cacheKey = $"{typeof(T).FullName}.{methodName}.MethodCallerWithResult";
                    return (Func<T, TParam, TResult>)Cache.GetOrAdd(cacheKey, _ => CreateMethodCallerDelegate<T, TParam, TResult>(methodName));
                }

                private static Func<T, TParam, TResult> CreateMethodCallerDelegate<T, TParam, TResult>(string methodName)
                {
                    var parameter = Expression.Parameter(typeof(T), "instance");
                    var argument = Expression.Parameter(typeof(TParam), "argument");
                    var methodCall = Expression.Call(parameter, typeof(T).GetMethod(methodName), argument);
                    var lambda = Expression.Lambda<Func<T, TParam, TResult>>(methodCall, parameter, argument);
                    return lambda.Compile();
                }

                /// <summary>
                /// 动态调用多参无返回值的方法
                /// </summary>
                /// <typeparam name="T"></typeparam>
                /// <param name="methodName"></param>
                /// <param name="parameterTypes"></param>
                /// <returns></returns>
                public static Action<T, object[]> MethodCaller<T>(string methodName, params Type[] parameterTypes)
                {
                    string cacheKey = $"{typeof(T).FullName}.{methodName}.MethodCaller";
                    return (Action<T, object[]>)Cache.GetOrAdd(cacheKey, _ => CreateMethodCallerDelegate<T>(methodName, parameterTypes));
                }

                private static Action<T, object[]> CreateMethodCallerDelegate<T>(string methodName, Type[] parameterTypes)
                {
                    var parameter = Expression.Parameter(typeof(T), "instance");
                    var arguments = parameterTypes.Select((type, index) =>
                        Expression.Parameter(typeof(object), $"arg{index}")
                    ).ToList();

                    var convertedArguments = arguments.Select((arg, index) =>
                        Expression.Convert(arg, parameterTypes[index])
                    ).ToList();

                    var methodInfo = typeof(T).GetMethod(methodName, parameterTypes);

                    if (methodInfo == null)
                    {
                        throw new ArgumentException($"Method '{methodName}' not found in type '{typeof(T).FullName}' with given parameter types.");
                    }

                    var methodCall = Expression.Call(parameter, methodInfo, convertedArguments);
                    var lambda = Expression.Lambda<Action<T, object[]>>(methodCall, new[] { parameter }.Concat(arguments));
                    return lambda.Compile();
                }



                /// <summary>
                /// 动态调用多参有返回值的方法
                /// </summary>
                /// <typeparam name="T"></typeparam>
                /// <typeparam name="TResult"></typeparam>
                /// <param name="methodName"></param>
                /// <param name="parameterTypes"></param>
                /// <returns></returns>
                public static Func<T, object[], TResult> MethodCallerHaveResult<T, TResult>(string methodName, Type[] parameterTypes)
                {
                    string cacheKey = $"{typeof(T).FullName}.{methodName}.MethodCallerHaveResult";
                    return (Func<T, object[], TResult>)Cache.GetOrAdd(cacheKey, _ => CreateMethodCallerDelegate<T, TResult>(methodName, parameterTypes));
                }

                private static Func<T, object[], TResult> CreateMethodCallerDelegate<T, TResult>(string methodName, Type[] parameterTypes)
                {
                    var instanceParam = Expression.Parameter(typeof(T), "instance");
                    var argsParam = Expression.Parameter(typeof(object[]), "args");

                    var convertedArgs = new Expression[parameterTypes.Length];
                    for (int i = 0; i < parameterTypes.Length; i++)
                    {
                        var index = Expression.Constant(i);
                        var argType = parameterTypes[i];
                        var arrayIndex = Expression.ArrayIndex(argsParam, index);
                        var convertedArg = Expression.Convert(arrayIndex, argType);
                        convertedArgs[i] = convertedArg;
                    }

                    var methodInfo = typeof(T).GetMethod(methodName, parameterTypes);

                    if (methodInfo == null)
                    {
                        throw new ArgumentException($"Method '{methodName}' not found in type '{typeof(T).FullName}' with given parameter types.");
                    }

                    var methodCall = Expression.Call(instanceParam, methodInfo, convertedArgs);
                    var lambda = Expression.Lambda<Func<T, object[], TResult>>(methodCall, instanceParam, argsParam);
                    return lambda.Compile();
                }








        */

        #endregion
        #region 暂时不删（已注释）
        /* /// <summary>
         /// 表达式树构建多个参数,有返回值的方法
         /// </summary>
         public static Delegate MethodCallerHaveResult(Type type, string methodName, Type[] parameterTypes)
         {
             string cacheKey = $"{type.FullName}.{methodName}.MethodCallerHaveResult";
             return Cache.GetOrAdd(cacheKey, _ => CreateMethodCallerDelegateHaveResult(type, methodName, parameterTypes));
         }

         private static Delegate CreateMethodCallerDelegateHaveResult(Type type, string methodName, Type[] parameterTypes)
         {
             var instanceParam = Expression.Parameter(typeof(object), "instance");
             var argsParam = Expression.Parameter(typeof(object[]), "args");
             var convertedArgs = parameterTypes.Select((paramType, index) =>
                 Expression.Convert(Expression.ArrayIndex(argsParam, Expression.Constant(index)), paramType)
             ).ToArray();
             var methodCall = Expression.Call(Expression.Convert(instanceParam, type), type.GetMethod(methodName, parameterTypes), convertedArgs);
             var lambda = Expression.Lambda(Expression.Convert(methodCall, typeof(object)), instanceParam, argsParam);
             return lambda.Compile();
         }*/



        /*/// <summary>
        /// 表达式反射 构建 无返回值、无参数 的委托
        /// </summary>
        /// <param name="type"></param>
        /// <param name="methodName"></param>
        /// <param name="parameterTypes"></param>
        /// <returns></returns>
        public static Delegate MethodCaller(Type type, string methodName, Type[] parameterTypes)
        {
            string cacheKey = $"{type.FullName}.{methodName}.{string.Join(",", parameterTypes.Select(t => t.FullName))}.MethodCaller";
            return Cache.GetOrAdd(cacheKey, _ => CreateMethodCallerDelegate(type, methodName, parameterTypes));
        }

        /// <summary>
        /// 表达式反射 构建 无返回值、无参数 的委托
        /// </summary>
        /// <param name="type"></param>
        /// <param name="methodName"></param>
        /// <param name="parameterTypes"></param>
        /// <returns></returns>
        private static Delegate CreateMethodCallerDelegate(Type type, string methodName, Type[] parameterTypes)
        {
            var parameter = Expression.Parameter(typeof(object), "instance");
            var arguments = parameterTypes.Select((paramType, index) => Expression.Parameter(paramType, $"param{index}")).ToArray();
            var methodCall = Expression.Call(Expression.Convert(parameter, type), type.GetMethod(methodName, parameterTypes), arguments);

            var delegateType = Expression.GetActionType(new[] { typeof(object) }.Concat(parameterTypes).ToArray());
            var lambda = Expression.Lambda(delegateType, methodCall, new[] { parameter }.Concat(arguments).ToArray());
            return lambda.Compile();
        }
*/
        /*public static Delegate MethodCallerHaveResult(Type type, string methodName, Type returnType, Type[] parameterTypes)
        {
            string cacheKey = $"{type.FullName}.{methodName}.{string.Join(",", parameterTypes.Select(t => t.FullName))}.MethodCallerHaveResult";
            return Cache.GetOrAdd(cacheKey, _ => CreateMethodCallerDelegateHaveResult(type, methodName, returnType, parameterTypes));
        }

        private static Delegate CreateMethodCallerDelegateHaveResult(Type type, string methodName, Type returnType, Type[] parameterTypes)
        {
            var parameter = Expression.Parameter(typeof(object), "instance");
            var arguments = parameterTypes.Select((paramType, index) => Expression.Parameter(paramType, $"param{index}")).ToArray();
            var methodCall = Expression.Call(Expression.Convert(parameter, type), type.GetMethod(methodName, parameterTypes), arguments);

            var delegateType = Expression.GetFuncType(new[] { typeof(object) }.Concat(parameterTypes).Concat(new[] { typeof(object) }).ToArray());
            var lambda = Expression.Lambda(delegateType, Expression.Convert(methodCall, typeof(object)), new[] { parameter }.Concat(arguments).ToArray());
            return lambda.Compile();
        }

*/

        #endregion
    }
}
