using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Serein.Proto.HttpApi
{
    /// <summary>
    /// api请求处理模块
    /// </summary>
    public class ApiHandleConfig
    {
        private readonly DelegateDetails delegateDetails;

        /// <summary>
        /// Post请求处理方法中，入参参数类型
        /// </summary>
        public enum PostArgType
        {
            /// <summary>
            /// 不做处理
            /// </summary>
            None,
            /// <summary>
            /// 使用Url参数
            /// </summary>
            IsUrlData,
            /// <summary>
            /// 使用整体的Boby参数
            /// </summary>
            IsBobyData,
        }

        /// <summary>
        /// 添加处理配置
        /// </summary>
        /// <param name="routerInfo"></param>
        public ApiHandleConfig(RouterInfo routerInfo)
        {
            
            var methodInfo = routerInfo.MethodInfo;
            delegateDetails = new DelegateDetails(methodInfo);
            var parameterInfos = methodInfo.GetParameters();
            ParameterType = parameterInfos.Select(t => t.ParameterType).ToArray();
            ParameterName = parameterInfos.Select(t => t.Name!).ToArray();

            PostArgTypes = parameterInfos.Select(p =>
            {
                bool isUrlData = p.GetCustomAttribute(typeof(UrlAttribute)) != null;
                bool isBobyData = p.GetCustomAttribute(typeof(BodyAttribute)) != null;
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
            RouterInfo = routerInfo;
        }

        /// <summary>
        /// 路由信息
        /// </summary>
        public RouterInfo RouterInfo { get; }

        /// <summary>
        /// Post请求处理方法中，入参参数类型
        /// </summary>
        public PostArgType[] PostArgTypes { get;private set; }
        /// <summary>
        /// 参数名称
        /// </summary>
        public string[] ParameterName { get; private set; }
        /// <summary>
        /// 参数类型
        /// </summary>
        public Type[] ParameterType { get; private set; }
       
        /// <summary>
        /// 处理Get请求
        /// </summary>
        /// <returns></returns>
        public object?[] GetArgsOfGet(Dictionary<string, string> routeData)
        {
            object?[] args = new object[ParameterType.Length];
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
                        args[i] = JsonHelper.Deserialize(argValue, type); // JsonConvert.DeserializeObject(argValue, type);
                    }
                }
                else
                {
                    args[i] = type.IsValueType ? Activator.CreateInstance(type) : null;
                }
            }
            return args;
        }

        /// <summary>
        ///  从 POST 获取参数
        /// </summary>
        /// <param name="routeData"></param>
        /// <param name="jsonObject"></param>
        /// <returns></returns>
        public (bool, int, Type, Exception?) TryGetArgsOfPost(Dictionary<string, string> routeData, IJsonToken jsonObject, out object?[] argData)
        {
            argData = null;
            object?[] args = new object[ParameterType.Length];
            int i = 0;

            try
            {
                for (; i < ParameterType.Length; i++)
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
                                args[i] = JsonHelper.Deserialize(argValue, type);
                            }
                        }
                        else
                        {
                            args[i] = type.IsValueType ? Activator.CreateInstance(type) : null;
                        }
                    }
                    else if (jsonObject != null && PostArgTypes[i] == PostArgType.IsBobyData)
                    {
                        if (type.IsEnum)
                        {
                            args[i] = jsonObject.ToObject(type);
                        }
                        
                        else
                        {
                            args[i] = jsonObject.ToObject(type);
                        }
                    }
                    else if (jsonObject != null)
                    {
                        if (jsonObject.TryGetValue(argName, out var jsonToken))
                        {
                            args[i] = jsonToken.ToObject(type);
                        }
                        else
                        {
                            args[i] = type.IsValueType ? Activator.CreateInstance(type) : null;
                        }

                    }
                }
                argData = args;
                return (true ,-1, null, null);
            }
            catch (Exception ex)
            {
                argData = [];
                return (false,i, ParameterType[i], ex);
            }
        }


        /// <summary>
        /// 处理请求
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public async Task<object?> HandleAsync(object instance, object?[] args)
        {
            var result = await delegateDetails.InvokeAsync(instance, args);
            return result;

        }

    }



}
