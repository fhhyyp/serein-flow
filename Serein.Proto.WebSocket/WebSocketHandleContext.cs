using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.Proto.WebSocket.Handle;

namespace Serein.Proto.WebSocket
{

    /// <summary>
    /// 消息处理上下文
    /// </summary>
    public class WebSocketHandleContext
    {
        /// <summary>
        /// 构造函数，传入发送消息的异步方法
        /// </summary>
        /// <param name="sendAsync"></param>
        public WebSocketHandleContext(Func<string, Task> sendAsync)
        {
            _sendAsync = sendAsync;
        }

        /// <summary>
        /// 重置上下文
        /// </summary>
        public void Reset()
        {
            MsgRequest = null;
            MsgData = null;
            ErrorMessage = null;
            Model = null;
            IsError = false;
        }

        /// <summary>
        /// 标记是否已经处理，如果是，则提前退出
        /// </summary>
        public bool Handle { get; set; }

        /// <summary>
        /// WebSocket 模块配置
        /// </summary>
        public ModuleConfig Model { get; set; }

        /// <summary>
        /// 消息本体（IJsonToken）
        /// </summary>
        public IJsonToken? MsgRequest { get; set; }

        /// <summary>
        /// 此次消息的数据
        /// </summary>
        public IJsonToken? MsgData { get; set; }


        /// <summary>
        /// 是否发生错误
        /// </summary>
        public bool IsError { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; set; }


        /// <summary>
        /// 用于在同一个 Web Socket 中调用上下文中, 共享某个对象
        /// </summary>
        private object? _wsTag;
        private object _wsTagLockObj = new object();

        /// <summary>
        /// 设置共享对象，不建议设置非托管对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tag"></param>
        public void SetTag<T>(T tag)
        {
            lock (_wsTagLockObj) 
            {
                _wsTag = tag; 
            }
        }
        /// <summary>
        /// 获取共享对象(将在同一个 Web Socket 调起的上下文中保持一致)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tag"></param>
        public object? GetTag()
        {
            TryGetTag(out object? tag);
            return tag;
        }
        /// <summary>
        /// 获取共享对象(将在同一个 Web Socket 调起的上下文中保持一致)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tag"></param>
        public T? GetTag<T>()
        {
            TryGetTag(out T? tag);
            return tag;
        }
        /// <summary>
        /// 获取共享对象(将在同一个 Web Socket 调起的上下文中保持一致)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tag"></param>
        //public bool TryGetTag<T>([NotNullWhen(true)] out T? tag)
        public bool TryGetTag<T>(out T? tag)
        {
            lock (_wsTagLockObj)
            {
                if (_wsTag is T t)
                {
                    tag = t;
                    return true;
                }
                tag = default;
                return false;
            }
        }

        /// <summary>
        /// 异常外部感知使能
        /// </summary>
        public Action<Exception>? OnExceptionTracking { get; set; }

        /// <summary>
        /// 处理回复消息的函数
        /// </summary>
        public Func<WebSocketHandleContext, object, object> OnReplyMakeData { get; set; }

        /// <summary>
        /// 获取回复内容的回调
        /// </summary>
        public Action<string>? OnReply { get; set; }
        /// <summary>
        /// 发送消息
        /// </summary>

        private Func<string, Task> _sendAsync;


        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public async Task SendAsync(string msg)
        {
            await _sendAsync.Invoke(msg);
        }

        /// <summary>
        /// 触发异常追踪
        /// </summary>
        public void TriggerExceptionTracking(string exMessage)
        {
            var ex = new SereinWebSocketHandleException(exMessage);
            TriggerExceptionTracking(ex); 
        }

        /// <summary>
        /// 触发异常追踪
        /// </summary>
        public void TriggerExceptionTracking(Exception ex)
        {
            IsError = true;
            var msgId = Model.MsgId;
            var theme = Model.Theme;
            ErrorMessage = $"请求[{msgId}]主题[{theme}]异常 ：{ex.Message}"; 
            OnExceptionTracking?.Invoke(ex);
            var error = OnReplyMakeData?.Invoke(this, ex.Message); // 触发回复消息
            _ = SendAsync(JsonHelper.Serialize(error)).ContinueWith((t) =>
            {
                if (t.IsFaulted)
                {
                    Console.WriteLine($"发送错误消息失败: {t.Exception?.Message}");
                }
            }); // 发送错误消息
        }



    }

}
