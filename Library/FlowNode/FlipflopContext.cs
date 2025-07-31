using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Serein.Library
{
    /// <summary>
    /// FlipflopFunc 类提供了与 Flipflop 相关的功能方法。
    /// </summary>
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
        /// <summary>
        /// 触发器完成的状态（根据业务场景手动设置）
        /// </summary>
        public FlipflopStateType State { get; set; }
        /// <summary>
        /// 触发类型
        /// </summary>
        public TriggerDescription Type { get; set; }
        /// <summary>
        /// 触发时传递的数据
        /// </summary>
        public TResult Value { get; set; }

        public FlipflopContext()
        {
            
        }

        /// <summary>
        /// 成功触发器上下文，表示触发器执行成功并返回结果
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="result"></param>
        /// <returns></returns>
        public static FlipflopContext<TResult> Ok<TResult>(TResult result)
        {
            return new FlipflopContext<TResult>()
            {
                State = FlipflopStateType.Succeed,
                Type = TriggerDescription.External,
                Value = result,
            };
        }

        /// <summary>
        /// 表示触发器执行失败
        /// </summary>
        /// <returns></returns>
        public static FlipflopContext<TResult> Fail()
        {
            return new FlipflopContext<TResult>()
            {
                State = FlipflopStateType.Fail,
                Type = TriggerDescription.External,
                Value = default,
            };
        }

        /// <summary>
        /// 表示触发器执行过程中发生了错误
        /// </summary>
        /// <returns></returns>
        public static FlipflopContext<TResult> Error()
        {
            return new FlipflopContext<TResult>()
            {
                State = FlipflopStateType.Error,
                Type = TriggerDescription.External,
                Value = default,
            };
        }

        /// <summary>
        /// 取消触发器上下文，表示触发器被外部取消
        /// </summary>
        /// <returns></returns>
        public static FlipflopContext<TResult> Cancel()
        {
            return new FlipflopContext<TResult>()
            {
                State = FlipflopStateType.Cancel,
                Type = TriggerDescription.External,
                Value = default,
            };
        }

        /// <summary>
        /// 超时触发器上下文，表示触发器在指定时间内未完成
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public static FlipflopContext<TResult> Overtime(FlipflopStateType state = FlipflopStateType.Fail)
        {
            return new FlipflopContext<TResult>()
            {
                State = state,
                Type = TriggerDescription.Overtime,
                Value = default,
            };
        }




    }

}
