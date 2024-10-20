﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library
{
    /// <summary>
    /// 标识一个类中的某些字段需要生成相应代码
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class AutoPropertyAttribute : Attribute
    {
        /// <summary>
        /// <para>属性路径</para>
        /// <para>CustomNode : 自定义节点</para>
        /// </summary>
        public string ValuePath = string.Empty;
    }

    /// <summary>
    /// 自动生成环境的属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class PropertyInfoAttribute : Attribute
    {
        /// <summary>
        /// 是否通知UI
        /// </summary>
        public bool IsNotification = false;
        /// <summary>
        /// 是否使用Console.WriteLine打印
        /// </summary>
        public bool IsPrint = false;
        /// <summary>
        /// 是否禁止参数进行修改（初始化后不能再通过setter修改）
        /// </summary>
        public bool IsProtection = false;
    }

}
