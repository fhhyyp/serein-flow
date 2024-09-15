﻿using Serein.Library.Enums;
using System;

namespace Serein.Library.Attributes
{
    /// <summary>
    /// 表示该属性为自动注入依赖项
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class AutoInjectionAttribute : Attribute
    {
    }

    /// <summary>
    /// 用来判断一个类是否需要注册并构建实例（单例模式场景使用）
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DynamicFlowAttribute : Attribute
    {
        public DynamicFlowAttribute(bool scan = true)
        {
            Scan = scan;
        }
        public bool Scan { get; set; } = true;
    }



    /// <summary>
    /// 建议触发器手动设置返回类型
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class NodeActionAttribute : Attribute
    {
        public NodeActionAttribute(NodeType methodDynamicType,
                                       string methodTips = "",
                                       bool scan = true,
                                       string lockName = "")
        {
            Scan = scan;
            MethodDynamicType = methodDynamicType;
            MethodTips = methodTips;
            LockName = lockName;
        }
        public bool Scan;
        public string MethodTips;
        public NodeType MethodDynamicType;
        public Type ReturnType;
        public string LockName;
    }

}
