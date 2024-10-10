using Newtonsoft.Json.Linq;
using Serein.Library.Attributes;
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


    [AutoRegister]
    public class WebSocketClient
    {
        public WebSocketClient()
        {
            
        }
      

        private ClientWebSocket _client = new ClientWebSocket();


        public async Task ConnectAsync(string uri)
        {
            await _client.ConnectAsync(new Uri(uri), CancellationToken.None);
            await ReceiveAsync();
        }


        public async Task SendAsync(WebSocket webSocket,string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }

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


                        Debug.WriteLine($"Received: {message}");
                    }
                }
                catch (Exception ex)
                {

                    await Console.Out.WriteLineAsync(ex.ToString());
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
