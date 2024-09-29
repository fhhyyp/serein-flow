using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Serein.Library.Web
{

    /// <summary>
    /// HTTP接口监听类
    /// </summary>
    [AutoRegister]
    public class WebServer
    {
        private readonly IRouter Router;// 路由器
        public WebServer(IRouter router)
        {
            this.Router = router;
        }


        //[AutoInjection]
        //public NodeRunCts nodeRunCts { get; set; }

        private HttpListener listener; // HTTP 监听器
        private RequestLimiter requestLimiter; //接口防刷

        public WebServer()
        {
            listener = new HttpListener();
            requestLimiter = new RequestLimiter(5, 8);
        }

        // 启动服务器
        public WebServer Start(string prefixe)
        {

            if (!prefixe.Substring(prefixe.Length - 1, 1).Equals(@"/"))
            {
                prefixe += @"/";
            }

            listener.Prefixes.Add(prefixe); // 添加监听前缀
            try
            {
                listener.Start(); // 开始监听
                Task.Run(async () =>
                {
                    while (listener.IsListening)
                    {
                        var context = await listener.GetContextAsync(); // 获取请求上下文
                        ProcessRequestAsync(context); // 处理请求
                    }
                });
            }
            catch(Exception ex)
            {
                listener = null;
                Console.WriteLine(ex);
            }

            //_ = Task.Run(async () =>
            //{
            //    while (true)
            //    {
            //        await Task.Delay(100);
            //        if (nodeRunCts.IsCancellationRequested)
            //        {

            //        }

            //        var context = await listener.GetContextAsync(); // 获取请求上下文
            //        ProcessRequestAsync(context); // 处理请求
            //    }
            //});

           
            
            return this;
        }


        /// <summary>
        /// 处理请求
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private async void ProcessRequestAsync(HttpListenerContext context)
        {
            // 添加CORS头部
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
            context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            // 处理OPTIONS预检请求
            if (context.Request.HttpMethod == "OPTIONS")
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.Close();
                return;
            }

            var isPass = requestLimiter.AllowRequest(context.Request);
            if (!isPass)
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound; // 返回 404 错误
            }
            isPass = await Router.ProcessingAsync(context); // 路由解析
            if (isPass)
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK; // 返回 404 错误
               
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound; // 返回 404 错误
            }
            context.Response.Close(); // 关闭响应
            // var request = context.Request;
            // 获取远程终结点信息
            var remoteEndPoint = context.Request.RemoteEndPoint;
            // 获取用户的IP地址和端口
            IPAddress ipAddress = remoteEndPoint.Address;
            int port = remoteEndPoint.Port;
            Console.WriteLine("外部连接：" + ipAddress.ToString() + ":" + port);
        }

        // 停止服务器
        public void Stop()
        {

            try
            {
                listener?.Stop(); // 停止监听
                listener?.Close(); // 关闭监听器
            }
            catch (Exception EX)
            {
                Console.WriteLine(EX);
            }
        }

    }
    /// <summary>
    /// 判断访问接口的频次是否正常
    /// </summary>
    public class RequestLimiter
    {
        private readonly ConcurrentDictionary<string, Queue<DateTime>> requestHistory = new ConcurrentDictionary<string, Queue<DateTime>>();
        private readonly TimeSpan interval;
        private readonly int maxRequests;

        public RequestLimiter(int seconds, int maxRequests)
        {
            this.interval = TimeSpan.FromSeconds(seconds);
            this.maxRequests = maxRequests;
        }

        /// <summary>
        /// 判断访问接口的频次是否正常
        /// </summary>
        /// <returns></returns>
        public bool AllowRequest(HttpListenerRequest request)
        {
            var clientIp = request.RemoteEndPoint.Address.ToString();
            var clientPort = request.RemoteEndPoint.Port;
            var clientKey = clientIp + ":" + clientPort;

            var now = DateTime.Now;

            // 尝试从字典中获取请求队列，不存在则创建新的队列
            var requests = requestHistory.GetOrAdd(clientKey, new Queue<DateTime>());

            lock (requests)
            {
                // 移除超出时间间隔的请求记录
                while (requests.Count > 0 && now - requests.Peek() > interval)
                {
                    requests.Dequeue();
                }

                // 如果请求数超过限制，拒绝请求
                if (requests.Count >= maxRequests)
                {
                    return false;
                }

                // 添加当前请求时间，并允许请求
                requests.Enqueue(now);
            }

            return true;
        }
    }

}
