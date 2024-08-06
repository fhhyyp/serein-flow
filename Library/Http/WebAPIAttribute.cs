using Serein.Library.IOC;
using System.Collections.Concurrent;
using System.Net;
using System.Security.AccessControl;

namespace Serein.Library.Http
{

    /// <summary>
    /// HTTP接口监听类
    /// </summary>
    public class WebServer
    {
        private readonly HttpListener listener; // HTTP 监听器
        private Router router; // 路由器
        private readonly RequestLimiter requestLimiter; //接口防刷



        public WebServer()

        {
            listener = new HttpListener();

            requestLimiter = new RequestLimiter(5, 8);

        }

        // 启动服务器
        public WebServer Start(string prefixe, IServiceContainer serviceContainer)
        {
            try
            {
                router = new Router(serviceContainer);
                if (listener.IsListening)
                {
                    return this;
                }

                if (!prefixe.Substring(prefixe.Length - 1, 1).Equals(@"/"))
                {
                    prefixe += @"/";
                }


                listener.Prefixes.Add(prefixe); // 添加监听前缀
                listener.Start(); // 开始监听

                Console.WriteLine($"开始监听:{prefixe}");
                Task.Run(async () =>
                {
                    while (listener.IsListening)
                    {
                        var context = await listener.GetContextAsync(); // 获取请求上下文
                        _ = Task.Run(() => ProcessRequestAsync(context)); // 处理请求)
                    }
                });
                return this;
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 183)
            {
                return this;
            }
        }


        /// <summary>
        /// 处理请求
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private async Task ProcessRequestAsync(HttpListenerContext context)
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

            var isPass = await router.RouteAsync(context); // 路由解析
            if (isPass)
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK; 
                context.Response.Close(); // 关闭响应
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound; 
                context.Response.Close(); // 关闭响应
            }

            //var isPass = requestLimiter.AllowRequest(context.Request);
            //if (isPass)
            //{
            //    // 如果路由没有匹配，返回 404
            //    router.RouteAsync(context); // 路由解析
            //}
            //else
            //{
            //    context.Response.StatusCode = (int)HttpStatusCode.NotFound; // 返回 404 错误
            //    context.Response.Close(); // 关闭响应
            //}

            // var request = context.Request;
            // 获取远程终结点信息
            //var remoteEndPoint = context.Request.RemoteEndPoint;
            //// 获取用户的IP地址和端口
            //IPAddress ipAddress = remoteEndPoint.Address;
            //int port = remoteEndPoint.Port;
            //Console.WriteLine("外部连接：" + ipAddress.ToString() + ":" + port);
        }

        // 停止服务器
        public void Stop()
        {
            if (listener.IsListening)
            {
                listener?.Stop(); // 停止监听
                listener?.Close(); // 关闭监听器
            }
        }

        public void RegisterAutoController<T>()
        {
            //var instance = Activator.CreateInstance(typeof(T));
            router.RegisterAutoController<T>();
        }

        /*public void RegisterRoute<T>(T controllerInstance)
        {
            router.RegisterRoute(controllerInstance);
        }*/
    }
    /// <summary>
    /// 判断访问接口的频次是否正常
    /// </summary>
    public class RequestLimiter(int seconds, int maxRequests)
    {
        private readonly ConcurrentDictionary<string, Queue<DateTime>> requestHistory = new ();
        private readonly TimeSpan interval = TimeSpan.FromSeconds(seconds);
        private readonly int maxRequests = maxRequests;

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
