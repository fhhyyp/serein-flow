using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serein.Library.IOC;
using Serein.Tool;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web;
using Enum = System.Enum;
using Type = System.Type;

namespace Serein.Library.Http
{



    /// <summary>
    /// 路由注册与解析
    /// </summary>
    public class Router
    {

        private readonly ConcurrentDictionary<string, bool> _controllerAutoHosting; // 存储是否实例化
        private readonly ConcurrentDictionary<string, Type> _controllerTypes; // 存储控制器类型
        private readonly ConcurrentDictionary<string, object> _controllerInstances; // 存储控制器实例对象
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, MethodInfo>> _routes; // 用于存储路由信息

        private readonly IServiceContainer serviceRegistry; // 用于存储路由信息

        //private Type PostRequest;

        public Router(IServiceContainer serviceRegistry) // 构造函数，初始化 Router 类的新实例
        {
            this.serviceRegistry = serviceRegistry;

            _routes = new ConcurrentDictionary<string, ConcurrentDictionary<string, MethodInfo>>(); // 初始化路由字典

            _controllerAutoHosting = new ConcurrentDictionary<string, bool>(); // 初始化控制器实例对象字典
            _controllerTypes = new ConcurrentDictionary<string, Type>(); // 初始化控制器实例对象字典
            _controllerInstances = new ConcurrentDictionary<string, object>(); // 初始化控制器实例对象字典

            foreach (API method in Enum.GetValues(typeof(API))) // 遍历 HTTP 枚举类型的所有值
            {
                _routes.TryAdd(method.ToString(), new ConcurrentDictionary<string, MethodInfo>()); // 初始化每种 HTTP 方法对应的路由字典
            }

            // 获取当前程序集
            Assembly assembly = Assembly.GetExecutingAssembly();

            // 获取包含“Controller”名称的类型
            var controllerTypes = assembly.GetTypes()
                                          .Where(t => t.Name.Contains("Controller"));

            Type baseAttribute = typeof(AutoHostingAttribute);
            Type baseController = typeof(ControllerBase);
            foreach (var controllerType in controllerTypes)
            {
                if (controllerType.IsSubclassOf(baseController) && controllerType.IsDefined(baseAttribute))
                {

                    // 如果属于控制器，并标记了AutoHosting特性，进行自动注册
                    AutoRegisterAutoController(controllerType);
                }
                else
                {
                    continue;
                }
            }
        }
        

        /// <summary>
        /// 自动注册 自动实例化控制器 类型
        /// </summary>
        /// <param name="controllerType"></param>
        public void AutoRegisterAutoController(Type controllerType) // 方法声明，用于注册并实例化控制器类型
        {
            if (!controllerType.IsClass || controllerType.IsAbstract) return; // 如果不是类或者是抽象类，则直接返回

            var autoHostingAttribute = controllerType.GetCustomAttribute<AutoHostingAttribute>();
            if (autoHostingAttribute != null) { 
                foreach (var method in controllerType.GetMethods()) // 遍历控制器类型的所有方法
                {
                    var apiGetAttribute = method.GetCustomAttribute<ApiGetAttribute>();
                    var apiPostAttribute = method.GetCustomAttribute<ApiPostAttribute>();
                    if( apiGetAttribute == null && apiPostAttribute == null )
                    {
                        continue;
                    }



                    WebApiAttribute webApiAttribute = new WebApiAttribute()
                    {
                        Type = apiGetAttribute != null ? API.GET : API.POST,
                        Url = apiGetAttribute != null ? apiGetAttribute.Url : apiPostAttribute.Url,
                        IsUrl = apiGetAttribute != null ? apiGetAttribute.IsUrl : apiPostAttribute.IsUrl,
                    };



                    if (apiPostAttribute != null) // 如果存在 WebAPIAttribute 属性
                    {
                        var url = AddRoutesUrl(autoHostingAttribute,
                                                webApiAttribute,
                                                controllerType, method);
                        Console.WriteLine(url);
                        if (url == null) continue;
                        _controllerAutoHosting[url] = true;
                        _controllerTypes[url] = controllerType;

                        _controllerInstances[url] = null;

                    }


                   /* var routeAttribute = method.GetCustomAttribute<WebApiAttribute>(); // 获取方法上的 WebAPIAttribute 自定义属性
                    if (routeAttribute != null) // 如果存在 WebAPIAttribute 属性
                    {
                        var url = AddRoutesUrl(autoHostingAttribute, routeAttribute, controllerType, method);
                        Console.WriteLine(url);
                        if (url == null) continue;
                        _controllerAutoHosting[url] = true;
                        _controllerTypes[url] = controllerType;
                        _controllerInstances[url] = null;
                    }*/
                }
            }
        }
        /// <summary>
        /// 手动注册 自动实例化控制器实例
        /// </summary>
        public void RegisterAutoController<T>() // 方法声明，用于动态注册路由
        {
            Type controllerType = typeof(T); // 获取控制器实例的类型
            foreach (var method in controllerType.GetMethods()) // 遍历控制器类型的所有方法
            {
                var apiGetAttribute = method.GetCustomAttribute<ApiGetAttribute>();
                var apiPostAttribute = method.GetCustomAttribute<ApiPostAttribute>();
                if (apiGetAttribute == null && apiPostAttribute == null)
                {
                    continue;
                }



                WebApiAttribute webApiAttribute = new WebApiAttribute()
                {
                    Type = apiGetAttribute != null ? API.GET : API.POST,
                    Url = apiGetAttribute != null ? apiGetAttribute.Url : apiPostAttribute.Url,
                    IsUrl = apiGetAttribute != null ? apiGetAttribute.IsUrl : apiPostAttribute.IsUrl,
                };



                var url = AddRoutesUrl(null, webApiAttribute, controllerType, method);

                if (url == null) continue;
                _controllerAutoHosting[url] = true;
                _controllerTypes[url] = controllerType;

                _controllerInstances[url] = null;

            }
        }


