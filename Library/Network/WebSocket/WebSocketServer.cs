using Newtonsoft.Json.Linq;
using Serein.Library.Attributes;
using Serein.Library.Network.WebSocketCommunication.Handle;
using System;
using System.Collections.Concurrent;
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
        /// 是否已经鉴权
        /// </summary>
        public bool IsAuthorized { get => isAuthorized; } //set => isAuthorized = value;

        /// <summary>
        /// 是否已经鉴权
        /// </summary>
        private bool isAuthorized;

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
        public async Task HandleAuthorized(string message)
        {
            if(!isAuthorized && semaphoreSlim is null) // 需要重新授权
            {
                semaphoreSlim = new SemaphoreSlim(1);
            }
            await semaphoreSlim.WaitAsync(1);
            if(isAuthorized) // 授权通过，无须再次检查授权
            {
                return;
            }
            JObject json = JObject.Parse(message);
            if(json.TryGetValue(TokenKey,out var token))
            {
                // 交给之前定义的授权方法进行判断
                isAuthorized = await InspectionAuthorizedFunc?.Invoke(token);
                if (isAuthorized)
                {
                    // 授权通过，释放资源
                    semaphoreSlim.Release();
                    semaphoreSlim.Dispose();
                    semaphoreSlim = null;
                }
            }
            else
            {
                isAuthorized = false;
            }
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
            this.IsNeedInspectionAuthorized = false;
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
            this.IsNeedInspectionAuthorized = true;
        }

        /// <summary>
        /// 授权
        /// </summary>
        public ConcurrentDictionary<string, WebSocketAuthorizedHelper> AuthorizedClients;
        private readonly string TokenKey;
        private readonly Func<dynamic, Task<bool>> InspectionAuthorizedFunc;
        private bool IsNeedInspectionAuthorized = false;
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
                        if (IsNeedInspectionAuthorized)
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
            if (IsNeedInspectionAuthorized && authorizedHelper is null)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                return;
            }


            Func<string, Task> SendAsync = async (text) =>
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
                    if (IsNeedInspectionAuthorized)
                    {
                        AuthorizedClients.TryRemove(authorizedHelper.AddresPort, out var _);
                    }
                }
                else
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count); // 序列为文本
                    if(!IsNeedInspectionAuthorized)
                    {
                        // 无须授权
                        _ = MsgHandleHelper.HandleMsgAsync(SendAsync, message); // 处理消息
                        
                    }
                    else
                    {
                        // 需要授权
                        if (!authorizedHelper.IsAuthorized)
                        {
                            // 该连接尚未验证授权，尝试检测授权
                            _ = SendAsync("正在授权");
                            await authorizedHelper.HandleAuthorized(message);
                        }


                        if (authorizedHelper.IsAuthorized)
                        {
                            // 该连接通过了验证
                            _ = SendAsync("授权成功");
                            _ = MsgHandleHelper.HandleMsgAsync(SendAsync, message); // 处理消息
                        }
                        else
                        {
                            _ = SendAsync("授权失败");
                        }
                        
                    }

                    
                }
            }
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="webSocket"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static async Task SendAsync(WebSocket webSocket, string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }

    }
}
