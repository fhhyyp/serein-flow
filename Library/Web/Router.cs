using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serein.Library.Api;
using Serein.Library.Attributes;
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
using System.Web;
using Enum = System.Enum;
using Type = System.Type;

namespace Serein.Library.Web
{
    public interface IRouter
    {
        bool RegisterController(Type controllerType);
        Task<bool> ProcessingAsync(HttpListenerContext context);
    }

    /// <summary>
    /// 路由注册与解析
    /// </summary>
    public class Router : IRouter
    {
        private readonly ISereinIOC SereinIOC; // 用于存储路由信息


        /// <summary>
        /// 控制器实例对象的类型，每次调用都会重新实例化，[Url - ControllerType]
        /// </summary>
        private readonly ConcurrentDictionary<string, Type> _controllerTypes; // 存储控制器类型

        /// <summary>
        /// 用于存储路由信息，[GET|POST - [Url - Method]]
        /// </summary>
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, MethodInfo>> _routes;

        
        // private readonly ILoggerService loggerService; // 用于存储路由信息

        //private Type PostRequest;

        public Router(ISereinIOC SereinIOC)
        {
            this.SereinIOC = SereinIOC;
            _routes = new ConcurrentDictionary<string, ConcurrentDictionary<string, MethodInfo>>(); // 初始化路由字典
            _controllerTypes = new ConcurrentDictionary<string, Type>(); // 初始化控制器实例对象字典
            foreach (API method in Enum.GetValues(typeof(API))) // 遍历 HTTP 枚举类型的所有值
            {
                _routes.TryAdd(method.ToString(), new ConcurrentDictionary<string, MethodInfo>()); // 初始化每种 HTTP 方法对应的路由字典
            }
            Type baseAttribute = typeof(AutoHostingAttribute);
            Type baseController = typeof(ControllerBase);

            // 获取当前程序集
            Assembly assembly = Assembly.GetExecutingAssembly();
            // 获取包含“Controller”名称的类型
            var controllerTypes = assembly.GetTypes().Where(type => type.IsSubclassOf(baseController) // 控制器子类
                                                                 && type.IsDefined(baseAttribute)     // 包含特性
                                                                 && type.Name.Contains("Controller"));

            foreach (var controllerType in controllerTypes)
            {
                RegisterController(controllerType);
            }
        }

        /// <summary>
        /// 在外部调用API后，解析路由，调用对应的方法
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<bool> ProcessingAsync(HttpListenerContext context)
        {
            var request = context.Request; // 获取请求对象
            var response = context.Response; // 获取响应对象
            var url = request.Url; // 获取请求的 URL
            var httpMethod = request.HttpMethod; // 获取请求的 HTTP 方法
            var template = request.Url.AbsolutePath.ToLower();
            if (!_routes[httpMethod].TryGetValue(template, out MethodInfo method))
            {
                return false;
            }

            var routeValues = GetUrlData(url); // 解析 URL 获取路由参数

            ControllerBase controllerInstance = (ControllerBase)SereinIOC.Instantiate(_controllerTypes[template]);// 使用反射创建控制器实例

            if (controllerInstance is null)
            {
                return false; // 未找到控制器实例
            }

            controllerInstance.Url = url.AbsolutePath;
            try
            {
                object result;
                switch (httpMethod) // 根据请求的 HTTP 方法执行不同的操作
                {
                    case "GET": // 如果是 GET 请求，传入方法、控制器、url参数
                        // loggerService.Information(GetLog(template));
                        result = InvokeControllerMethodWithRouteValues(method, controllerInstance, routeValues);
                        break;
                    case "POST": // POST 请求传入方法、控制器、请求体内容，url参数
                        var requestBody = await ReadRequestBodyAsync(request); // 读取请求体内容
                        controllerInstance.BobyData = requestBody;
                        var requestJObject = JObject.Parse(requestBody);  //requestBody.FromJSON<JObject>();

                        // loggerService.Information(GetLog(template, requestBody));
                        result = InvokeControllerMethod(method, controllerInstance, requestJObject, routeValues);
                        break;
                    default:
                        result = null;
                        break;
                }
                Return(response, result); // 返回结果
                return true;
            }
            catch (Exception ex)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound; // 返回 404 错误
                Return(response, ex.Message); // 返回结果
                return true;
            }

        }