        /// <summary>
        /// 手动注册 实例持久控制器实例
        /// </summary>
        /// <param name="controllerInstance"></param>
        public void RegisterController<T>(T controllerInstance) // 方法声明，用于动态注册路由
        {
            if(controllerInstance == null) return;
            Type controllerType = controllerInstance.GetType(); // 获取控制器实例的类型
            foreach (var method in controllerType.GetMethods()) // 遍历控制器类型的所有方法
            {
                var apiGetAttribute = method.GetCustomAttribute<ApiGetAttribute>();
                var apiPostAttribute = method.GetCustomAttribute<ApiPostAttribute>();
                if (apiGetAttribute == null && apiPostAttribute == null)
                {
                    continue;
                }



                WebApiAttribute webApiAttribute = new WebApiAttribute()
                {
                    Type = apiGetAttribute != null ? API.GET : API.POST,
                    Url = apiGetAttribute != null ? apiGetAttribute.Url : apiPostAttribute.Url,
                    IsUrl = apiGetAttribute != null ? apiGetAttribute.IsUrl : apiPostAttribute.IsUrl,
                };



                var url = AddRoutesUrl(null, webApiAttribute, controllerType, method);

                if (url == null) continue;
                _controllerInstances[url] = controllerInstance;
                _controllerAutoHosting[url] = false;
            }
        }

