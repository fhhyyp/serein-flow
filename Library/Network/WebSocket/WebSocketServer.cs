using Newtonsoft.Json.Linq;
using Serein.Library.Network.WebSocketCommunication.Handle;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serein.Library.Network.WebSocketCommunication
{
    /// <summary>
    /// WebSocket JSON 消息授权管理
    /// </summary>
    public class WebSocketAuthorizedHelper
    {
        /// <summary>
        /// WebSocket JSON 消息授权管理
        /// </summary>
        public WebSocketAuthorizedHelper(string addresPort,string token, Func<dynamic, Task<bool>> inspectionAuthorizedFunc)
        {
            this.AddresPort = addresPort;
            this.TokenKey = token;
            this.InspectionAuthorizedFunc = inspectionAuthorizedFunc;
        }

        /// <summary>
        /// 客户端地址
        /// </summary>
        public string AddresPort { get; }


        /// <summary>
        /// 授权字段
        /// </summary>
        private readonly string TokenKey;

        /// <summary>
        /// 处理消息授权事件
        /// </summary>
        private readonly Func<dynamic, Task<bool>> InspectionAuthorizedFunc;

        private SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1);

        /// <summary>
        /// 处理消息授权
        /// </summary>
        /// <param name="message"></param>
        public async Task<bool> HandleAuthorized(string message)
        {
            await semaphoreSlim.WaitAsync(1);
            bool isAuthorized = false;
            JObject json = JObject.Parse(message);
            if(json.TryGetValue(TokenKey,out var token))
            {
                // 交给之前定义的授权方法进行判断
                isAuthorized = await InspectionAuthorizedFunc?.Invoke(token);
            }
            else
            {
                isAuthorized = false;
            }
            return isAuthorized;
        }

    }


    /// <summary>
    /// WebSocket服务类
    /// </summary>
    [AutoRegister]
    public class WebSocketServer
    {
        /// <summary>
        /// 消息处理
        /// </summary>
        public WebSocketMsgHandleHelper MsgHandleHelper { get; } = new WebSocketMsgHandleHelper();

        private HttpListener listener;

        /// <summary>
        /// 创建无须授权验证的WebSocket服务端
        /// </summary>
        public WebSocketServer()
        {
            this.AuthorizedClients = new ConcurrentDictionary<string, WebSocketAuthorizedHelper>();
            this.InspectionAuthorizedFunc = (tokenObj) => Task.FromResult(true);
            this.IsCheckToken = false;
        }

        /// <summary>
        /// 创建需要授权验证的WebSocket服务端
        /// </summary>
        /// <param name="tokenKey">token 字段</param>
        /// <param name="inspectionAuthorizedFunc">验证token的方法</param>
        public WebSocketServer(string tokenKey, Func<dynamic, Task<bool>> inspectionAuthorizedFunc)
        {
            this.TokenKey = tokenKey;
            this.AuthorizedClients = new ConcurrentDictionary<string, WebSocketAuthorizedHelper>();
            this.InspectionAuthorizedFunc = inspectionAuthorizedFunc;
            this.IsCheckToken = true;
        }

        /// <summary>
        /// 授权
        /// </summary>
        public ConcurrentDictionary<string, WebSocketAuthorizedHelper> AuthorizedClients;
        private readonly string TokenKey;
        private readonly Func<dynamic, Task<bool>> InspectionAuthorizedFunc;
        private bool IsCheckToken = false;
        /// <summary>
        /// 进行监听服务
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public async Task StartAsync(string url)
        {
            listener = new HttpListener();
            listener.Prefixes.Add(url);
            await Console.Out.WriteLineAsync($"WebSocket消息处理已启动[{url}]");
            try
            {
                listener.Start();
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync(ex.Message);
                return;
            }

            while (true)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    string clientPoint = context.Request.RemoteEndPoint?.ToString();

                    await Console.Out.WriteLineAsync($"新的连接加入：{clientPoint}");

                    if (context.Request.IsWebSocketRequest)
                    {
                        WebSocketAuthorizedHelper authorizedHelper = null;
                        if (IsCheckToken)
                        {
                            if (AuthorizedClients.TryAdd(clientPoint, new WebSocketAuthorizedHelper(clientPoint, TokenKey, InspectionAuthorizedFunc)))
                            {
                                AuthorizedClients.TryGetValue(clientPoint, out authorizedHelper);
                            }
                        }

                        var webSocketContext = await context.AcceptWebSocketAsync(null); //新连接
                         _ = HandleWebSocketAsync(webSocketContext.WebSocket, authorizedHelper); // 处理消息
                    }
                }
                catch (Exception ex)
                {
                    await Console.Out.WriteLineAsync(ex.Message);
                    break;
                }
            }
        }

        /// <summary>
        /// 停止监听服务
        /// </summary>
        public void Stop()
        {
            listener?.Stop();
        }

        private async Task HandleWebSocketAsync(WebSocket webSocket, WebSocketAuthorizedHelper authorizedHelper)
        {
            // 需要授权，却没有成功创建授权类，关闭连接
            if (IsCheckToken && authorizedHelper is null)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                return;
            }

            var msgQueueUtil = new MsgHandleUtil();
            _ = Task.Run(async () =>
            {
                await HandleMsgAsync(webSocket,msgQueueUtil, authorizedHelper);
            });

            //Func<string, Task> SendAsync = async (text) =>
            //{
            //    await WebSocketServer.SendAsync(webSocket, text);
            //};

            var receivedMessage = new StringBuilder(); // 用于拼接长消息

            while ( webSocket.State == WebSocketState.Open)
            {
               
                try
                {
                    WebSocketReceiveResult result;
                    var buffer = new byte[1024];
                    do
                    {
                        result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                            if (IsCheckToken)
                            {
                                AuthorizedClients.TryRemove(authorizedHelper.AddresPort, out var _);
                            }
                        }
                        // 将接收到的部分消息解码并拼接
                        var partialMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        receivedMessage.Append(partialMessage);

                    } while (!result.EndOfMessage); // 循环直到接收到完整的消息
                    // 完整消息已经接收到，准备处理
                    var message = receivedMessage.ToString(); // 获取消息文本
                    receivedMessage.Clear();  // 清空 StringBuilder 为下一条消息做准备
                    await msgQueueUtil.WriteMsgAsync(message);  // 处理消息
                }
                catch (Exception ex)
                {
                    // 处理异常
                    Debug.WriteLine($"Error: {ex.ToString()}");
                }
            }
        }


        public async Task HandleMsgAsync(WebSocket webSocket,
                                         MsgHandleUtil msgQueueUtil, 
                                         WebSocketAuthorizedHelper authorizedHelper)
        {
            async Task sendasync(string text)
            {
                await SocketExtension.SendAsync(webSocket, text); // 回复客户端，处理方法中入参如果需要发送消息委托，则将该回调方法作为委托参数传入
            }
            while (true)
            {
                var message = await msgQueueUtil.WaitMsgAsync();  // 有消息时通知
                if (IsCheckToken)
                {
                    var authorizedResult = await authorizedHelper.HandleAuthorized(message); // 尝试检测授权
                    if (!authorizedResult) // 授权失败
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        if (IsCheckToken)
                        {
                            AuthorizedClients.TryRemove(authorizedHelper.AddresPort, out var _);
                        }
                        return;
                    }
                }
                var context = new WebSocketMsgContext(sendasync);
                context.JsonObject = JObject.Parse(message);
                MsgHandleHelper.Handle(context); // 处理消息

                //using (var context = new WebSocketMsgContext(sendasync))
                //{
                //    context.JsonObject = JObject.Parse(message);
                //    await MsgHandleHelper.Handle(context); // 处理消息
                //}
                //_ = Task.Run(() => {
                

                //});


            }
            
        }

    }



}
