using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serein.Library.Network.WebSocketCommunication
{
    public class WebSocketServer
    {
       public Func<string,Action> OnReceiveMsg;

        public async Task StartAsync(string url)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();

            while (true)
            {
                var context = await listener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    var webSocketContext = await context.AcceptWebSocketAsync(null); //新连接
                    _ = HandleWebSocketAsync(webSocketContext.WebSocket); // 处理消息
                }
            }
        }

        private async Task HandleWebSocketAsync(WebSocket webSocket)
        {
            var buffer = new byte[1024];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
                else
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine($"Received: {message}");
                    var action = OnReceiveMsg.Invoke(message);
                    action?.Invoke();

                    // 回显消息（可选）
                    //ar echoMessage = Encoding.UTF8.GetBytes(message);
                    //await webSocket.SendAsync(new ArraySegment<byte>(echoMessage, 0, echoMessage.Length), result.MessageType, result.EndOfMessage, CancellationToken.None);

                }
            }
        }
    }
}
