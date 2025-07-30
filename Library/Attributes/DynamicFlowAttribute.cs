using System;
using System.Text.RegularExpressions;

namespace Serein.Library
{

    /// <summary>
    /// <para>表示该类中存在节点信息</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DynamicFlowAttribute : Attribute
    {
        /// <summary>
        /// 动态流程特性构造函数
        /// </summary>
        /// <param name="name"></param>
        /// <param name="scan"></param>
        public DynamicFlowAttribute(string name = "",bool scan = true)
        {
            Name = name;
            Scan = scan;
        }
        /// <summary>
        /// 补充名称，不影响运行流程
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 如果设置为false，将忽略该类
        /// </summary>
        public bool Scan { get; set; } = true;
    }

}
