﻿using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Threading.Tasks;

namespace Serein.Library
{
    public static class FlipflopFunc
    {
        /// <summary>
        /// 传入触发器方法的返回类型，尝试获取Task[Flipflop[]] 中的泛型类型
        /// </summary>
        //public static Type GetFlipflopInnerType(Type type)
        //{
        //    // 检查是否为泛型类型且为 Task<>
        //    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        //    {
        //        // 获取 Task<> 的泛型参数类型，即 Flipflop<>
        //        var innerType = type.GetGenericArguments()[0];

        //        // 检查泛型参数是否为 Flipflop<>
        //        if (innerType.IsGenericType && innerType.GetGenericTypeDefinition() == typeof(FlipflopContext<>))
        //        {
        //            // 获取 Flipflop<> 的泛型参数类型，即 T
        //            var flipflopInnerType = innerType.GetGenericArguments()[0];

        //            // 返回 Flipflop<> 中的具体类型
        //            return flipflopInnerType;
        //        }
        //    }
        //    // 如果不符合条件，返回 null
        //    return null;
        //}

        public static bool IsTaskOfFlipflop(Type type)
        {
            // 检查是否为泛型类型且为 Task<>
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                // 获取 Task<> 的泛型参数类型
                var innerType = type.GetGenericArguments()[0];
                if (innerType.IsGenericType && type.GetGenericTypeDefinition() == typeof(IFlipflopContext<>))
                {
                    var flipflop = type.GetGenericArguments()[0];
                    return true;
                }

                // 判断 innerType 是否继承 IFlipflopContext
                //if (typeof(IFlipflopContext).IsAssignableFrom(innerType))
                //{
                //    return true;
                //}
                //else
                //{
                //    return false;
                //}

                // 检查泛型参数是否为 Flipflop<>
                //if (innerType == typeof(IFlipflopContext))
                //if (innerType.IsGenericType && innerType.GetGenericTypeDefinition() == typeof(FlipflopContext<>))
                //{
                //return true;
                //}
            }

            return false;
        }
    }

    /// <summary>
    /// 触发器上下文
    /// </summary>
    public class FlipflopContext<TResult> : IFlipflopContext<TResult>
    {
        public FlipflopStateType State { get; set; }

        public TriggerDescription Type { get; set; }
        public TResult Value { get; set; }

        public FlipflopContext(FlipflopStateType ffState)
        {
            State = ffState;
        }
        public FlipflopContext(FlipflopStateType ffState, TResult value)
        {
            State = ffState;
            Value = value;
        }


    }

}
