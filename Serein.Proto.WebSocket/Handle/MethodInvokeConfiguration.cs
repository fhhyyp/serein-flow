using Serein.Library;
using static Serein.Proto.WebSocket.SereinWebSocketService;



namespace Serein.Proto.WebSocket.Handle
{
    /// <summary>
    /// socket模块处理数据配置
    /// </summary>

    public class MethodInvokeConfiguration
    {
        /// <summary>
        /// Emit委托
        /// </summary>
        public DelegateDetails? DelegateDetails { get; set; }

        /// <summary>
        /// 所使用的实例
        /// </summary>
        public Func<ISocketHandleModule>? InstanceFactory { get; set; }

        /// <summary>
        /// 是否需要返回
        /// </summary>
        public bool IsReturnValue { get; set; } = true;

        /// <summary>
        /// 是否使用Data整体内容作为入参参数
        /// </summary>
        public bool[] UseData { get; set; } = [];

        /// <summary>
        /// 是否使用Request整体内容作为入参参数
        /// </summary>
        public bool[] UseRequest { get; set; } = [];

        /// <summary>
        /// 是否需要发送消息的委托
        /// </summary>
        public bool[] IsNeedSendDelegate { get; set; } = [];

        /// <summary>
        /// 发送消息的委托类型
        /// </summary>
        public SendType[] SendDelegateType { get; set; } = [];

        /// <summary>
        /// 缓存的发送委托数组
        /// </summary>
        public Delegate?[] CachedSendDelegates ;

        /// <summary>
        /// 是否使用消息ID作为入参参数
        /// </summary>
        public bool[] UseMsgId { get; set; } = [];

        /// <summary>
        /// 是否使用上下文作为参数
        /// </summary>
        public bool[] UseContent { get; set; } = [];

        /// <summary>
        /// 参数名称
        /// </summary>
        public string[] ParameterName { get; set; } = [];

        /// <summary>
        /// 参数类型
        /// </summary>
        public Type[] ParameterType { get; set; } = [];

    }




}
