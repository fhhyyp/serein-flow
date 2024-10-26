using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serein.Library.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Network.WebSocketCommunication.Handle
{
    /// <summary>
    /// 
    /// </summary>
    public class JsonMsgHandleConfig
    {
        public Guid HandleGuid { get; }


        internal JsonMsgHandleConfig(SocketHandleModule model,
                                     ISocketHandleModule instance,
                                     MethodInfo methodInfo,
                                     Action<Exception, Action<object>> onExceptionTracking,
                                     bool ArgNotNull)
        {
            DelegateDetails = new DelegateDetails(methodInfo);
            this.Module = model;
            Instance = instance;
            var parameterInfos = methodInfo.GetParameters();
            this.ParameterType = parameterInfos.Select(t => t.ParameterType).ToArray();
            this.ParameterName = parameterInfos.Select(t => t.Name).ToArray();
            this.HandleGuid = instance.HandleGuid;
            this.OnExceptionTracking = onExceptionTracking;
            this.ArgNotNull = ArgNotNull;

            this.useData = parameterInfos.Select(p => p.GetCustomAttribute<UseDataAttribute>() != null).ToArray();
            this.useMsgId = parameterInfos.Select(p => p.GetCustomAttribute<UseMsgIdAttribute>() != null).ToArray();
#if NET5_0_OR_GREATER
            this.IsCheckArgNotNull = parameterInfos.Select(p => p.GetCustomAttribute<NotNullAttribute>() != null).ToArray(); 
#endif
            

            
            if(IsCheckArgNotNull is null)
            {
                IsCheckArgNotNull = parameterInfos.Select(p => p.GetCustomAttribute<NeedfulAttribute>() != null).ToArray();
            }
            else
            {
                // 兼容两种非空特性的写法
                var argNotNull = parameterInfos.Select(p => p.GetCustomAttribute<NeedfulAttribute>() != null).ToArray();
                for (int i = 0; i < IsCheckArgNotNull.Length; i++)
                {
                    if (!IsCheckArgNotNull[i] && argNotNull[i])
                    {
                        IsCheckArgNotNull[i] = true;
                    }
                }
            }

        }

        /// <summary>
        /// 参数不能为空
        /// </summary>
        private bool ArgNotNull;
        /// <summary>
        /// Emit委托
        /// </summary>
        private readonly DelegateDetails DelegateDetails;
        /// <summary>
        /// 未捕获的异常跟踪
        /// </summary>
        private readonly Action<Exception, Action<object>> OnExceptionTracking;
        /// <summary>
        /// 所在的模块
        /// </summary>
        private readonly SocketHandleModule Module;
        /// <summary>
        /// 所使用的实例
        /// </summary>
        private readonly ISocketHandleModule Instance;
        /// <summary>
        /// 参数名称
        /// </summary>
        private readonly string[] ParameterName;
        /// <summary>
        /// 参数类型
        /// </summary>
        private readonly Type[] ParameterType;
        /// <summary>
        /// 是否使Data整体内容作为入参参数
        /// </summary>
        private readonly bool[] useData;
        /// <summary>
        /// 是否使用消息ID作为入参参数
        /// </summary>
        private readonly bool[] useMsgId;
        /// <summary>
        /// 是否检查变量为空
        /// </summary>
        private readonly bool[] IsCheckArgNotNull;

        public async void Handle(Func<object, Task> SendAsync,string msgId, JObject jsonObject)
        {
            object[] args = new object[ParameterType.Length];
            bool isCanInvoke = true;; // 表示是否可以调用方法
            for (int i = 0; i < ParameterType.Length; i++)
            {
                var type = ParameterType[i]; // 入参变量类型
                var argName = ParameterName[i]; // 入参参数名称
                #region 传递消息ID
                if (useMsgId[i])
                {
                    args[i] = msgId;
                }
                #endregion
                #region DATA JSON数据
                else if (useData[i])
                {
                    args[i] = jsonObject.ToObject(type);
                }
                #endregion
                #region 值类型参数
                else if (type.IsValueType)
                {
                    var jsonValue = jsonObject.GetValue(argName);
                    if (!(jsonValue is null))
                    {
                        args[i] = jsonValue.ToObject(type);
                    }
                    else
                    {
                        if (ArgNotNull && !IsCheckArgNotNull[i]) // 检查不能为空
                        {

                            args[i] = Activator.CreateInstance(type); // 值类型返回默认值
                        }
                        else
                        {
                            isCanInvoke = false; // 参数不能为空，终止调用
                            break;
                        }
                    }
                }
                #endregion
                #region 引用类型参数
                else if (type.IsClass)
                {
                    var jsonValue = jsonObject.GetValue(argName);
                    if (!(jsonValue is null))
                    {
                        args[i] = jsonValue.ToObject(type);
                    }
                    else
                    {
                        if (ArgNotNull && !IsCheckArgNotNull[i])
                        {

                            args[i] = null; // 引用类型返回null
                        }
                        else
                        {
                            isCanInvoke = false; // 参数不能为空，终止调用
                            break;
                        }
                    }
                } 
                #endregion
                #region 传递消息委托
                else if (type.IsGenericType) // 传递SendAsync委托
                {
                    if (type.IsAssignableFrom(typeof(Func<object, Task>)))
                    {
                        args[i] = new Func<object, Task>(async data =>
                        {
                            var jsonText = JsonConvert.SerializeObject(data);
                            await SendAsync.Invoke(jsonText);
                        });
                    }
                    else if (type.IsAssignableFrom(typeof(Func<string, Task>)))
                    {
                        args[i] = new Func<string, Task>(async data =>
                        {
                            await SendAsync.Invoke(data);
                        });
                    }
                    else if (type.IsAssignableFrom(typeof(Action<object>)))
                    {
                        args[i] = new Action<object>(async data =>
                        {
                            var jsonText = JsonConvert.SerializeObject(data);
                            await SendAsync.Invoke(jsonText);
                        });
                    }
                    else if (type.IsAssignableFrom(typeof(Action<string>)))
                    {
                        args[i] = new Action<string>(async data =>
                        {
                            var jsonText = JsonConvert.SerializeObject(data);
                            await SendAsync.Invoke(jsonText);
                        });
                    }
                }  
                #endregion
            }

            if (!isCanInvoke)
            {
                return;
            }

            object result;
            try
            {
                result = await DelegateDetails.InvokeAsync(Instance, args);
            }
            catch (Exception ex)
            {
                result = null;
                await Console.Out.WriteLineAsync(ex.Message);
                this.OnExceptionTracking.Invoke(ex, (async exData =>
                {
                    await SendAsync.Invoke(exData);
                }));
            }
            
            if (Module.IsReturnValue)
            {
                _ = SendAsync.Invoke(result);
            }

        }

    }


  



}
