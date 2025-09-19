using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Serein.Proto.HttpApi.PathRouter;
using Enum = System.Enum;
using Type = System.Type;

namespace Serein.Proto.HttpApi
{
   
    /// <summary>
    /// 路由注册与解析
    /// </summary>
    internal class PathRouter 
    {
        /// <summary>
        /// IOC容器
        /// </summary>
        private readonly ISereinIOC SereinIOC; 

        private long _requestId = 0;

        /// <summary>
        /// 控制器实例对象的类型，每次调用都会重新实例化，[Url - ControllerType]
        /// </summary>
        private readonly ConcurrentDictionary<string, Type> _controllerTypes = new ConcurrentDictionary<string, Type>();

        /// <summary>
        /// 用于存储路由信息，[GET|POST - [Url - ApiHandleConfig]]
        /// </summary>
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ApiHandleConfig>> HandleModels = new ConcurrentDictionary<string, ConcurrentDictionary<string, ApiHandleConfig>>();

        /// <summary>
        /// 请求时的处理函数，传入API类型、URL、Body
        /// </summary>
        internal Func<ApiRequestInfo, bool>? OnBeforRequest;


        public PathRouter(ISereinIOC SereinIOC)
        {
            this.SereinIOC = SereinIOC;
            foreach (ApiType method in Enum.GetValues(typeof(ApiType))) // 遍历 HTTP 枚举类型的所有值
            {
                HandleModels.TryAdd(method.ToString(), new ConcurrentDictionary<string, ApiHandleConfig>()); // 初始化每种 HTTP 方法对应的路由字典
            }
        }

        /// <summary>
        /// 获取路由信息
        /// </summary>
        /// <returns></returns>
        public List<RouterInfo> GetRouterInfos()
        {
            var t = HandleModels.Select(kvp => kvp.Value.Select(kvp2 => kvp2.Value.RouterInfo));
            return t.SelectMany(x => x).ToList();
        }

        /// <summary>
        /// 添加处理模块
        /// </summary>
        /// <param name="controllerType"></param>
        public void AddHandle(Type controllerType)
        {
            if (!controllerType.IsClass || controllerType.IsAbstract) return; // 如果不是类或者是抽象类，则直接返回

            var autoHostingAttribute = controllerType.GetCustomAttribute<WebApiControllerAttribute>();
            if(autoHostingAttribute is null)
            {
                SereinEnv.WriteLine(InfoType.WARN, $"类型 \"{controllerType}\" 需要实现 \"WebApiControllerAttribute\" 才可以作为路由控制器");
                return;
            }
            var methods = controllerType.GetMethods().Where(m => m.GetCustomAttribute<WebApiAttribute>() != null).ToArray();

            
            foreach (var method in methods) // 遍历控制器类型的所有方法
            {
                var routeAttribute = method.GetCustomAttribute<WebApiAttribute>(); // 获取方法上的 WebAPIAttribute 自定义属性
                if (routeAttribute is null) // 如果存在 WebAPIAttribute 属性
                {
                    continue;
                }
                var url = GetRoutesUrl(autoHostingAttribute, routeAttribute, controllerType, method);
                if (url is null) continue;

                SereinEnv.WriteLine(InfoType.INFO, url);
                var apiType = routeAttribute.ApiType.ToString();

                var routerInfo = new RouterInfo
                {
                    ApiType = routeAttribute.ApiType,
                    Url = url,
                    MethodInfo = method,
                };

                var config = new ApiHandleConfig(routerInfo);
                if(!HandleModels.TryGetValue(apiType, out var configs))
                {
                    configs = new ConcurrentDictionary<string, ApiHandleConfig>();
                    HandleModels[apiType] = configs;
                }
                configs.TryAdd(url, config);
                _controllerTypes.TryAdd(url,controllerType);
            }
            return;
        }

