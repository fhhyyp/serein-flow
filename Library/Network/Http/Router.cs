using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.Entity;
using Serein.Library.Utils;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Enum = System.Enum;
using Type = System.Type;

namespace Serein.Library.Web
{
    public interface IRouter
    {
        void AddHandle(Type controllerType);
        Task<bool> ProcessingAsync(HttpListenerContext context);
    }



    public class ApiHandleConfig
    {
        private readonly Delegate EmitDelegate;
        private readonly EmitHelper.EmitMethodType EmitMethodType;

        public enum PostArgType
        {
            None,   
            IsUrlData,
            IsBobyData,
        }
        public ApiHandleConfig(MethodInfo methodInfo)
        {
            EmitMethodType = EmitHelper.CreateDynamicMethod(methodInfo, out EmitDelegate);
            var parameterInfos = methodInfo.GetParameters();
            ParameterType = parameterInfos.Select(t => t.ParameterType).ToArray();
            ParameterName = parameterInfos.Select(t => t.Name.ToLower()).ToArray();

            PostArgTypes = parameterInfos.Select(p =>
            {
                bool isUrlData = p.GetCustomAttribute(typeof(UrlAttribute)) != null;
                bool isBobyData = p.GetCustomAttribute(typeof(BobyAttribute)) != null;
                if (isBobyData)
                {
                    return PostArgType.IsBobyData;
                }
                else if (isUrlData)
                {
                    return PostArgType.IsUrlData;
                }
                else
                {
                    return PostArgType.None;
                }
            }).ToArray();


         
        }
        private readonly PostArgType[] PostArgTypes; 
        private readonly string[] ParameterName;
        private readonly Type[] ParameterType;


        public async Task<object> HandleGet(object instance, Dictionary<string, string> routeData)
        {
            object[] args = new object[ParameterType.Length];
            for (int i = 0; i < ParameterType.Length; i++)
            {
                var type = ParameterType[i];
                var argName = ParameterName[i];
                if (routeData.TryGetValue(argName, out var argValue))
                {
                    if (type == typeof(string))
                    {
                        args[i] = argValue;
                    }
                    else // if (type.IsValueType)
                    {
                        args[i] = JsonConvert.DeserializeObject(argValue, type);
                    }
                }
                else
                {
                    args[i] = type.IsValueType ? Activator.CreateInstance(type) : null;
                }
                
            }

            object result;
            try
            {
                if (EmitMethodType == EmitHelper.EmitMethodType.HasResultTask && EmitDelegate is Func<object, object[], Task<object>> hasResultTask)
                {
                    result = await hasResultTask(instance, args);
                }
                else if (EmitMethodType == EmitHelper.EmitMethodType.Task && EmitDelegate is Func<object, object[], Task> task)
                {
                    await task.Invoke(instance, args);
                    result = null;
                }
                else if (EmitMethodType == EmitHelper.EmitMethodType.Func && EmitDelegate is Func<object, object[], object> func)
                {
                    result = func.Invoke(instance, args);
                }
                else
                {
                    result = null;
                }
            }
            catch (Exception ex)
            {
                result = null;
                await Console.Out.WriteLineAsync(ex.Message);
                
            }
            return result;
           
        }



        public async Task<object> HandlePost(object instance, JObject jsonObject, Dictionary<string, string> routeData)
        {
            object[] args = new object[ParameterType.Length];
            for (int i = 0; i < ParameterType.Length; i++)
            {
                var type = ParameterType[i];
                var argName = ParameterName[i];
                if (PostArgTypes[i] == PostArgType.IsUrlData)
                {
                    if (routeData.TryGetValue(argName, out var argValue))
                    {
                        if (type == typeof(string))
                        {
                            args[i] = argValue;
                        }
                        else // if (type.IsValueType)
                        {
                            args[i] = JsonConvert.DeserializeObject(argValue, type);
                        }
                    }
                    else
                    {
                        args[i] = type.IsValueType ? Activator.CreateInstance(type) : null;
                    }
                }
                else if (PostArgTypes[i] == PostArgType.IsBobyData)
                {
                    args[i] = jsonObject;
                }
                else
                {
                    var jsonValue = jsonObject.GetValue(argName);
                    if (jsonValue is null)
                    {
                        // 值类型返回默认值，引用类型返回null
                        args[i] = type.IsValueType ? Activator.CreateInstance(type) : null;
                    }
                    else
                    {
                        args[i] = jsonValue.ToObject(type);
                    }
                }
                
               
            }

            object result;
            try
            {
                if (EmitMethodType == EmitHelper.EmitMethodType.HasResultTask && EmitDelegate is Func<object, object[], Task<object>> hasResultTask)
                {
                    result = await hasResultTask(instance, args);
                }
                else if (EmitMethodType == EmitHelper.EmitMethodType.Task && EmitDelegate is Func<object, object[], Task> task)
                {
                    await task.Invoke(instance, args);
                    result = null;
                }
                else if (EmitMethodType == EmitHelper.EmitMethodType.Func && EmitDelegate is Func<object, object[], object> func)
                {
                    result = func.Invoke(instance, args);
                }
                else
                {
                    result = null;
                }
            }
            catch (Exception ex)
            {
                result = null;
                await Console.Out.WriteLineAsync(ex.Message);

            }
            return result;

        }

        
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
        private readonly ConcurrentDictionary<string, Type> _controllerTypes = new ConcurrentDictionary<string, Type>();

