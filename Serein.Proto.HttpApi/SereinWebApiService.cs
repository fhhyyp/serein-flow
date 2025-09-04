using Serein.Library;
using Serein.Library.Utils;
using System.Net;
using System.Text;

namespace Serein.Proto.HttpApi
{
 
    /// <summary>
    /// 对于 HttpListenerContext 的拓展服务
    /// </summary>
    public class SereinWebApiService
    {
        private readonly IPathRouter _pathRouter;
        //private RequestLimiter? requestLimiter;

        /// <summary>
        /// 初始化处理器
        /// </summary>
        /// <param name="pathRouter"></param>
        public SereinWebApiService(IPathRouter pathRouter)
        {
            _pathRouter = pathRouter;
        }

        /// <summary>
        /// 添加处理模块
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void AddHandleModule<T>() where T : ControllerBase
        {
            _pathRouter.AddHandle(typeof(T));
        }

        /// <summary>
        /// 添加处理模块
        /// </summary>
        /// <param name="type"></param>
        public void AddHandleModule(Type type)
        {
            _pathRouter.AddHandle(type);
        }

        /// <summary>
        /// 获取路由信息
        /// </summary>
        /// <returns></returns>
        public List<RouterInfo> GetRouterInfos()
        {
            return _pathRouter.GetRouterInfos();
        }

        private Func<InvokeResult, (object, HttpStatusCode)> OnBeforeReplying;

        /// <summary>
        /// 设置回复前的处理函数
        /// </summary>
        /// <param name="func"></param>
        public void SetOnBeforeReplying(Func<InvokeResult, (object, HttpStatusCode)> func)
        {
            OnBeforeReplying = func;
        }

        /// <summary>
        /// 处理请求
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task HandleAsync(HttpListenerContext context)
        {
            // 获取远程终结点信息
            var remoteEndPoint = context.Request.RemoteEndPoint;
            // 获取用户的IP地址和端口
            IPAddress ipAddress = remoteEndPoint.Address;
            int port = remoteEndPoint.Port;
            SereinEnv.WriteLine(InfoType.INFO, "外部连接：" + ipAddress.ToString() + ":" + port);

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
            /*if (requestLimiter is not null)
            {
                if (!requestLimiter.AllowRequest(context.Request))
                {
                    SereinEnv.WriteLine(InfoType.INFO, "接口超时");
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound; // 返回 404 错误
                }
            }*/

            var invokeResult = await _pathRouter.HandleAsync(context); // 路由解析
            (var result, var code) = (OnBeforeReplying is not null) switch
            {
                true => OnBeforeReplying.Invoke(invokeResult),
                false => (invokeResult.Data, HttpStatusCode.OK),
            };
            var json = (result is not null) switch
            {
                true => JsonHelper.Serialize(result),
                false => string.Empty
            };
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            var response = context.Response; // 获取响应对象
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.StatusCode = (int)code; // 返回 200 成功
            context.Response.Close(); // 关闭响应



        }
    }
}
