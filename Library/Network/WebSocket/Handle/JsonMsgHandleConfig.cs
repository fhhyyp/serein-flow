using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serein.Library.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly Delegate EmitDelegate;
        private readonly EmitHelper.EmitMethodType EmitMethodType;

        private Action<Exception, Action<object>> OnExceptionTracking;

        internal JsonMsgHandleConfig(SocketHandleModel model,ISocketHandleModule instance, MethodInfo methodInfo, Action<Exception, Action<object>> onExceptionTracking)
        {
            EmitMethodType = EmitHelper.CreateDynamicMethod(methodInfo,out EmitDelegate);
            this.Model = model;
            Instance = instance;
            var parameterInfos = methodInfo.GetParameters();
            ParameterType = parameterInfos.Select(t => t.ParameterType).ToArray();
            ParameterName = parameterInfos.Select(t => t.Name).ToArray();
            this.HandleGuid = instance.HandleGuid;
            this.OnExceptionTracking = onExceptionTracking;

        }

        private SocketHandleModel Model;
        private ISocketHandleModule Instance;
        public Guid HandleGuid { get;  }
        private string[] ParameterName;
        private Type[] ParameterType;


        public async void Handle(Func<string, Task> RecoverAsync, JObject jsonObject)
        {
            object[] args = new object[ParameterType.Length];
            for (int i = 0; i < ParameterType.Length; i++)
            {
                var type = ParameterType[i];
                var argName = ParameterName[i];
                if (type.IsGenericType)
                {
                   if (type.IsAssignableFrom(typeof(Func<object, Task>)))
                    {
                        args[i] = new Func<object, Task>(async data =>
                        {
                            var jsonText = JsonConvert.SerializeObject(data);
                            await RecoverAsync.Invoke(jsonText);
                        });
                    }
                    else if (type.IsAssignableFrom(typeof(Func<string, Task>)))
                    {
                        args[i] = new Func<string, Task>(async data =>
                        {
                            await RecoverAsync.Invoke(data);
                        });
                    }
                    else if (type.IsAssignableFrom(typeof(Action<object>)))
                    {
                        args[i] = new Action<object>(async data =>
                        {
                            var jsonText = JsonConvert.SerializeObject(data);
                            await RecoverAsync.Invoke(jsonText);
                        });
                    }
                    else if (type.IsAssignableFrom(typeof(Action<string>)))
                    {
                        args[i] = new Action<string>(async data =>
                        {
                            var jsonText = JsonConvert.SerializeObject(data);
                            await RecoverAsync.Invoke(jsonText);
                        });
                    }
                }
                else if (type.IsValueType || type.IsClass)
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
            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            object result;
            try
            {
                if (EmitMethodType == EmitHelper.EmitMethodType.HasResultTask && EmitDelegate is Func<object, object[], Task<object>> hasResultTask)
                {
                    result = await hasResultTask(Instance, args);
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
                    await RecoverAsync.Invoke(jsonText);
                }));
            }
           //sw.Stop();
           //Console.WriteLine($"Emit Invoke：{sw.ElapsedTicks * 1000000F / Stopwatch.Frequency:n3}μs");

            if(Model.IsReturnValue &&  result != null && result.GetType().IsClass)
            {
                var reusltJsonText = JsonConvert.SerializeObject(result);
                _ = RecoverAsync.Invoke($"{reusltJsonText}");
            }
            

        }
        public void Clear()
        {
            Instance = null;
            ParameterName = null;
            ParameterType = null;
            //expressionDelegate = null;
        }

    }


  



}
