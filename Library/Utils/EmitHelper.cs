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
        public static EmitMethodType CreateDynamicMethod(MethodInfo methodInfo,out Delegate @delegate)
        {
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

            // 加载实例 (this)
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, methodInfo.DeclaringType); // 将 ISocketControlBase 转换为目标类类型

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

            // 调用方法
            il.Emit(OpCodes.Callvirt, methodInfo);

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
                    emitMethodType = EmitMethodType.HasResultTask;
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
            return emitMethodType;
        }

        
    }

}
