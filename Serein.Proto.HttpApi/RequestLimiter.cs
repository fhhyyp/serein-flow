using System.Collections.Concurrent;
using System.Net;

namespace Serein.Proto.HttpApi
{
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
            interval = TimeSpan.FromSeconds(seconds);
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
