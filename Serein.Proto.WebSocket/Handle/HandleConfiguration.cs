using Serein.Library;



namespace Serein.Proto.WebSocket.Handle
{
    /// <summary>
    /// socket模块处理数据配置
    /// </summary>

    public class HandleConfiguration
    {
        /// <summary>
        /// Emit委托
        /// </summary>
        public DelegateDetails DelegateDetails { get; set; }

        /// <summary>
        /// 未捕获的异常跟踪
        /// </summary>
        public Action<Exception, Action<object>> OnExceptionTracking { get; set; }

        /// <summary>
        /// 所使用的实例
        /// </summary>
        public Func<ISocketHandleModule> InstanceFactory { get; set; }

        /// <summary>
        /// 是否需要返回
        /// </summary>
        public bool IsReturnValue { get; set; } = true;

        /// <summary>
        /// 是否要求必须不为null
        /// </summary>
        public bool ArgNotNull { get; set; } = true;

        /// <summary>
        /// 是否使用Data整体内容作为入参参数
        /// </summary>
        public bool[] UseData { get; set; }

        /// <summary>
        /// 是否使用Request整体内容作为入参参数
        /// </summary>
        public bool[] UseRequest { get; set; }

        /// <summary>
        /// 是否使用消息ID作为入参参数
        /// </summary>
        public bool[] UseMsgId { get; set; }

        /// <summary>
        /// 参数名称
        /// </summary>
        public string[] ParameterName { get; set; }

        /// <summary>
        /// 参数类型
        /// </summary>
        public Type[] ParameterType { get; set; }

        /// <summary>
        /// 是否检查变量为空
        /// </summary>
        public bool[] IsCheckArgNotNull { get; set; }

    }




}