        /// <summary>
        /// 自动注册并实例化控制器类型
        /// </summary>
        /// <param name="controllerType"></param>
        public bool RegisterController(Type controllerType) // 方法声明，用于注册并实例化控制器类型
        {
            if (!controllerType.IsClass || controllerType.IsAbstract) return false; // 如果不是类或者是抽象类，则直接返回

            var autoHostingAttribute = controllerType.GetCustomAttribute<AutoHostingAttribute>();
            var methods = controllerType.GetMethods().Where(m => m.GetCustomAttribute<WebApiAttribute>() != null).ToArray();

            foreach (var method in methods) // 遍历控制器类型的所有方法
            {
                var routeAttribute = method.GetCustomAttribute<WebApiAttribute>(); // 获取方法上的 WebAPIAttribute 自定义属性
                if (routeAttribute != null) // 如果存在 WebAPIAttribute 属性
                {
                    var url = AddRoutesUrl(autoHostingAttribute, routeAttribute, controllerType, method);
                    Console.WriteLine(url);
                    if (url is null) continue;
                    _controllerTypes[url] = controllerType;
                }
            }
            return true;
        }

        #region 调用Get Post对应的方法

        /// <summary>
        ///  GET请求的控制器方法
        /// </summary>
        private object InvokeControllerMethodWithRouteValues(MethodInfo method, object controllerInstance, Dictionary<string, string> routeValues)
        {
            object[] parameters = GetMethodParameters(method, routeValues);
            return InvokeMethod(method, controllerInstance, parameters);
        }

        /// <summary>
        /// GET请求调用控制器方法传入参数
        /// </summary>
        /// <param name="method">方法</param>
        /// <param name="controllerInstance">控制器实例</param>
        /// <param name="methodParameters">参数列表</param>
        /// <returns></returns>
        private object InvokeMethod(MethodInfo method, object controllerInstance, object[] methodParameters)
        {
            object result = null;
            try
            {
                result = method?.Invoke(controllerInstance, methodParameters);
            }
            catch (ArgumentException ex)
            {
                string targetType = ExtractTargetTypeFromExceptionMessage(ex.Message);

                // 如果方法调用失败
                result = new
                {
                    error = $"函数签名类型[{targetType}]不符合",
                };
            }
            catch (JsonSerializationException ex)
            {

                // 查找类型信息开始的索引
                int startIndex = ex.Message.IndexOf("to type '") + "to type '".Length;
                // 查找类型信息结束的索引
                int endIndex = ex.Message.IndexOf("'", startIndex);
                // 提取类型信息
                string typeInfo = ex.Message.Substring(startIndex, endIndex - startIndex);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return result; // 调用方法并返回结果
        }

        private readonly Dictionary<MethodInfo, ParameterInfo[]> methodParameterCache = new Dictionary<MethodInfo, ParameterInfo[]>();
        /// <summary>
        /// POST请求调用控制器方法传入参数
        /// </summary>
        public object InvokeControllerMethod(MethodInfo method, object controllerInstance, JObject requestData, Dictionary<string, string> routeValues)
        {
            if (requestData is null) return null;
            ParameterInfo[] parameters;
            object[] cachedMethodParameters;
            if (!methodParameterCache.TryGetValue(method, out parameters))
            {
                parameters = method.GetParameters();
            }
            cachedMethodParameters = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                string paramName = parameters[i].Name;
                bool isUrlData = parameters[i].GetCustomAttribute(typeof(UrlAttribute)) != null;
                bool isBobyData = parameters[i].GetCustomAttribute(typeof(BobyAttribute)) != null;

                if (isUrlData)
                {
                    if (routeValues.ContainsKey(paramName))
                    {
                        cachedMethodParameters[i] = ConvertValue(routeValues[paramName], parameters[i].ParameterType);
                    }
                    else
                    {
                        cachedMethodParameters[i] = null;
                    }
                }
                else if (isBobyData)
                {
                    cachedMethodParameters[i] = ConvertValue(requestData.ToString(), parameters[i].ParameterType);
                }
                else
                {
                    if (requestData.ContainsKey(paramName))
                    {
                        var rd = requestData[paramName];
                        if (parameters[i].ParameterType == typeof(string))
                        {
                            cachedMethodParameters[i] = rd.ToString();
                        }
                        else if (parameters[i].ParameterType == typeof(bool))
                        {
                            cachedMethodParameters[i] = rd.ToBool();
                        }
                        else if (parameters[i].ParameterType == typeof(int))
                        {
                            cachedMethodParameters[i] = rd.ToInt();
                        }
                        else if (parameters[i].ParameterType == typeof(double))
                        {
                            cachedMethodParameters[i] = rd.ToDouble();
                        }
                        else
                        {
                            cachedMethodParameters[i] = ConvertValue(rd, parameters[i].ParameterType);
                        }
                    }
                    else
                    {
                        cachedMethodParameters[i] = null;
                    }
                }
            }

