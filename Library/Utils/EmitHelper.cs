using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{

    /// <summary>
    /// Emit创建委托工具类
    /// </summary>
    public class EmitHelper
    {
        /// <summary>
        /// 动态方法信息
        /// </summary>
        public class EmitMethodInfo
        {
            /// <summary>
            /// 方法声明类型
            /// </summary>
            public Type DeclaringType {  get; set; }

            /// <summary>
            /// 方法类型
            /// </summary>
            public EmitMethodType EmitMethodType   { get; set; }

            /// <summary>
            /// 是异步方法
            /// </summary>
            public bool IsAsync { get; set; }
            /// <summary>
            /// 是静态的
            /// </summary>
            public bool IsStatic { get; set; }


            public bool HasByRefParameters { get; set; }
            public int[] ByRefParameterIndexes { get; set; } = [];
        }

        /// <summary>
        /// 方法类型枚举
        /// </summary>
        public enum EmitMethodType
        {
            /// <summary>
            /// 普通的方法。如果方法返回void时，将会返回null。
            /// </summary>
            Func,
            /// <summary>
            /// 无返回值的异步方法
            /// </summary>
            Task,
            /// <summary>
            /// 有返回值的异步方法
            /// </summary>
            TaskHasResult,
        }

        static Task<object> ConvertTaskResult<T>(Task<T> task) where T : class
        {
            return task.ContinueWith(t => (object)t.Result);
        }

        /// <summary>
        /// 判断一个类型是否为泛型 Task&lt;T&gt; 或 Task，并返回泛型参数类型（如果有的话）
        /// </summary>
        /// <param name="returnType"></param>
        /// <param name="taskResult"></param>
        /// <returns></returns>
#nullable enable
        public static bool IsGenericTask(Type returnType, out Type? taskResult)
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

        /// <summary>
        /// 根据方法信息创建动态调用的委托，返回方法类型，以及传出一个委托
        /// </summary>
        /// <param name="methodInfo"></param>
        /// <param name="delegate"></param>
        /// <returns></returns>
        /// <returns></returns>
        public static EmitMethodInfo CreateMethod(MethodInfo methodInfo, out Delegate @delegate)
        {
            if (methodInfo.DeclaringType == null)
                throw new ArgumentNullException(nameof(methodInfo.DeclaringType));

            bool isStatic = methodInfo.IsStatic;
            bool isTask = IsGenericTask(methodInfo.ReturnType, out var taskResultType);
            bool isTaskGeneric = taskResultType != null;

            Type dynamicReturnType;
            if (!isTask)
                dynamicReturnType = typeof(object);
            else
                dynamicReturnType = isTaskGeneric ? typeof(Task<object>) : typeof(Task);

            var dynamicMethod = new DynamicMethod(
                methodInfo.Name + "_Emit",
                dynamicReturnType,
                new[] { typeof(object), typeof(object[]) },
                restrictedSkipVisibility: true);

            ILGenerator il = dynamicMethod.GetILGenerator();
            var parameters = methodInfo.GetParameters();

            // ==============================
            // 1. 声明 ref / out / in 局部变量
            // ==============================
            LocalBuilder?[] locals = new LocalBuilder?[parameters.Length];
            List<int> byRefIndexes = new();

            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType.IsByRef)
                {
                    var elementType = parameters[i].ParameterType.GetElementType()!;
                    locals[i] = il.DeclareLocal(elementType);
                    byRefIndexes.Add(i);
                }
            }

            // ==============================
            // 2. 初始化 ref / in 参数
            // ==============================
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (!p.ParameterType.IsByRef || p.IsOut)
                    continue;

                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldelem_Ref);

                var elementType = p.ParameterType.GetElementType()!;
                if (elementType.IsValueType)
                    il.Emit(OpCodes.Unbox_Any, elementType);
                else
                    il.Emit(OpCodes.Castclass, elementType);

                il.Emit(OpCodes.Stloc, locals[i]!);
            }

            // ==============================
            // 3. 加载实例
            // ==============================
            if (!isStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, methodInfo.DeclaringType);
            }

            // ==============================
            // 4. 加载参数
            // ==============================
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p.ParameterType.IsByRef)
                {
                    il.Emit(OpCodes.Ldloca, locals[i]!);
                }
                else
                {
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldc_I4, i);
                    il.Emit(OpCodes.Ldelem_Ref);

                    if (p.ParameterType.IsValueType)
                        il.Emit(OpCodes.Unbox_Any, p.ParameterType);
                    else
                        il.Emit(OpCodes.Castclass, p.ParameterType);
                }
            }

            // ==============================
            // 5. 调用方法
            // ==============================
            il.Emit(isStatic ? OpCodes.Call : OpCodes.Callvirt, methodInfo);

            // 如果是泛型Task
            if (isTaskGeneric && methodInfo.ReturnType.IsValueType && taskResultType is not null)
            {
                var convertMethod = typeof(EmitHelper)
                    .GetMethod(nameof(ConvertTaskResult),
                        BindingFlags.Static | BindingFlags.NonPublic)!
                    .MakeGenericMethod(taskResultType);

                il.Emit(OpCodes.Call, convertMethod);
            }

            // ==============================
            // 6. 回写 ref / out 参数
            // ==============================
            for (int i = 0; i < parameters.Length; i++)
            {
                if (!parameters[i].ParameterType.IsByRef)
                    continue;

                var elementType = parameters[i].ParameterType.GetElementType()!;

                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldloc, locals[i]!);

                if (elementType.IsValueType)
                    il.Emit(OpCodes.Box, elementType);

                il.Emit(OpCodes.Stelem_Ref);
            }

            // ==============================
            // 7. 处理返回值
            // ==============================
            if (methodInfo.ReturnType == typeof(void))
            {
                il.Emit(OpCodes.Ldnull);
            }
            else if (!isTask && methodInfo.ReturnType.IsValueType)
            {
                il.Emit(OpCodes.Box, methodInfo.ReturnType);
            }

            

            il.Emit(OpCodes.Ret);

            // ==============================
            // 8. 创建委托
            // ==============================
            EmitMethodType emitType;
            if (!isTask)
            {
                emitType = EmitMethodType.Func;
                @delegate = dynamicMethod.CreateDelegate(
                    typeof(Func<object, object[], object>));
            }
            else if (isTaskGeneric)
            {
                emitType = EmitMethodType.TaskHasResult;
                @delegate = dynamicMethod.CreateDelegate(
                    typeof(Func<object, object[], Task<object>>));
            }
            else
            {
                emitType = EmitMethodType.Task;
                @delegate = dynamicMethod.CreateDelegate(
                    typeof(Func<object, object[], Task>));
            }

            return new EmitMethodInfo
            {
                EmitMethodType = emitType,
                DeclaringType = methodInfo.DeclaringType,
                IsAsync = isTask,
                IsStatic = isStatic,
                HasByRefParameters = byRefIndexes.Count > 0,
                ByRefParameterIndexes = byRefIndexes.ToArray()
            };
        }

        /// <summary>
        /// 构建字段 Getter 委托：Func&lt;object, object&gt;
        /// </summary>
        public static Func<object, object> CreateFieldGetter(FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
                throw new ArgumentNullException(nameof(fieldInfo));
            
            if (fieldInfo.DeclaringType == null)
                throw new ArgumentNullException(nameof(fieldInfo.DeclaringType));


            var method = new DynamicMethod(
                fieldInfo.Name + "_Get",
                typeof(object),
                new[] { typeof(object) },
                fieldInfo.DeclaringType,
                true);

            ILGenerator il = method.GetILGenerator();

            if (!fieldInfo.IsStatic)
            {
                // 加载实例
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, fieldInfo.DeclaringType);
                il.Emit(OpCodes.Ldfld, fieldInfo);
            }
            else
            {
                il.Emit(OpCodes.Ldsfld, fieldInfo);
            }

            // 如果是值类型，装箱
            if (fieldInfo.FieldType.IsValueType)
                il.Emit(OpCodes.Box, fieldInfo.FieldType);

            il.Emit(OpCodes.Ret);

            return (Func<object, object>)method.CreateDelegate(typeof(Func<object, object>));
        }

        /// <summary>
        /// 构建字段 Setter 委托：Action&lt;object, object&gt;
        /// </summary>
        public static Action<object, object> CreateFieldSetter(FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
                throw new ArgumentNullException(nameof(fieldInfo));
            if (fieldInfo.DeclaringType == null)
                throw new ArgumentNullException(nameof(fieldInfo.DeclaringType));
            if (fieldInfo.IsInitOnly)
                throw new InvalidOperationException($"字段 {fieldInfo.Name} 是只读字段，无法设置值。");

            var method = new DynamicMethod(
                fieldInfo.Name + "_Set",
                null,
                new[] { typeof(object), typeof(object) },
                fieldInfo.DeclaringType,
                true);

            ILGenerator il = method.GetILGenerator();

            if (!fieldInfo.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, fieldInfo.DeclaringType);
            }

            // 加载值
            il.Emit(OpCodes.Ldarg_1);
            if (fieldInfo.FieldType.IsValueType)
                il.Emit(OpCodes.Unbox_Any, fieldInfo.FieldType);
            else
                il.Emit(OpCodes.Castclass, fieldInfo.FieldType);

            if (fieldInfo.IsStatic)
                il.Emit(OpCodes.Stsfld, fieldInfo);
            else
                il.Emit(OpCodes.Stfld, fieldInfo);

            il.Emit(OpCodes.Ret);

            return (Action<object, object>)method.CreateDelegate(typeof(Action<object, object>));
        }

        /// <summary>
        /// 构建属性 Getter 委托：Func&lt;object, object&gt;
        /// </summary>
        public static Func<object, object> CreatePropertyGetter(PropertyInfo propertyInfo)
        {
            if (propertyInfo == null)
                throw new ArgumentNullException(nameof(propertyInfo));
            if (propertyInfo.DeclaringType == null)
                throw new ArgumentNullException(nameof(propertyInfo.DeclaringType));
            var getMethod = propertyInfo.GetGetMethod(true);
            if (getMethod == null)
                throw new InvalidOperationException($"属性 {propertyInfo.Name} 没有可用的 Getter。");

            var method = new DynamicMethod(
                propertyInfo.Name + "_Get",
                typeof(object),
                new[] { typeof(object) },
                propertyInfo.DeclaringType,
                true);

            ILGenerator il = method.GetILGenerator();

            if (!getMethod.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, propertyInfo.DeclaringType);
                il.EmitCall(OpCodes.Callvirt, getMethod, null);
            }
            else
            {
                il.EmitCall(OpCodes.Call, getMethod, null);
            }

            // 装箱
            if (propertyInfo.PropertyType.IsValueType)
                il.Emit(OpCodes.Box, propertyInfo.PropertyType);

            il.Emit(OpCodes.Ret);

            return (Func<object, object>)method.CreateDelegate(typeof(Func<object, object>));
        }

        /// <summary>
        /// 构建属性 Setter 委托：Action&lt;object, object&gt;
        /// </summary>
        public static Action<object, object> CreatePropertySetter(PropertyInfo propertyInfo)
        {
            if (propertyInfo == null)
                throw new ArgumentNullException(nameof(propertyInfo));
            if (propertyInfo.DeclaringType == null)
                throw new ArgumentNullException(nameof(propertyInfo.DeclaringType));

            var setMethod = propertyInfo.GetSetMethod(true);
            if (setMethod == null)
                throw new InvalidOperationException($"属性 {propertyInfo.Name} 没有可用的 Setter。");

            var method = new DynamicMethod(
                propertyInfo.Name + "_Set",
                null,
                new[] { typeof(object), typeof(object) },
                propertyInfo.DeclaringType,
                true);

            ILGenerator il = method.GetILGenerator();

            if (!setMethod.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, propertyInfo.DeclaringType);
            }

            // 加载值
            il.Emit(OpCodes.Ldarg_1);
            if (propertyInfo.PropertyType.IsValueType)
                il.Emit(OpCodes.Unbox_Any, propertyInfo.PropertyType);
            else
                il.Emit(OpCodes.Castclass, propertyInfo.PropertyType);

            if (setMethod.IsStatic)
                il.EmitCall(OpCodes.Call, setMethod, null);
            else
                il.EmitCall(OpCodes.Callvirt, setMethod, null);

            il.Emit(OpCodes.Ret);

            return (Action<object, object>)method.CreateDelegate(typeof(Action<object, object>));
        }

        /// <summary>
        /// 构建数组创建委托：Func&lt;int, object[]&gt;
        /// </summary>
        /// <param name="elementType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>

        public static Func<int, object> CreateArrayFactory(Type elementType)
        {
            if (elementType == null) throw new ArgumentNullException(nameof(elementType));

            var arrayType = elementType.MakeArrayType();

            var dm = new DynamicMethod(
                $"NewArray_{elementType.Name}",
                typeof(object), // 返回 object
                new[] { typeof(int) }, // 参数：length
                typeof(EmitHelper).Module,
                true);

            var il = dm.GetILGenerator();


            il.Emit(OpCodes.Ldarg_0);             // length
            il.Emit(OpCodes.Newarr, elementType); // new T[length]
            il.Emit(OpCodes.Ret);                 // 返回 T[]

            return (Func<int, object>)dm.CreateDelegate(typeof(Func<int, object>));
        }



        /// <summary>
        /// 构建集合赋值委托：Action&lt;object, object, object&gt;
        /// </summary>
        /// <param name="collectionType"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static Action<object, object, object> CreateCollectionSetter(Type collectionType)
        {
            DynamicMethod dm = new DynamicMethod(
                "SetCollectionValue",
                null,
                new[] { typeof(object), typeof(object), typeof(object) },
                typeof(EmitHelper).Module,
                true);

            ILGenerator il = dm.GetILGenerator();

            if (collectionType.IsArray)
            {
                // (object array, object index, object value) => ((T[])array)[(int)index] = (T)value;
                var elementType = collectionType.GetElementType()!;
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, collectionType); // 转为真实数组类型

                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Unbox_Any, typeof(int));    // index

                il.Emit(OpCodes.Ldarg_2);
                if (elementType.IsValueType)
                    il.Emit(OpCodes.Unbox_Any, elementType);
                else
                    il.Emit(OpCodes.Castclass, elementType);

                il.Emit(OpCodes.Stelem, elementType); // 设置数组元素
                il.Emit(OpCodes.Ret);
            }
            else
            {
                // 尝试获取 set_Item 方法
                MethodInfo? setItem = collectionType.GetMethod("set_Item", BindingFlags.Instance | BindingFlags.Public);
                if (setItem == null)
                    throw new NotSupportedException($"类型 {collectionType} 不支持 set_Item。");

                var parameters = setItem.GetParameters();
                var indexType = parameters[0].ParameterType;
                var valueType = parameters[1].ParameterType;

                // (object collection, object index, object value) => ((CollectionType)collection)[(IndexType)index] = (ValueType)value;
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, collectionType);

                il.Emit(OpCodes.Ldarg_1);
                if (indexType.IsValueType)
                    il.Emit(OpCodes.Unbox_Any, indexType);
                else
                    il.Emit(OpCodes.Castclass, indexType);

                il.Emit(OpCodes.Ldarg_2);
                if (valueType.IsValueType)
                    il.Emit(OpCodes.Unbox_Any, valueType);
                else
                    il.Emit(OpCodes.Castclass, valueType);

                il.Emit(OpCodes.Callvirt, setItem);
                il.Emit(OpCodes.Ret);
            }

            return (Action<object, object, object>)dm.CreateDelegate(typeof(Action<object, object, object>));
        }


        /// <summary>
        /// 构建集合取值委托：(object collection, object index) => object value
        /// 支持数组、泛型集合、IDictionary 等类型
        /// </summary>
        public static Func<object, object, object> CreateCollectionGetter(Type collectionType, Type? itemType = null)
        {
            DynamicMethod dm = new DynamicMethod(
                "GetCollectionValue",
                typeof(object),
                new[] { typeof(object), typeof(object) },
                typeof(EmitHelper).Module,
                skipVisibility: true);

            ILGenerator il = dm.GetILGenerator();

            // 数组类型处理
            if (collectionType.IsArray)
            {
                var elementType = collectionType.GetElementType()!;
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, collectionType); // 转为真实数组类型
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Unbox_Any, typeof(int));    // 转为int索引
                il.Emit(OpCodes.Ldelem, elementType);       // 取值
                if (elementType.IsValueType)
                    il.Emit(OpCodes.Box, elementType);      // 装箱
                il.Emit(OpCodes.Ret);
            }
            // 非泛型 IDictionary 类型（如 Hashtable、JObject）
            else if (IsGenericDictionaryType(collectionType, out var keyType, out var valueType))
            {
                var getItem = collectionType.GetMethod("get_Item", new[] { keyType });
                if (getItem == null)
                    throw new NotSupportedException($"{collectionType} 未实现 get_Item({keyType})");

                var returnType = getItem.ReturnType;

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, collectionType);

                il.Emit(OpCodes.Ldarg_1);
                if (keyType.IsValueType)
                    il.Emit(OpCodes.Unbox_Any, keyType);
                else
                    il.Emit(OpCodes.Castclass, keyType);

                il.Emit(OpCodes.Callvirt, getItem);

                if (returnType.IsValueType)
                    il.Emit(OpCodes.Box, returnType);

                il.Emit(OpCodes.Ret);
            }

            // 实现 get_Item 方法的类型（如 List<T>, Dictionary<TKey, TValue> 等）
            else
            {
                /*var methodInfos = collectionType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
                MethodInfo? getItem;

                if (methodInfos.Length > 1)
                {
                    getItem = methodInfos.Where(m => m.Name.Equals("get_Item")).Where(m =>
                    {
                        var ps = m.GetParameters().ToArray();
                        if (ps.Length > 1) return false;
                        if (ps[0].ParameterType == typeof(object)) return false;
                        return true;
                    }).FirstOrDefault();
                }
                else
                {
                    //getItem = collectionType.GetMethod("get_Item", BindingFlags.Instance | BindingFlags.Public);
                    getItem = methodInfos.First(m => m.Name.Equals("get_Item"));
                }
                */
                //  GetMethod(name, bindingAttr, binder: null, types, modifiers: null);
                MethodInfo? getItem = collectionType.GetMethod("get_Item", bindingAttr: BindingFlags.Instance | BindingFlags.Public, binder: null, types: [itemType], modifiers: null);
                if (getItem == null)
                    throw new NotSupportedException($"类型 {collectionType} 不支持 get_Item。");

                var indexParamType = getItem.GetParameters()[0].ParameterType;
                var returnType = getItem.ReturnType;

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, collectionType);
                il.Emit(OpCodes.Ldarg_1);

                if (indexParamType.IsValueType)
                    il.Emit(OpCodes.Unbox_Any, indexParamType);
                else
                    il.Emit(OpCodes.Castclass, indexParamType);

                il.Emit(OpCodes.Callvirt, getItem);

                if (returnType.IsValueType)
                    il.Emit(OpCodes.Box, returnType);

                il.Emit(OpCodes.Ret);
            }

            return (Func<object, object, object>)dm.CreateDelegate(typeof(Func<object, object, object>));
        }



        private static bool IsGenericDictionaryType(Type type, out Type keyType, out Type valueType)
        {
            keyType = null!;
            valueType = null!;

            var dictInterface = type
                .GetInterfaces()
                .FirstOrDefault(t =>
                    t.IsGenericType &&
                    t.GetGenericTypeDefinition() == typeof(IDictionary<,>));

            if (dictInterface != null)
            {
                var args = dictInterface.GetGenericArguments();
                keyType = args[0];
                valueType = args[1];
                return true;
            }

            return false;
        }
    }
}


