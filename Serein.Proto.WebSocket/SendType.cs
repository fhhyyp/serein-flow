namespace Serein.Proto.WebSocket
{
public partial class SereinWebSocketService
    {
        public enum SendType
        {
            /// <summary>
            /// 不发送数据
            /// </summary>
            None,
            /// <summary>
            /// 发送字符串
            /// </summary>
            String,
            /// <summary>
            /// 发送对象
            /// </summary>
            Object,
            /// <summary>
            /// 异步发送字符串
            /// </summary>
            StringAsync,
            /// <summary>
            /// 异步发送对象
            /// </summary>
            ObjectAsync
        }
    }

}