            // 缓存方法和参数的映射
            //methodParameterCache[method] = cachedMethodParameters;


            // 调用方法
            return method.Invoke(controllerInstance, cachedMethodParameters);
        }

        #endregion
        #region 工具方法
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
        /// 检查方法入参参数类型，返回对应的入参数组
        /// </summary>
        /// <param name="method"></param>
        /// <param name="routeValues"></param>
        /// <returns></returns>
        private object[] GetMethodParameters(MethodInfo method, Dictionary<string, string> routeValues)
        {
            ParameterInfo[] methodParameters = method.GetParameters();
            object[] parameters = new object[methodParameters.Length];

            for (int i = 0; i < methodParameters.Length; i++)
            {
                string paramName = methodParameters[i].Name;
                if (routeValues.ContainsKey(paramName))
                {
                    parameters[i] = ConvertValue(routeValues[paramName], methodParameters[i].ParameterType);
                }
                else
                {
                    parameters[i] = null;
                }
            }

            return parameters;
        }

        /// <summary>
        /// 转为对应的类型
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <returns></returns>
        private object ConvertValue(object value, Type targetType)
        {
            try
            {
                return JsonConvert.DeserializeObject(value.ToString(), targetType);
            }
            catch (JsonReaderException ex)
            {
                return value;
            }
            catch (JsonSerializationException ex)
            {
                // 如果无法转为对应的JSON对象
                int startIndex = ex.Message.IndexOf("to type '") + "to type '".Length; // 查找类型信息开始的索引
                int endIndex = ex.Message.IndexOf("'", startIndex);  // 查找类型信息结束的索引
                var typeInfo = ex.Message.Substring(startIndex, endIndex - startIndex); // 提取出错类型信息，该怎么传出去？
                return null;
            }
            catch // (Exception ex)
            {
                return value;
            }
        }

