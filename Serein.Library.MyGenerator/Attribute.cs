using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Serein.Library
{

    /// <summary>
    /// 通过枚举来区分该怎么生成代码
    /// </summary>
    public enum NodeValuePath
    {
        /// <summary>
        /// 默认值
        /// </summary>
        None,
        /// <summary>
        /// 节点本身
        /// </summary>
        Node,
        /// <summary>
        /// 节点对应的方法
        /// </summary>
        Method,
        /// <summary>
        /// 节点方法对应的入参
        /// </summary>
        Parameter,
        /// <summary>
        /// 节点的调试设置
        /// </summary>
        DebugSetting,

    }


    /// <summary>
    /// 标识一个类中的某些字段需要生成相应代码
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class NodePropertyAttribute : Attribute
    {
        /// <summary>
        /// <para>属性路径</para>
        /// <para>CustomNode : 自定义节点</para>
        /// </summary>
        public NodeValuePath ValuePath = NodeValuePath.None;
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
        /// 是否禁止参数进行修改（初始化后不能再通过 Setter 修改）
        /// </summary>
        public bool IsProtection = false;
    }

}
