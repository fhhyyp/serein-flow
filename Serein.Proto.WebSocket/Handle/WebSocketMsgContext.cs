using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Threading.Tasks;

namespace Serein.Proto.WebSocket.Handle
{

    /// <summary>
    /// 消息处理上下文
    /// </summary>
    public class WebSocketMsgContext : IDisposable
    {
        public WebSocketMsgContext(Func<string, Task> sendAsync)
        {
            _sendAsync = sendAsync;
        }

        public void Dispose()
        {
            MsgRequest = null;
            MsgTheme = string.Empty; 
            MsgId =  string.Empty; 
            MsgData = null;
            MsgData = null;
            _sendAsync = null;
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

        public bool _handle = false;

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


        private Func<string, Task>? _sendAsync;

        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public async Task SendAsync(string msg)
        {
            if (_sendAsync is null) return;
            await _sendAsync.Invoke(msg);
        }


        /// <summary>
        /// 返回消息
        /// </summary>
        /// <param name="moduleConfig"></param>
        /// <param name="context"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task RepliedAsync(WebSocketHandleModuleConfig moduleConfig,
                                    WebSocketMsgContext context,
                                    object data)
        {
            if (moduleConfig.IsResponseUseReturn)
            {
                var responseContent = JsonHelper.Serialize(data);
                await SendAsync(responseContent);
            }
            else
            {

                IJsonToken jsonData;

                jsonData = JsonHelper.Object(obj =>
                {
                    obj[moduleConfig.MsgIdJsonKey] = context.MsgId;
                    obj[moduleConfig.ThemeJsonKey] = context.MsgTheme;
                    obj[moduleConfig.DataJsonKey] = data is null ? null
                                                                 : JsonHelper.FromObject(data);
                });

                var msg = jsonData.ToString();
                //Console.WriteLine($"[{msgId}] => {theme}");
                await SendAsync(msg);
            }

        }

        
    }

}
