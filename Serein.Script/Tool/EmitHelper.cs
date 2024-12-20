using System.Reflection;
using System.Reflection.Emit;

namespace Serein.Library.Utils
{
    /// <summary>
    /// Emit创建委托工具类
    /// </summary>
    public class EmitHelper
    {

        public class EmitMethodInfo
        {
            public Type DeclaringType {  get; set; }
            /// <summary>
            /// 是异步方法
            /// </summary>
            public bool IsTask { get; set; }
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
            HasResultTask,

            /// <summary>
            /// 普通的方法。如果方法返回void时，将会返回null。
            /// </summary>
            StaticFunc,
            /// <summary>
            /// 无返回值的异步方法
            /// </summary>
            StaticTask,
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
            if (IsTask)
            {
                if (IsTaskGenerics)
                {
                    @delegate = dynamicMethod.CreateDelegate(typeof(Func<object, object[], Task<object>>));
                }
                else
                {
                    @delegate = dynamicMethod.CreateDelegate(typeof(Func<object, object[], Task>));
                }
            }
            else
            {
                @delegate = dynamicMethod.CreateDelegate(typeof(Func<object, object[], object>));

            }
            return new EmitMethodInfo
            {
                DeclaringType = methodInfo.DeclaringType,
                IsTask = IsTask,
                IsStatic = isStatic
            };
        }

        
    }

}
