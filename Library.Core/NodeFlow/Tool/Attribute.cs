using Serein.Library.Api.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Core.NodeFlow.Tool
{
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


}