        /// <summary>
        /// 在外部调用API后，解析路由，调用对应的方法
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<InvokeResult> HandleAsync(HttpListenerContext context)
        {
            var request = context.Request; // 获取请求对象
            var uri = request.Url; // 获取请求的完整URL
            var httpMethod = request.HttpMethod.ToUpper(); // 获取请求的 HTTP 方法
            var fullUrl = uri.ToString(); // 获取完整URL字符串
            var routeValues = GetUrlData(fullUrl); // 解析 URL 获取路由参数
            var requestBody = await ReadRequestBodyAsync(request); // 读取请求体内容
            var requestId = System.Threading.Interlocked.Increment(ref _requestId);
            var requestInfo = new ApiRequestInfo
            {
                RequestId = requestId,
                ApiType = httpMethod,
                Url = fullUrl,
                Body = requestBody,
            };
            if (OnBeforRequest?.Invoke(requestInfo) == false)
            {
                return InvokeResult.Fail(requestId, HandleState.RequestBlocked); // 请求被阻止
            }
            if (!HandleModels.TryGetValue(httpMethod, out var modules) || request.Url is null)
            {
                return InvokeResult.Fail(requestId, HandleState.NotHttpApiRequestType); // 没有对应HTTP请求方法的处理
            }
            var template = request.Url.AbsolutePath.ToLower() ;
            if (!_controllerTypes.TryGetValue(template, out var controllerType))
            {
                return InvokeResult.Fail(requestId, HandleState.NotHanleController); // 没有对应的处理器
            }
            if (!modules.TryGetValue(template, out var config))
            {
                return InvokeResult.Fail(requestId, HandleState.NotHandleConfig); // 没有对应的处理配置
            }

            ControllerBase controllerInstance;
            try
            {
                controllerInstance = (ControllerBase)SereinIOC.CreateObject(controllerType);
            }
            catch 
            {
                return InvokeResult.Fail(requestId, HandleState.HanleControllerIsNull); // 未找到控制器实例
            }


            

            controllerInstance.Url = uri.AbsolutePath; // 设置控制器实例的 URL 属性

            object?[] args;
            switch (httpMethod) 
            {
                case "GET":
                    args = config.GetArgsOfGet(routeValues); // Get请求
                    break;
                case "POST":
                    controllerInstance.Body = requestBody;
                    if (!JsonHelper.TryParse(requestBody, out var requestJObject))
                    {
                        var exTips = $"body 无法转换为 json 数据, body: {requestBody}";
                        return InvokeResult.Fail(requestId, HandleState.InvokeArgError, new Exception(exTips));
                    }
                    (var isPass, var index, var type, var ex) = config.TryGetArgsOfPost(routeValues, requestJObject, out args);
                    if (!isPass)
                    {
                        var exTips = $"尝试转换第{index}个入参参数时，类型 {type.FullName} 参数获取失败：{ex?.Message}";
                        return InvokeResult.Fail(requestId, HandleState.InvokeArgError, new Exception(exTips));
                    }
                    break;
                default:
                    return InvokeResult.Fail(requestId, HandleState.NotHttpApiRequestType);
            }

            try
            {
                var invokeResult = await config.HandleAsync(controllerInstance, args);

                return InvokeResult.Ok(requestId, invokeResult);
            }
            catch (Exception ex)
            {
                return InvokeResult.Fail(requestId, HandleState.InvokeErrored, ex);
            }

        }


        /// <summary>
        /// 读取Body中的消息
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private async Task<string> ReadRequestBodyAsync(HttpListenerRequest request)
        {
            using (Stream stream = request.InputStream)
            {
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }

        /// <summary>
        /// 从方法中收集路由信息，返回方法对应的url
        /// </summary>
        /// <param name="autoHostingAttribute">类的特性</param>
        /// <param name="webAttribute">方法的特性</param>
        /// <param name="controllerType">控制器类型</param>
        /// <param name="method">方法信息</param>
        /// <returns>方法对应的urk</returns>
        private static string GetRoutesUrl(WebApiControllerAttribute autoHostingAttribute, WebApiAttribute webAttribute, Type controllerType, MethodInfo method)
        {
            string controllerName;
            if (string.IsNullOrWhiteSpace(autoHostingAttribute.Url))
            {
                controllerName = controllerType.Name.Replace("Controller", "").ToLower(); // 获取控制器名称并转换为小写
            }
            else
            {
                controllerName = autoHostingAttribute.Url;
            }

            var httpMethod = webAttribute.ApiType; // 获取 HTTP 方法
            var customUrl = webAttribute.Url; // 获取自定义 URL

            string url;
            controllerName = CleanUrl(controllerName);
            if (string.IsNullOrWhiteSpace(customUrl))
            {
                url = $"/{controllerName}/{method.Name}".ToLower();// 清理自定义 URL，并构建新的 URL
            }
            else
            {
                if(customUrl == "/")
                {
                    url = $"/{controllerName}".ToLower(); // 使用控制器
                }
                else
                {
                    
                    customUrl = CleanUrl(customUrl);
                    url = $"/{controllerName}/{customUrl}".ToLower();// 清理自定义 URL，并构建新的 URL
                }
            }
            return url;

        }

        /// <summary>
        /// 修正方法特性中的URL格式
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private static string CleanUrl(string url)
        {

            while (url.Length > 0 && url[0] == '/') // 去除开头的斜杠
            {
                url = url.Substring(1);
            }

            while (url.Length > 0 && url[url.Length - 1] == '/') // 去除末尾的斜杠
            {
                url = url.Substring(0, url.Length - 1);
            }

            for (int i = 0; i < url.Length - 1; i++) // 去除连续的斜杠
            {
                if (url[i] == '/' && url[i + 1] == '/')
                {
                    url = url.Remove(i, 1);
                    i--;
                }
            }

            return url; // 返回清理后的 URL
        }

        /// <summary>
        /// 方法声明，用于解析 URL 获取路由参数
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetUrlData(string uri)
        {
            Dictionary<string, string> routeValues = new Dictionary<string, string>();

            var pathParts = uri.Split('?'); // 拆分 URL，获取路径部分

            if (pathParts.Length > 1) // 如果包含查询字符串
            {
                //var queryParams = HttpUtility.ParseQueryString(pathParts[1]); // 解析查询字符串
                //foreach (string key in queryParams) // 遍历查询字符串的键值对
                //{
                //    if (key == null) continue;
                //    routeValues[key] = queryParams[key]; // 将键值对添加到路由参数字典中
                //}
                var parsedQuery = QueryStringParser.ParseQueryString(pathParts[1]);
                foreach (var kvp in parsedQuery)
                {
                    //Console.WriteLine($"{kvp.Key}: {kvp.Value}");
                    routeValues[kvp.Key.ToLower()] = kvp.Value; // 将键值对添加到路由参数字典中
                }
            }

            return routeValues; // 返回路由参数字典
        }

       
    }


    
}

