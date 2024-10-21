using Serein.Library.Network.WebSocketCommunication.Handle;
using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serein.Library.Network.WebSocketCommunication
{

    /// <summary>
    /// WebSocket客户端
    /// </summary>
    public class WebSocketClient
    {
        /// <summary>
        /// WebSocket客户端
        /// </summary>
        public WebSocketClient()
        {

        }

        /// <summary>
        /// 消息处理
        /// </summary>
        public WebSocketMsgHandleHelper MsgHandleHelper { get; } = new WebSocketMsgHandleHelper();

        private ClientWebSocket _client = new ClientWebSocket();

        /// <summary>
        /// 连接到指定WebSocket Server服务
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task<bool> ConnectAsync(string uri)
        {
            try
            {

                await _client.ConnectAsync(new Uri(uri), CancellationToken.None);
                _ = ReceiveAsync();
                return true;
            }
            catch (Exception)
            {

                return false;
            }
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="webSocket"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task SendAsync(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            await _client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        /// <summary>
        /// 开始处理消息
        /// </summary>
        /// <returns></returns>
        private async Task ReceiveAsync()
        {
            var buffer = new byte[1024];
            var receivedMessage = new StringBuilder(); // 用于拼接长消息

            while (_client.State == WebSocketState.Open)
            {
                try
                {
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                        // 根据接收到的字节数解码为部分字符串，并添加到 StringBuilder 中
                        var partialMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        receivedMessage.Append(partialMessage);

                    } while (!result.EndOfMessage); // 判断是否已经收到完整消息

                    // 处理收到的完整消息
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    }
                    else
                    {
                        var completeMessage = receivedMessage.ToString();
                        _ = MsgHandleHelper.HandleMsgAsync(SendAsync, completeMessage); // 处理消息
                        //Debug.WriteLine($"Received: {completeMessage}");
                    }

                    // 清空 StringBuilder 为下一条消息做准备
                    receivedMessage.Clear();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Received: {ex.ToString()}");
                }
            }
        }


        /* #region 消息处理
        private readonly string ThemeField;
        private readonly ConcurrentDictionary<string, HandldConfig> ThemeConfigs = new ConcurrentDictionary<string, HandldConfig>();

        public async Task HandleSocketMsg(string jsonStr)
        {
            JObject json;
            try
            {
                json = JObject.Parse(jsonStr);
            }
            catch (Exception ex)
            {
                await SendAsync(_client, ex.Message);
                return;
            }
            // 获取到消息
            string themeName = json[ThemeField]?.ToString();
            if (!ThemeConfigs.TryGetValue(themeName, out var handldConfig))
            {
                return;
            }

            object dataValue;
            if (string.IsNullOrEmpty(handldConfig.DataField))
            {
                dataValue = json.ToObject(handldConfig.DataType);
            }
            else
            {
                dataValue = json[handldConfig.DataField].ToObject(handldConfig.DataType);
            }
            await handldConfig.Invoke(dataValue, SendAsync);
        }

        public void AddConfig(string themeName, Type dataType, MsgHandler msgHandler)
        {
            if (!ThemeConfigs.TryGetValue(themeName, out var handldConfig))
            {
                handldConfig = new HandldConfig
                {
                    DataField = themeName,
                    DataType = dataType
                };
                ThemeConfigs.TryAdd(themeName, handldConfig);
            }
            handldConfig.HandldAsync += msgHandler;
        }
        public void RemoteConfig(string themeName, MsgHandler msgHandler)
        {
            if (ThemeConfigs.TryGetValue(themeName, out var handldConfig))
            {
                handldConfig.HandldAsync -= msgHandler;
                if (!handldConfig.HasSubscribers)
                {
                    ThemeConfigs.TryRemove(themeName, out _);
                }
            }
        }
        #endregion*/
    }
}
