using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow
{

    public enum DynamicNodeType
    {
        /// <summary>
        /// 初始化
        /// </summary>
        Init,
        /// <summary>
        /// 开始载入
        /// </summary>
        Loading,
        /// <summary>
        /// 结束
        /// </summary>
        Exit,

        /// <summary>
        /// 触发器
        /// </summary>
        Flipflop,
        /// <summary>
        /// 条件节点
        /// </summary>
        Condition,
        /// <summary>
        /// 动作节点
        /// </summary>
        Action,
    }



    /// <summary>
    /// 用来判断一个类是否需要注册并构建实例（单例模式场景使用）
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DynamicFlowAttribute(bool scan = true) : Attribute
    {
        public bool Scan { get; set; } = scan;
    }

    /// <summary>
    /// 标记一个方法是什么类型，加载dll后用来拖拽到画布中
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class MethodDetailAttribute(DynamicNodeType methodDynamicType,
                                       string methodTips = "",
                                       bool scan = true,
                                       string lockName = "") : Attribute
    {
        public bool Scan { get; set; } = scan;
        public string MethodTips { get; } = methodTips;
        public DynamicNodeType MethodDynamicType { get; } = methodDynamicType;
        public string LockName { get; } = lockName;
    }

    /// <summary>
    /// 是否为显式参数
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class ExplicitAttribute : Attribute // where TEnum : Enum
    {
    }

}
