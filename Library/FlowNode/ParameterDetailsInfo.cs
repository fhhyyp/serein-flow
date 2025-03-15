using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library
{

    /// <summary>
    /// 方法入参描述（远程用）
    /// </summary>
    public class ParameterDetailsInfo
    {
        /// <summary>
        /// 参数索引
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 是否为可变参数
        /// </summary>
        public bool IsParams { get; set; }

        /// <summary>
        /// 方法需要的类型
        /// </summary>
        public string DataTypeFullName { get; set; }

        /// <summary>
        /// 方法入参参数名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 显式类型
        /// </summary>
        public string ExplicitTypeFullName { get; set; }

        /// <summary>
        /// 目前存在三种状态：Select/Bool/Value
        /// <para>Select : 枚举值</para>
        /// <para>Bool   : 布尔类型</para>
        /// <para>Value  ： 除以上类型之外的任意参数</para>
        /// </summary>
        public string InputType { get; set; }

        /// <summary>
        /// 参数选择器
        /// </summary>
        public string[] Items { get; set; }
    }
}