        /// <summary>
        /// 用于存储路由信息，[GET|POST - [Url - Method]]
        /// </summary>
        //private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, MethodInfo>> _routes = new ConcurrentDictionary<string, ConcurrentDictionary<string, MethodInfo>>();


        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ApiHandleConfig>> HandleModels = new ConcurrentDictionary<string, ConcurrentDictionary<string, ApiHandleConfig>>();



        public Router(ISereinIOC SereinIOC)
        {
            this.SereinIOC = SereinIOC;
            foreach (ApiType method in Enum.GetValues(typeof(ApiType))) // 遍历 HTTP 枚举类型的所有值
            {
                HandleModels.TryAdd(method.ToString(), new ConcurrentDictionary<string, ApiHandleConfig>()); // 初始化每种 HTTP 方法对应的路由字典
            }
#if false
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
#endif
        }

        public void AddHandle(Type controllerType)
        {
            if (!controllerType.IsClass || controllerType.IsAbstract) return; // 如果不是类或者是抽象类，则直接返回

            var autoHostingAttribute = controllerType.GetCustomAttribute<AutoHostingAttribute>();
            var methods = controllerType.GetMethods().Where(m => m.GetCustomAttribute<WebApiAttribute>() != null).ToArray();

            
            foreach (var method in methods) // 遍历控制器类型的所有方法
            {
                var routeAttribute = method.GetCustomAttribute<WebApiAttribute>(); // 获取方法上的 WebAPIAttribute 自定义属性
                if (routeAttribute is null) // 如果存在 WebAPIAttribute 属性
                {
                    continue;
                }
                var url = AddRoutesUrl(autoHostingAttribute, routeAttribute, controllerType, method);
                if (url is null) continue;

                Console.WriteLine(url);
                var apiType = routeAttribute.ApiType.ToString();

                var config = new ApiHandleConfig(method);
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
        public async Task<bool> ProcessingAsync(HttpListenerContext context)
        {
            var request = context.Request; // 获取请求对象
            var response = context.Response; // 获取响应对象
            var url = request.Url; // 获取请求的 URL
            var httpMethod = request.HttpMethod; // 获取请求的 HTTP 方法
            var template = request.Url.AbsolutePath.ToLower();
            if (!_controllerTypes.TryGetValue(template, out Type controllerType))
            {
                return false;
            }

            var routeValues = GetUrlData(url); // 解析 URL 获取路由参数

            ControllerBase controllerInstance = (ControllerBase)SereinIOC.Instantiate(controllerType);

            if (controllerInstance is null)
            {
                return false; // 未找到控制器实例
            }

            controllerInstance.Url = url.AbsolutePath;


            if (!HandleModels.TryGetValue(httpMethod, out var modules))
            {
                return false;
            }
            if (!modules.TryGetValue(template,out var config))
            {
                return false;
            }
            



            dynamic invokeResult;
            switch (httpMethod) 
            {
                case "GET":
                     invokeResult = config.HandleGet(controllerInstance, routeValues);
                    break;
                case "POST":
                    var requestBody = await ReadRequestBodyAsync(request); // 读取请求体内容
                    controllerInstance.BobyData = requestBody;
                    var requestJObject = JObject.Parse(requestBody);
                    invokeResult = config.HandlePost(controllerInstance,  requestJObject,  routeValues);
                    break;
                default:
                    invokeResult = null;
                    break;
            }
            object result;
            try
            {
                result = invokeResult.Result;
            }
            catch (Exception)
            {
                result = invokeResult;
            }
            Return(response, result); // 返回结果
            return true;

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

            var httpMethod = webAttribute.ApiType; // 获取 HTTP 方法
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
                 //HandleModels[httpMethod.ToString()].TryAdd(url ); // 将 URL 和方法添加到对应的路由字典中
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
                // _routes[httpMethod.ToString()].TryAdd(url, method); // 将 URL 和方法添加到对应的路由字典中
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
                    routeValues[kvp.Key.ToLower()] = kvp.Value; // 将键值对添加到路由参数字典中
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