        /// <summary>
        /// 返回响应消息
        /// </summary>
        /// <param name="response"></param>
        /// <param name="msg"></param>
        private static void Return(HttpListenerResponse response, object msg)
        {
            string resultData;
            if (response != null)
            {
                try
                {
                    if (msg is JArray jArray)
                    {
                        resultData = jArray.ToString();
                    }
                    else if (msg is JObject jObject)
                    {
                        resultData = jObject.ToString();
                    }
                    else if (msg is IEnumerable ienumerable)
                    {
                        resultData = JArray.FromObject(ienumerable).ToString();
                    }
                    else if (msg is string tmpmsg)
                    {
                        resultData = tmpmsg;
                    }
                    else
                    {
                        // 否则，将其序列化为JObject
                        resultData = JObject.FromObject(msg).ToString();
                    }
                    // 
                    byte[] buffer = Encoding.UTF8.GetBytes(resultData);
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                catch (Exception ex)
                {
                    // If serialization fails, use the original message's string representation
                    try
                    {
                        resultData = ex.ToString();
                        byte[] buffer = Encoding.UTF8.GetBytes(resultData);
                        response.ContentLength64 = buffer.Length;
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                    }
                    catch (Exception ex1)
                    {

                        Console.WriteLine(ex1);
                    }
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
        private string AddRoutesUrl(AutoHostingAttribute autoHostingAttribute, WebApiAttribute webAttribute, Type controllerType, MethodInfo method)
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

            var httpMethod = webAttribute.Http; // 获取 HTTP 方法
            var customUrl = webAttribute.Url; // 获取自定义 URL

            string url;

            if (webAttribute.IsUrl)
            {

                if (string.IsNullOrEmpty(customUrl)) // 如果自定义 URL 为空
                {
                    url = $"/{controllerName}/{method.Name}".ToLower(); // 构建默认 URL
                }
                else
                {
                    customUrl = CleanUrl(customUrl);
                    url = $"/{controllerName}/{method.Name}/{customUrl}".ToLower();// 清理自定义 URL，并构建新的 URL
                }
                _routes[httpMethod.ToString()].TryAdd(url, method); // 将 URL 和方法添加到对应的路由字典中
            }
            else
            {
                if (string.IsNullOrEmpty(customUrl)) // 如果自定义 URL 为空
                {
                    url = $"/{controllerName}".ToLower(); // 构建默认 URL
                }
                else
                {
                    customUrl = CleanUrl(customUrl);
                    url = $"/{controllerName}/{customUrl}".ToLower();// 清理自定义 URL，并构建新的 URL
                }
                _routes[httpMethod.ToString()].TryAdd(url, method); // 将 URL 和方法添加到对应的路由字典中
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
        private Dictionary<string, string> GetUrlData(Uri uri)
        {
            Dictionary<string, string> routeValues = new Dictionary<string, string>();

            var pathParts = uri.ToString().Split('?'); // 拆分 URL，获取路径部分

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
                    routeValues[kvp.Key] = kvp.Value; // 将键值对添加到路由参数字典中
                }
            }

            return routeValues; // 返回路由参数字典
        }

        /// <summary>
        /// 从控制器调用方法的异常中获取出出错类型的信息
        /// </summary>
        /// <param name="errorMessage"></param>
        /// <returns></returns>
        public static string ExtractTargetTypeFromExceptionMessage(string errorMessage)
        {
            string targetText = "为类型“";
            int startIndex = errorMessage.IndexOf(targetText);
            if (startIndex != -1)
            {
                startIndex += targetText.Length;
                int endIndex = errorMessage.IndexOf("”", startIndex);
                if (endIndex != -1)
                {
                    return errorMessage.Substring(startIndex, endIndex - startIndex);
                }
            }

            return null;
        }
        #endregion
    }



    internal static class WebFunc
    {
        public static bool ToBool(this JToken token, bool defult = false)
        {
            var value = token?.ToString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return defult;
            }
            if (!bool.TryParse(value, out bool result))
            {
                return defult;
            }
            else
            {
                return result;
            }
        }
        public static int ToInt(this JToken token, int defult = 0)
        {
            var value = token?.ToString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return defult;
            }
            if (!int.TryParse(value, out int result))
            {
                return defult;
            }
            else
            {
                return result;
            }
        }
        public static double ToDouble(this JToken token, double defult = 0)
        {
            var value = token?.ToString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return defult;
            }
            if (!int.TryParse(value, out int result))
            {
                return defult;
            }
            else
            {
                return result;
            }
        }
    }

    #region 已经注释

    // private readonly ConcurrentDictionary<string, bool> _controllerAutoHosting; // 存储是否实例化
    // private readonly ConcurrentDictionary<string, object> _controllerInstances;

    //public void CollectRoutes(Type controllerType)
    //{
    //    string controllerName = controllerType.Name.Replace("Controller", "").ToLower(); // 获取控制器名称并转换为小写
    //    foreach (var method in controllerType.GetMethods()) // 遍历控制器类型的所有方法
    //    {
    //        var routeAttribute = method.GetCustomAttribute<WebApiAttribute>(); // 获取方法上的 WebAPIAttribute 自定义属性
    //        if (routeAttribute != null) // 如果存在 WebAPIAttribute 属性
    //        {
    //            var customUrl = routeAttribute.Url; // 获取自定义 URL
    //            string url;
    //            if (string.IsNullOrEmpty(customUrl)) // 如果自定义 URL 为空
    //            {
    //                url = $"/api/{controllerName}/{method.Name}".ToLower(); // 构建默认 URL
    //            }
    //            else
    //            {
    //                customUrl = CleanUrl(customUrl);
    //                url = $"/api/{controllerName}/{method.Name}/{customUrl}".ToLower();// 清理自定义 URL，并构建新的 URL
    //            }
    //            var httpMethod = routeAttribute.Http; // 获取 HTTP 方法
    //            _routes[httpMethod.ToString()].TryAdd(url, method); // 将 URL 和方法添加到对应的路由字典中
    //        }
    //    }
    //}

    //public void RegisterRoute<T>(T controllerInstance) // 方法声明，用于动态注册路由
    //{
    //    Type controllerType = controllerInstance.GetType(); // 获取控制器实例的类型
    //    var autoHostingAttribute = controllerType.GetCustomAttribute<AutoHostingAttribute>();
    //    foreach (var method in controllerType.GetMethods()) // 遍历控制器类型的所有方法
    //    {
    //        var webAttribute = method.GetCustomAttribute<WebApiAttribute>(); // 获取方法上的 WebAPIAttribute 自定义属性
    //        if (webAttribute != null) // 如果存在 WebAPIAttribute 属性
    //        {
    //            var url = AddRoutesUrl(autoHostingAttribute, webAttribute, controllerType, method);
    //            if (url == null) continue;
    //            _controllerInstances[url] = controllerInstance;
    //            _controllerAutoHosting[url] = false;
    //        }

    //    }
    //}

    #endregion
}

