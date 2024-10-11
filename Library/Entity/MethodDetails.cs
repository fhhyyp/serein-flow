using Serein.Library.Api;
using Serein.Library.Enums;
using System;
using System.Linq;

namespace Serein.Library.Entity
{


    /// <summary>
    /// 每个节点有独自的MethodDetails实例
    /// </summary>
    public class MethodDetails 
    {
        /// <summary>
        /// 从DLL拖动出来时拷贝新的实例
        /// </summary>
        /// <returns></returns>
        public MethodDetails Clone()
        {
            return new MethodDetails
            {
                ActingInstance = ActingInstance,
                ActingInstanceType = ActingInstanceType,
                MethodDynamicType = MethodDynamicType,
                MethodTips = MethodTips,
                ReturnType = ReturnType,
                MethodName = MethodName,
                MethodLockName = MethodLockName,
                IsProtectionParameter = IsProtectionParameter,
                ExplicitDatas = ExplicitDatas?.Select(it => it.Clone()).ToArray(),
            };
        }

        /// <summary>
        /// 是否保护参数（仅视觉效果参数，不影响运行实现）
        /// </summary>
        public bool IsProtectionParameter { get; set; } = false;

        /// <summary>
        /// 作用实例的类型（多个相同的节点将拥有相同的类型）
        /// </summary>
        public Type ActingInstanceType { get; set; }

        /// <summary>
        /// 作用实例（多个相同的节点将会共享同一个实例）
        /// </summary>
        public object ActingInstance { get; set; }

        /// <summary>
        /// 方法名称
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// 节点类型
        /// </summary>
        public NodeType MethodDynamicType { get; set; }

        /// <summary>
        /// 锁名称（暂未实现）
        /// </summary>
        public string MethodLockName { get; set; }


        /// <summary>
        /// 方法说明
        /// </summary>
        public string MethodTips { get; set; }


        /// <summary>
        /// 参数内容
        /// </summary>

        public ExplicitData[] ExplicitDatas { get; set; }

        /// <summary>
        /// 出参类型
        /// </summary>

        public Type ReturnType { get; set; }


    }


}