        /// <summary>
        /// 从方法中收集路由信息
        /// </summary>
        /// <param name="controllerType"></param>
        public string AddRoutesUrl(AutoHostingAttribute autoHostingAttribute, WebApiAttribute webAttribute, Type controllerType, MethodInfo method)
        {
            string controllerName;
            if (autoHostingAttribute == null || string.IsNullOrWhiteSpace(autoHostingAttribute.Url))
            {
                controllerName = controllerType.Name.Replace("Controller", "").ToLower(); // 获取控制器名称并转换为小写
            }
            else
            {
                controllerName = autoHostingAttribute.Url;
            }

            var httpMethod = webAttribute.Type; // 获取 HTTP 方法
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
        /// 收集路由信息
        /// </summary>
        /// <param name="controllerType"></param>
        public void CollectRoutes(Type controllerType)
        {
            string controllerName = controllerType.Name.Replace("Controller", "").ToLower(); // 获取控制器名称并转换为小写
            foreach (var method in controllerType.GetMethods()) // 遍历控制器类型的所有方法
            {
                var routeAttribute = method.GetCustomAttribute<WebApiAttribute>(); // 获取方法上的 WebAPIAttribute 自定义属性
                if (routeAttribute != null) // 如果存在 WebAPIAttribute 属性
                {
                    var customUrl = routeAttribute.Url; // 获取自定义 URL
                    string url;
                    if (string.IsNullOrEmpty(customUrl)) // 如果自定义 URL 为空
                    {
                        url = $"/api/{controllerName}/{method.Name}".ToLower(); // 构建默认 URL
                    }
                    else
                    {
                        customUrl = CleanUrl(customUrl);
                        url = $"/api/{controllerName}/{method.Name}/{customUrl}".ToLower();// 清理自定义 URL，并构建新的 URL
                    }
                    var httpMethod = routeAttribute.Type; // 获取 HTTP 方法
                    _routes[httpMethod.ToString()].TryAdd(url, method); // 将 URL 和方法添加到对应的路由字典中
                }
            }
        }
        /// <summary>
        /// 解析路由，调用对应的方法
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<bool> RouteAsync(HttpListenerContext context)
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

            ControllerBase controllerInstance;
            if (!_controllerAutoHosting[template])
            {
                controllerInstance = (ControllerBase)_controllerInstances[template];
            }
            else
            {

                 controllerInstance = (ControllerBase)serviceRegistry.Instantiate(_controllerTypes[template]);// 使用反射创建控制器实例


            }

            if (controllerInstance == null)
            {
                return false; // 未找到控制器实例
            }

            controllerInstance.Url = url.AbsolutePath;
            object result;
            switch (httpMethod) // 根据请求的 HTTP 方法执行不同的操作
            {
                case "GET": // 如果是 GET 请求，传入方法、控制器、url参数
                    result = InvokeControllerMethodWithRouteValues(method, controllerInstance, routeValues);
                    break;
                case "POST": // POST 请求传入方法、控制器、请求体内容，url参数
                    var requestBody = await ReadRequestBodyAsync(request); // 读取请求体内容
                    controllerInstance.BobyData = requestBody;
                    var requestJObject = requestBody.FromJSON<object>();

                    result = InvokeControllerMethod(method, controllerInstance, requestJObject, routeValues);
                    break;
                default:

                    result = null;

                    break;
            }

            Return(response, result); // 返回结果

            return true;
        }

        public static string GetLog(string Url, string BobyData = "")
        {
            return Environment.NewLine +
                    "Url : " + Url + Environment.NewLine +
                    "Data : " + BobyData + Environment.NewLine;
        }

        /// <summary>
        ///  GET请求的控制器方法
        /// </summary>
        private object InvokeControllerMethodWithRouteValues(MethodInfo method, object controllerInstance, Dictionary<string, string> routeValues)
        {
            object[] parameters = GetMethodParameters(method, routeValues);
            return InvokeMethod(method, controllerInstance, parameters);
        }

        private static readonly Dictionary<MethodInfo, ParameterInfo[]> methodParameterCache = [];
        /// <summary>
        /// POST请求的调用控制器方法
        /// </summary>
        public object InvokeControllerMethod(MethodInfo method, object controllerInstance, dynamic requestData, Dictionary<string, string> routeValues)
        {
            object?[]? cachedMethodParameters;

            if (!methodParameterCache.TryGetValue(method, out ParameterInfo[] parameters))
            {
                parameters = method.GetParameters();
            }

            cachedMethodParameters = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                string? paramName = parameters[i].Name;
                bool isUrlData = parameters[i].GetCustomAttribute(typeof(IsUrlDataAttribute)) != null;
                bool isBobyData = parameters[i].GetCustomAttribute(typeof(IsBobyDataAttribute)) != null;

