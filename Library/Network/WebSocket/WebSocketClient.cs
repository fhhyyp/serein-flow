using Newtonsoft.Json.Linq;
using Serein.Library.Attributes;
using Serein.Library.Network.WebSocketCommunication.Handle;
using Serein.Library.Web;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    [AutoRegister]
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
        public async Task ConnectAsync(string uri)
        {
            await _client.ConnectAsync(new Uri(uri), CancellationToken.None);
            await ReceiveAsync();
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
          
            while (_client.State == WebSocketState.Open)
            {
                try
                {
                    var result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    }
                    else
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        _ = MsgHandleHelper.HandleMsgAsync(SendAsync, message); // 处理消息
                        Debug.WriteLine($"Received: {message}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Received: {EX.ToString()}");
                }
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
