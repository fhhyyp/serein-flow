using System;

namespace Serein.Library
{
    /// <summary>
    ///  <para>表示该方法将会生成节点，或是加入到流程运行中</para>
    ///  <para>如果是Task类型的返回值，将会自动进行等待</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class NodeActionAttribute : Attribute
    {
        /// <summary>
        /// 节点行为特性构造函数
        /// </summary>
        /// <param name="methodDynamicType"></param>
        /// <param name="methodTips"></param>
        /// <param name="scan"></param>
        /// <param name="lockName"></param>
        public NodeActionAttribute(NodeType methodDynamicType,
                                   string methodTips = "",
                                   bool scan = true,
                                   string lockName = "")
        {
            Scan = scan;
            MethodDynamicType = methodDynamicType;
            AnotherName = methodTips;
            LockName = lockName;
        }
        /// <summary>
        /// 如果设置为false时将不会生成节点信息
        /// </summary>
        public bool Scan;
        /// <summary>
        /// 类似于注释的效果
        /// </summary>
        public string AnotherName;
        /// <summary>
        /// 标记节点行为
        /// </summary>
        public NodeType MethodDynamicType;
        /// <summary>
        /// 暂无意义
        /// </summary>
        public string LockName;
        /// <summary>
        /// 分组名称,暂无意义
        /// </summary>
        public string GroupName;
    }

}
