using Serein.Library.Utils;
using Serein.Proto.WebSocket.Handle;
using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetWebSocket = System.Net.WebSockets.WebSocket;

namespace Serein.Proto.WebSocket
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
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task SendAsync(string message)
        {
            await SocketExtension.SendAsync(_client, message); // 回复客户端
        }

        /// <summary>
        /// 开始处理消息
        /// </summary>
        /// <returns></returns>
        private async Task ReceiveAsync()
        {

            var msgQueueUtil = new WebSocketMessageTransmissionTool();
            _ = Task.Run(async () =>
            {
                await HandleMsgAsync(_client, msgQueueUtil);
            });

           
            var receivedMessage = new StringBuilder(); // 用于拼接长消息

            while (_client.State == WebSocketState.Open)
            {
                try
                {
                    var buffer = new byte[1024];
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                        // 根据接收到的字节数解码为部分字符串，并添加到 StringBuilder 中
                        var partialMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        receivedMessage.Append(partialMessage);

                    } while (!result.EndOfMessage); // 判断是否已经收到完整消息
                    var message = receivedMessage.ToString();
                    await msgQueueUtil.WriteMsgAsync(message);
                    receivedMessage.Clear();  // 清空 StringBuilder 为下一条消息做准备
                    // 处理收到的完整消息
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    }
                    
                    
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Received: {ex.ToString()}");
                }
            }
        }

        /// <summary>
        /// 处理消息
        /// </summary>
        /// <param name="webSocket"></param>
        /// <param name="msgQueueUtil"></param>
        /// <returns></returns>
        public async Task HandleMsgAsync(NetWebSocket webSocket, WebSocketMessageTransmissionTool msgQueueUtil)
        {
            async Task sendasync(string text)
            {
                await SocketExtension.SendAsync(webSocket, text); // 回复客户端，处理方法中入参如果需要发送消息委托，则将该回调方法作为委托参数传入
            }
            while (true)
            {
                var message = await msgQueueUtil.WaitMsgAsync();  // 有消息时通知
                var context = new WebSocketMsgContext(sendasync);
                context.MsgRequest = JsonHelper.Parse(message);
                MsgHandleHelper.HandleAsync(context); // 处理消息
            }
        }
    }
}
