using System;

namespace Serein.Library
{
    /// <summary>
    /// 节点参数设置
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class NodeParamAttribute : Attribute
    {
        /// <summary>
        /// 显示名称
        /// </summary>
        public string Name;

        /// <summary>
        /// 是否显式设置（此设置对于有入参默认值的参数无效）
        /// </summary>
        public bool IsExplicit;

     
    }

}
