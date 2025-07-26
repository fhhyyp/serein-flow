using System;
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

            
        }

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


        /// <summary>
        /// 根据方法信息创建动态调用的委托，返回方法类型，以及传出一个委托
        /// </summary>
        /// <param name="methodInfo"></param>
        /// <param name="delegate"></param>
        /// <returns></returns>
        public static EmitMethodInfo CreateDynamicMethod(MethodInfo methodInfo,out Delegate @delegate)
        {
            EmitMethodInfo emitMethodInfo = new EmitMethodInfo();
            bool IsTask = IsGenericTask(methodInfo.ReturnType, out var taskGenericsType);
            bool IsTaskGenerics = taskGenericsType != null;
            DynamicMethod dynamicMethod;
            
            Type returnType;
            if (!IsTask)
            {
                // 普通方法
                returnType = typeof(object);
               
            }
            else
            {
                // 异步方法
                if (IsTaskGenerics)
                {
                    returnType = typeof(Task<object>);
                }
                else
                {
                    returnType = typeof(Task);
                }
            }



            dynamicMethod = new DynamicMethod(
                       name: methodInfo.Name + "_DynamicEmitMethod",
                       returnType: returnType,
                       parameterTypes: new[] { typeof(object), typeof(object[]) }, // 方法实例、方法入参
                       restrictedSkipVisibility: true // 跳过私有方法访问限制
            );

            var il = dynamicMethod.GetILGenerator();

            // 判断是否为静态方法
            bool isStatic = methodInfo.IsStatic;

            if (isStatic)
            {
                // 如果是静态方法，直接跳过实例（不加载Ldarg_0）
            }
            else
            {
                // 加载实例 (this) 对于非静态方法
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, methodInfo.DeclaringType); // 将 ISocketControlBase 转换为目标类类型
            }
            // 加载方法参数
            var methodParams = methodInfo.GetParameters();
            for (int i = 0; i < methodParams.Length; i++)
            {
                il.Emit(OpCodes.Ldarg_1); // 加载参数数组
                il.Emit(OpCodes.Ldc_I4, i); // 加载当前参数索引
                il.Emit(OpCodes.Ldelem_Ref); // 取出数组元素

                var paramType = methodParams[i].ParameterType;
                if (paramType.IsValueType) // 如果参数是值类型，拆箱
                {
                    il.Emit(OpCodes.Unbox_Any, paramType);
                }
                //else if (paramType.IsGenericParameter) // 如果是泛型参数，直接转换
                //{
                //    il.Emit(OpCodes.Castclass, paramType);
                //}
                else // 如果是引用类型，直接转换
                {
                    il.Emit(OpCodes.Castclass, paramType);
                }

            }

            // 调用方法：静态方法使用 Call，实例方法使用 Callvirt
            if (isStatic)
            {
                il.Emit(OpCodes.Call, methodInfo); // 对于静态方法，使用 Call
            }
            else
            {
                il.Emit(OpCodes.Callvirt, methodInfo); // 对于实例方法，使用 Callvirt
            }

            //// 处理返回值，如果没有返回值，则返回null
            if (methodInfo.ReturnType == typeof(void))
            {
                il.Emit(OpCodes.Ldnull);
            }
            else if (methodInfo.ReturnType.IsValueType)
            {
                il.Emit(OpCodes.Box, methodInfo.ReturnType); // 如果是值类型，将其装箱
            }
            // 处理返回值，如果没有返回值，则返回null
            il.Emit(OpCodes.Ret); // 返回
            EmitMethodType emitMethodType;
            if (IsTask)
            {
                if (IsTaskGenerics)
                {
                    emitMethodType = EmitMethodType.TaskHasResult;
                    @delegate = dynamicMethod.CreateDelegate(typeof(Func<object, object[], Task<object>>));
                }
                else
                {
                    emitMethodType = EmitMethodType.Task;
                    @delegate = dynamicMethod.CreateDelegate(typeof(Func<object, object[], Task>));
                }
            }
            else
            {
                emitMethodType = EmitMethodType.Func;
                @delegate = dynamicMethod.CreateDelegate(typeof(Func<object, object[], object>));

            }
            return new EmitMethodInfo
            {
                EmitMethodType = emitMethodType,
                DeclaringType = methodInfo.DeclaringType,
                IsAsync = IsTask,
                IsStatic = isStatic
            };
        }



        /// <summary>
        /// 创建字段 Getter 委托：Func&lt;object, object&gt;
        /// </summary>
        public static Func<object, object> CreateFieldGetter(FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
                throw new ArgumentNullException(nameof(fieldInfo));

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
        /// 创建字段 Setter 委托：Action&lt;object, object&gt;
        /// </summary>
        public static Action<object, object> CreateFieldSetter(FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
                throw new ArgumentNullException(nameof(fieldInfo));
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
        /// 创建属性 Getter 委托：Func&lt;object, object&gt;
        /// </summary>
        public static Func<object, object> CreatePropertyGetter(PropertyInfo propertyInfo)
        {
            if (propertyInfo == null)
                throw new ArgumentNullException(nameof(propertyInfo));
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
        /// 创建属性 Setter 委托：Action&lt;object, object&gt;
        /// </summary>
        public static Action<object, object> CreatePropertySetter(PropertyInfo propertyInfo)
        {
            if (propertyInfo == null)
                throw new ArgumentNullException(nameof(propertyInfo));
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
        /// 创建集合赋值委托：Action&lt;object, object, object&gt;
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
        /// 创建集合获取委托：Func&lt;object, object, object&gt;
        /// </summary>
        /// <param name="collectionType"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static Func<object, object, object> CreateCollectionGetter(Type collectionType)
        {
            DynamicMethod dm = new DynamicMethod(
                "GetCollectionValue",
                typeof(object),
                new[] { typeof(object), typeof(object) },
                typeof(EmitHelper).Module,
                true);

            ILGenerator il = dm.GetILGenerator();

            if (collectionType.IsArray)
            {
                // (object array, object index) => ((T[])array)[(int)index]
                var elementType = collectionType.GetElementType()!;
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, collectionType); // 转为真实数组类型

                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Unbox_Any, typeof(int));    // index

                il.Emit(OpCodes.Ldelem, elementType);       // 取值

                if (elementType.IsValueType)
                    il.Emit(OpCodes.Box, elementType);      // 装箱

                il.Emit(OpCodes.Ret);
            }
            else
            {
                // 调用 get_Item 方法
                MethodInfo? getItem = collectionType.GetMethod("get_Item", BindingFlags.Instance | BindingFlags.Public);
                if (getItem == null)
                    throw new NotSupportedException($"类型 {collectionType} 不支持 get_Item。");

                var parameters = getItem.GetParameters();
                var indexType = parameters[0].ParameterType;
                var returnType = getItem.ReturnType;

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, collectionType);

                il.Emit(OpCodes.Ldarg_1);
                if (indexType.IsValueType)
                    il.Emit(OpCodes.Unbox_Any, indexType);
                else
                    il.Emit(OpCodes.Castclass, indexType);

                il.Emit(OpCodes.Callvirt, getItem);

                if (returnType.IsValueType)
                    il.Emit(OpCodes.Box, returnType);

                il.Emit(OpCodes.Ret);
            }

            return (Func<object, object, object>)dm.CreateDelegate(typeof(Func<object, object, object>));
        }

    }
}


