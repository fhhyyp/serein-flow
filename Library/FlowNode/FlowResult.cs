using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library
{

    /// <summary>
    /// 流程返回值的包装
    /// </summary>
    public sealed class FlowResult
    {
        /// <summary>
        /// 实例化返回值
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="context"></param>
        public static FlowResult OK(string nodeGuid, IFlowContext context, object value)
        {
            FlowResult flowResult = new FlowResult
            {
                SourceNodeGuid = nodeGuid,
                ContextGuid = context.Guid,
                Value = value,
                IsSuccess = true,
            };
            return flowResult;
        }

        /// <summary>
        /// 失败
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="context"></param>
        /// <param name="message"></param>
        public static FlowResult Fail(string nodeGuid, IFlowContext context, string message)
        {
            FlowResult flowResult = new FlowResult
            {
                SourceNodeGuid = nodeGuid,
                ContextGuid = context.Guid,
                Value = Unit.Default,
                IsSuccess = true,
                Message = message,
            };
            return flowResult;
        }

        /// <summary>
        /// 尝试获取值
        /// </summary>
        /// <param name="targetType">目标类型</param>
        /// <param name="value">返回值</param>
        /// <returns>指示是否获取成功</returns>
        /// <exception cref="ArgumentNullException">无法转为对应类型</exception>
        public bool TryGetValue(Type targetType, out object value)
        {
            if (targetType is null)
                throw new ArgumentNullException(nameof(targetType));

            if (targetType.IsInstanceOfType(Value))
            {
                value = Value;
                return true;
            }

            value = Unit.Default;
            return false;
        }

        /// <summary>
        /// 指示是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 执行结果消息（提示异常）
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 来源节点Guid
        /// </summary>
        public string SourceNodeGuid{ get; private set; }

        /// <summary>
        /// 来源上下文Guid
        /// </summary>
        public string ContextGuid { get; private set; } 

        /// <summary>
        /// 数据值
        /// </summary>
        public object Value { get; private set; }

        /// <summary>
        /// 生成时间
        /// </summary>
        public DateTime ResultTime { get; } = DateTime.MinValue;

        /// <summary>
        /// 是否自动回收
        /// </summary>
        public bool IsAutoRecovery { get; set; }

    }
}
