namespace Serein.Library
{
    /// <summary>
    /// 注册顺序
    /// </summary>
    public enum RegisterSequence
    {    /// <summary>
         /// 不自动初始化
         /// </summary>
        Node,
        /// <summary>
        /// 初始化后
        /// </summary>
        FlowInit,
        /// <summary>
        /// 加载后
        /// </summary>
        FlowLoading,
    }

}
