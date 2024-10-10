using Newtonsoft.Json.Linq;
using Serein.Library.Attributes;
using Serein.Library.Network.WebSocketCommunication.Handle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serein.Library.Network.WebSocketCommunication
{
    [AutoRegister]
    public class WebSocketServer
    {
        public WebSocketMsgHandleHelper MsgHandleHelper { get; } = new WebSocketMsgHandleHelper();

        private HttpListener listener;
        public async Task StartAsync(string url)
        {
            listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();

            while (true)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    string clientPoint = context.Request.RemoteEndPoint?.ToString();

                    await Console.Out.WriteLineAsync($"新的连接加入：{clientPoint}");
                    if (context.Request.IsWebSocketRequest)
                    {
                        var webSocketContext = await context.AcceptWebSocketAsync(null); //新连接

                        _ = HandleWebSocketAsync(webSocketContext.WebSocket); // 处理消息
                    }
                }
                catch (Exception ex)
                {
                    await Console.Out.WriteLineAsync(ex.Message);
                    break;
                }
            }
        }

        public void Stop()
        {
            listener?.Stop();
        }

        private async Task HandleWebSocketAsync(WebSocket webSocket)
        {
            Func<string,Task> SendAsync = async (text) =>
            {
                await WebSocketServer.SendAsync(webSocket, text);
            };
            var buffer = new byte[1024];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    SendAsync = null;
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
                else
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    
                    _ = MsgHandleHelper.HandleMsgAsync(SendAsync, message);
                    
                    //foreach (var item in HandldHelpers)
                    //{
                    //    await item.HandleSocketMsg(webSocket, message);
                    //}
                    //Console.WriteLine($"Received: {message}");
                    //var echoMessage = Encoding.UTF8.GetBytes(message);
                    //await webSocket.SendAsync(new ArraySegment<byte>(echoMessage, 0, echoMessage.Length), result.MessageType, result.EndOfMessage, CancellationToken.None);

                }
            }
        }

        public static async Task SendAsync(WebSocket webSocket, string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }

    }
}
