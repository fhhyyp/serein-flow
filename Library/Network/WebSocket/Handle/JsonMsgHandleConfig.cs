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
            EmitMethodType = EmitHelper.CreateDynamicMethod(methodInfo,out EmitDelegate);
            this.Module = model;
            Instance = instance;
            var parameterInfos = methodInfo.GetParameters();
            this.ParameterType = parameterInfos.Select(t => t.ParameterType).ToArray();
            this.ParameterName = parameterInfos.Select(t => t.Name).ToArray();
            this.HandleGuid = instance.HandleGuid;
            this.OnExceptionTracking = onExceptionTracking;
            this.ArgNotNull = ArgNotNull;

            this.useMsgData = parameterInfos.Select(p => p.GetCustomAttribute<UseMsgDataAttribute>() != null).ToArray();
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
        private readonly Delegate EmitDelegate;
        /// <summary>
        /// Emit委托类型
        /// </summary>
        private readonly EmitHelper.EmitMethodType EmitMethodType;
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
        /// 是否使用整体data参数
        /// </summary>
        private readonly bool[] useMsgData;
        /// <summary>
        /// 是否检查变量为空
        /// </summary>
        private readonly bool[] IsCheckArgNotNull;



        public async void Handle(Func<object, Task> SendAsync, JObject jsonObject)
        {
            object[] args = new object[ParameterType.Length];
            bool isCanInvoke = true;; // 表示是否可以调用方法
            for (int i = 0; i < ParameterType.Length; i++)
            {
                var type = ParameterType[i];
                var argName = ParameterName[i];
                if (useMsgData[i])
                {
                    args[i] = jsonObject.ToObject(type);
                }
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
            }

            if (!isCanInvoke)
            {
                return;
            }

            object result;
            try
            {
                if (EmitMethodType == EmitHelper.EmitMethodType.HasResultTask && EmitDelegate is Func<object, object[], Task<object>> hasResultTask)
                {
                    result = await hasResultTask(Instance, args);
                    //Console.WriteLine(result);
                    // why not data?
                }
                else if (EmitMethodType == EmitHelper.EmitMethodType.Task && EmitDelegate is Func<object, object[], Task> task)
                {
                    await task.Invoke(Instance, args);
                    result = null;
                }
                else if (EmitMethodType == EmitHelper.EmitMethodType.Func && EmitDelegate is Func<object, object[], object> func)
                {
                    result = func.Invoke(Instance, args);
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
                this.OnExceptionTracking.Invoke(ex, (async data =>
                {

                    var jsonText = JsonConvert.SerializeObject(data);
                    await SendAsync.Invoke(jsonText);
                }));
            }
           //sw.Stop();
           //Console.WriteLine($"Emit Invoke：{sw.ElapsedTicks * 1000000F / Stopwatch.Frequency:n3}μs");

            if(result is null)
            {
                return;
            }
            else
            {
                if (Module.IsReturnValue)
                {
                    _ = SendAsync.Invoke(result);
                }
            }
            //if( &&  result != null && result.GetType().IsClass)
            //{
            //    //var reusltJsonText = JsonConvert.SerializeObject(result);
               
            //    //_ = SendAsync.Invoke($"{reusltJsonText}");
            //}
            

        }

    }


  



}
