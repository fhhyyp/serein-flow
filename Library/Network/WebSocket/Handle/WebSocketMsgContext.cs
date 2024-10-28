using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Network.WebSocketCommunication.Handle
{

    /// <summary>
    /// 消息处理上下文
    /// </summary>
    public class WebSocketMsgContext : IDisposable
    {
        public WebSocketMsgContext(Func<string, Task> sendAsync)
        {
            this._sendAsync = sendAsync;
        }


        public void Dispose()
        {
            JsonObject = null;
            MsgTheme = null;
            MsgId = null;
            MsgData = null;
            MsgData = null;
            _sendAsync = null;
        }
        /// <summary>
        /// 标记是否已经处理，如果是，则提前退出
        /// </summary>
        public bool Handle { get; set; }

        /// <summary>
        /// 消息本体（JObject）
        /// </summary>
        public JObject JsonObject { get; set; }

        /// <summary>
        /// 此次消息请求的主题
        /// </summary>
        public string MsgTheme { get; set; }

        /// <summary>
        /// 此次消息附带的ID
        /// </summary>
        public string MsgId { get; set; }

        /// <summary>
        /// 此次消息的数据
        /// </summary>
        public JObject MsgData { get; set; }


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
            JObject jsonData;

            if (data is null)
            {
                jsonData = new JObject()
                {
                    [moduleConfig.MsgIdJsonKey] = context.MsgId,
                    [moduleConfig.ThemeJsonKey] = context.MsgTheme,
                };
            }
            else
            {
                JToken dataToken;
                if (data is System.Collections.IEnumerable || data is Array)
                {
                    dataToken = JArray.FromObject(data);
                }
                else
                {
                    dataToken = JObject.FromObject(data);
                }

                jsonData = new JObject()
                {
                    [moduleConfig.MsgIdJsonKey] = context.MsgId,
                    [moduleConfig.ThemeJsonKey] = context.MsgTheme,
                    [moduleConfig.DataJsonKey] = dataToken
                };
            }
            var msg = jsonData.ToString();
            //Console.WriteLine($"[{msgId}] => {theme}");
            await SendAsync(msg);
        }

        
    }

}
