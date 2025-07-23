using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library
{
    /// <summary>
    /// 表示空数据
    /// </summary>
    /*public readonly struct Unit : IEquatable<Unit>
    {
        public static readonly Unit Default = default;
        public bool Equals(Unit _) => true;
        public override bool Equals(object obj) => obj is Unit;
        public override int GetHashCode() => 0;
    }*/


    /// <summary>
    /// 流程返回值的包装
    /// </summary>
    public class FlowResult
    {
        /// <summary>
        /// 实例化返回值
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="context"></param>
        public FlowResult(string nodeGuid, IFlowContext context, object value)
        {
            this.SourceNodeGuid = nodeGuid;
            this.ContextGuid = context.Guid;
            this.Value = value;
        }

        /// <summary>
        /// 空返回值
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="context"></param>
        public FlowResult(string nodeGuid, IFlowContext context)
        {
            this.SourceNodeGuid = nodeGuid;
            this.ContextGuid = context.Guid;
            this.Value  = Unit.Default;
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
        /// 来源节点Guid
        /// </summary>
        public string SourceNodeGuid{ get; }
        /// <summary>
        /// 来源上下文Guid
        /// </summary>
        public string ContextGuid { get; } 
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
