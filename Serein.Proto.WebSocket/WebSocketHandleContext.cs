using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.Proto.WebSocket.Handle;
using System;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
            /*MsgTheme = string.Empty; 
            MsgId =  string.Empty; */
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
        /// 异常外部感知使能
        /// </summary>
        public Action<Exception>? OnExceptionTracking { get; set; }

        /// <summary>
        /// 处理回复消息的函数
        /// </summary>
        public Func<WebSocketHandleContext, object, object> OnReplyMakeData { get; set; }
        public Action<string>? OnReply { get; set; }

        /// <summary>
        /// 是否发生错误
        /// </summary>
        public bool IsError { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; set; }

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