                if (isUrlData)
                {
                    if (!string.IsNullOrEmpty(paramName) && routeValues.TryGetValue(paramName, out string? value))
                    {
                        cachedMethodParameters[i] = ConvertValue(value, parameters[i].ParameterType);
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
                        if (parameters[i].ParameterType == typeof(string))
                        {
                            cachedMethodParameters[i] = requestData[paramName].ToString();
                        }
                        else if (parameters[i].ParameterType == typeof(bool))
                        {
                            cachedMethodParameters[i] = requestData[paramName?.ToLower()].ToBool();
                        }
                        else if (parameters[i].ParameterType == typeof(int))
                        {
                            cachedMethodParameters[i] = requestData[paramName].ToInt();
                        }
                        else if (parameters[i].ParameterType == typeof(double))
                        {
                            cachedMethodParameters[i] = requestData[paramName].ToDouble();
                        }
                        else
                        {
                            cachedMethodParameters[i] = ConvertValue(requestData[paramName], parameters[i].ParameterType);
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


                if (routeValues.TryGetValue(paramName, out string? value))
                {
                    parameters[i] = ConvertValue(value, methodParameters[i].ParameterType);
                }
                else
                {

                    parameters[i] = null;

                }

            }

            return parameters;
        }

        /*/// <summary>
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
        }*/
        /// <summary>
        /// 转为对应的类型
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <returns></returns>
        private object ConvertValue(string value, Type targetType)
        {
            if(targetType == typeof(string))
            {
                return value;
            }

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
                int endIndex = ex.Message.IndexOf('\'');  // 查找类型信息结束的索引
                var typeInfo = ex.Message[startIndex..endIndex]; // 提取出错类型信息，该怎么传出去？

                return null;

            }
            catch // (Exception ex)
            {
                return value;
            }
        }

        /// <summary>
        /// 调用控制器方法传入参数
        /// </summary>
        /// <param name="method">方法</param>
        /// <param name="controllerInstance">控制器实例</param>
        /// <param name="methodParameters">参数列表</param>
        /// <returns></returns>
        private static object InvokeMethod(MethodInfo method, object controllerInstance, object[] methodParameters)
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
                int endIndex = ex.Message.IndexOf('\'');
                // 提取类型信息
                string typeInfo = ex.Message[startIndex..endIndex];
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return result; // 调用方法并返回结果

        }


        /// <summary>
        /// 方法声明，用于解析 URL 获取路由参数
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private static Dictionary<string, string> GetUrlData(Uri uri)
        {
            Dictionary<string, string> routeValues = [];

            var pathParts = uri.ToString().Split('?'); // 拆分 URL，获取路径部分

            if (pathParts.Length > 1) // 如果包含查询字符串
            {
                var queryParams = HttpUtility.ParseQueryString(pathParts[1]); // 解析查询字符串

                foreach (string key in queryParams) // 遍历查询字符串的键值对
                {
                    if (key == null) continue;

                    routeValues[key] = queryParams[key]; // 将键值对添加到路由参数字典中

                }
            }

            return routeValues; // 返回路由参数字典
        }

        /// <summary>
        /// 读取Body中的消息
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private static async Task<string> ReadRequestBodyAsync(HttpListenerRequest request)
        {
            using Stream stream = request.InputStream;
            using StreamReader reader = new(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }
        /// <summary>
        /// 返回响应消息
        /// </summary>
        /// <param name="response"></param>
        /// <param name="msg"></param>
        private static void Return(HttpListenerResponse response, dynamic msg)
        {
            string resultData;
            if (response != null)
            {
                try
                {
                    if (msg is IEnumerable && msg is not string)
                    {
                        // If msg is a collection (e.g., array or list), serialize it as JArray
                        resultData = JArray.FromObject(msg).ToString();
                    }
                    else
                    {
                        // Otherwise, serialize it as JObject
                        resultData = JObject.FromObject(msg).ToString();
                    }
                    byte[] buffer = Encoding.UTF8.GetBytes(resultData);
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                catch
                {
                    // If serialization fails, use the original message's string representation
                    resultData = msg.ToString();
                }


            }
        }

        /// <summary>
        /// 解析JSON
        /// </summary>
        /// <param name="requestBody"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private static dynamic ParseJson(string requestBody)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(requestBody))
                {
                    throw new Exception("Invalid JSON format");
                }
                return JObject.Parse(requestBody);
            }
            catch
            {
                throw new Exception("Invalid JSON format");
            }
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
                url = url[1..];
            }

            while (url.Length > 0 && url[^1] == '/') // 去除末尾的斜杠
            {
                url = url[..^1];
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
                int endIndex = errorMessage.IndexOf('\'');
                if (endIndex != -1)
                {
                    return errorMessage[startIndex..endIndex];
                }
            }


            return null;

        }
    }
}

