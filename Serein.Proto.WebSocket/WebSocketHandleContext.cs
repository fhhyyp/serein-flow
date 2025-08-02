using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.Proto.WebSocket.Handle;
using System;
using System.Threading.Tasks;

namespace Serein.Proto.WebSocket
{

    /// <summary>
    /// 消息处理上下文
    /// </summary>
    public class WebSocketHandleContext : IDisposable
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
        /// 释放资源，清理消息上下文
        /// </summary>
        public void Dispose()
        {
            MsgRequest = null;
            MsgTheme = string.Empty; 
            MsgId =  string.Empty; 
            MsgData = null;
            MsgData = null;
        }

        /// <summary>
        /// 标记是否已经处理，如果是，则提前退出
        /// </summary>
        public bool Handle { get => _handle; set{
                if(value)
                {
                    Dispose();
                    _handle = value;
                }
            } }

        private bool _handle = false;


        /// <summary>
        /// 消息本体（IJsonToken）
        /// </summary>
        public IJsonToken? MsgRequest { get; set; }

        /// <summary>
        /// 此次消息请求的主题
        /// </summary>
        public string MsgTheme { get; set; } = string.Empty;

        /// <summary>
        /// 此次消息附带的ID
        /// </summary>
        public string MsgId { get; set; } = string.Empty;

        /// <summary>
        /// 此次消息的数据
        /// </summary>
        public IJsonToken? MsgData { get; set; }

        /// <summary>
        /// 异常外部感知使能
        /// </summary>
        public Func<Exception, Func<object, Task>, Task> OnExceptionTrackingAsync { get; set; }

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
            var ex = new Exception(exMessage);
            Func<object, Task> func = async (data) =>
            {
                var msg = JsonHelper.Serialize(data);
                await _sendAsync.Invoke(msg);

            };
            OnExceptionTrackingAsync.Invoke(ex, func);
        }
        
        /// <summary>
        /// 触发异常追踪
        /// </summary>
        public void TriggerExceptionTracking(Exception ex)
        {
            Func<object, Task> func = async (data) =>
            {
                var msg = JsonHelper.Serialize(data);
                await _sendAsync.Invoke(msg);

            };
            OnExceptionTrackingAsync.Invoke(ex, func);
        }



    }

}
