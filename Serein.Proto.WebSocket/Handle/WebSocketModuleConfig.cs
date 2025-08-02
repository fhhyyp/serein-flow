namespace Serein.Proto.WebSocket.Handle
{
    /// <summary>
    /// 远程环境配置
    /// </summary>
    public class WebSocketModuleConfig
    {
        /// <summary>
        /// 有关消息ID的 Json Key
        /// </summary>
        public string MsgIdJsonKey { get; set; } = string.Empty;

        /// <summary>
        /// 有关消息主题的 Json Key
        /// </summary>
        public string ThemeJsonKey { get; set; } = string.Empty;

        /// <summary>
        /// 有关数据的 Json Key
        /// </summary>
        public string DataJsonKey { get; set; } = string.Empty;

        /// <summary>
        /// 使用怎么样的数据
        /// </summary>
        public bool IsResponseUseReturn { get; set; }
    }

    public class ModuleConfig()
    {
        public string MsgId { get; set; } = string.Empty;
        public string Theme { get; set; } = string.Empty;
        public bool IsResponseUseReturn { get; set; } = false;
    }

}
