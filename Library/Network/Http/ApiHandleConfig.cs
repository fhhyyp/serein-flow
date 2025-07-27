using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.Library.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Serein.Library.Network
{
    /// <summary>
    /// api请求处理模块
    /// </summary>
    public class ApiHandleConfig
    {
        private readonly IJsonPortal jsonPortal;
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
        /// <param name="jsonPortal"></param>
        /// <param name="methodInfo"></param>
        public ApiHandleConfig(IJsonPortal jsonPortal, MethodInfo methodInfo)
        {
            delegateDetails = new DelegateDetails(methodInfo);
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

        /// <summary>
        /// 处理Get请求
        /// </summary>
        /// <returns></returns>
        public object[] GetArgsOfGet(Dictionary<string, string> routeData)
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
                        args[i] = jsonPortal.Deserialize(argValue, type); // JsonConvert.DeserializeObject(argValue, type);
                    }
                }
                else
                {
                    args[i] = type.IsValueType ? Activator.CreateInstance(type) : null;
                }
            }
            return args;
        }

        public object[] GetArgsOfPost(Dictionary<string, string> routeData, IJsonToken jsonObject)
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
                            args[i] = jsonPortal.Deserialize(argValue, type);
                        }
                    }
                    else
                    {
                        args[i] = type.IsValueType ? Activator.CreateInstance(type) : null;
                    }
                }
                else if (jsonObject != null && PostArgTypes[i] == PostArgType.IsBobyData)
                {
                    args[i] = jsonObject.ToObject(type);
                }
                else if (jsonObject != null)
                {
                    if(jsonObject.TryGetProperty(argName, out var jsonToken))
                    {
                        args[i] = jsonToken.ToObject(type);
                    }
                    else
                    {
                        args[i] = type.IsValueType ? Activator.CreateInstance(type) : null;
                    }
                    
                }
            }
            return args;
        }


        /// <summary>
        /// 处理请求
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public async Task<object> HandleAsync(object instance, object[] args)
        {
            object result = null;
            try
            {
                result = await delegateDetails.InvokeAsync(instance, args);
            }
            catch (Exception ex)
            {
                result = null;
                await Console.Out.WriteLineAsync(ex.Message);

            }
            return result;

        }

    }



}
