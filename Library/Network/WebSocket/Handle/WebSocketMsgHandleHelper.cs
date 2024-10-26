using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Serein.Library.Utils;
using System.Net.Http.Headers;
using System.Linq.Expressions;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Serein.Library.Network.WebSocketCommunication.Handle
{
    /// <summary>
    /// 适用于Json数据格式的WebSocket消息处理类
    /// </summary>
    public class WebSocketMsgHandleHelper
    {
        /// <summary>
        /// (Theme Name ,Data Name) - HandleModule
        /// </summary>
        public ConcurrentDictionary<(string, string), WebSocketHandleModule> MyHandleModuleDict
            = new ConcurrentDictionary<(string, string), WebSocketHandleModule>();

        private Action<Exception, Action<object>> _onExceptionTracking;
        /// <summary>
        /// 异常跟踪
        /// </summary>
        public event Action<Exception, Action<object>> OnExceptionTracking;

        /// <summary>
        /// 添加消息处理与异常处理
        /// </summary>
        /// <param name="themeKeyName"></param>
        /// <param name="dataKeyName"></param>
        /// <param name="msgIdKeyName"></param>
        /// <returns></returns>
        private WebSocketHandleModule AddMyHandleModule(string themeKeyName, string dataKeyName, string msgIdKeyName)
        {
            var key = (themeKeyName, dataKeyName);
            if (!MyHandleModuleDict.TryGetValue(key, out var myHandleModule))
            {
                myHandleModule = new WebSocketHandleModule(themeKeyName, dataKeyName, msgIdKeyName);
                MyHandleModuleDict[key] = myHandleModule;
            }
            return myHandleModule;
        }

        /// <summary>
        /// 移除某个模块的WebSocket消息处理
        /// </summary>
        /// <param name="socketControlBase"></param>
        public void RemoveModule(ISocketHandleModule socketControlBase)
        {
            var type = socketControlBase.GetType();
            var moduleAttribute = type.GetCustomAttribute<AutoSocketModuleAttribute>();
            if (moduleAttribute is null)
            {
                return;
            }
            var themeKeyName = moduleAttribute.ThemeKey;
            var dataKeyName = moduleAttribute.DataKey;
            var key = (themeKeyName, dataKeyName);
            if (MyHandleModuleDict.TryGetValue(key, out var myHandleModules))
            {
                var isRemote = myHandleModules.RemoveConfig(socketControlBase);
                if (isRemote) MyHandleModuleDict.TryGetValue(key, out _);
            }

        }


        /// <summary>
        /// 添加消息处理以及异常处理
        /// </summary>
        /// <param name="socketControlBase"></param>
        /// <param name="onExceptionTracking"></param>
        public void AddModule(ISocketHandleModule socketControlBase, Action<Exception, Action<object>> onExceptionTracking)
        {
            var type = socketControlBase.GetType();
            var moduleAttribute = type.GetCustomAttribute<AutoSocketModuleAttribute>();
            if (moduleAttribute is null)
            {
                return;
            }

            var themeKey = moduleAttribute.ThemeKey;
            var dataKey = moduleAttribute.DataKey;
            var msgIdKey = moduleAttribute.MsgIdKey;
            
            var handleModule = AddMyHandleModule(themeKey, dataKey, msgIdKey);
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(method =>
                {
                    var methodsAttribute = method.GetCustomAttribute<AutoSocketHandleAttribute>();
                    if (methodsAttribute is null)
                    {
                        return (null, null,false);
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(methodsAttribute.ThemeValue))
                        {
                            methodsAttribute.ThemeValue = method.Name;
                        }
                        var model = new SocketHandleModule
                        {
                            IsReturnValue = methodsAttribute.IsReturnValue,
                            ThemeValue = methodsAttribute.ThemeValue,
                        };
                        var value = methodsAttribute.ThemeValue;
                        var argNotNull = methodsAttribute.ArgNotNull;
                        return (model, method, argNotNull);
                    }
                })
                .Where(x => !(x.model is null)).ToList();
            if (methods.Count == 0)
            {
                return;
            }

            

            Console.WriteLine($"add websocket handle model :");
            Console.WriteLine($"theme key, data key : {themeKey}, {dataKey}");
            foreach ((var module, var method,var argNotNull) in methods)
            {
                Console.WriteLine($"theme value  : {module.ThemeValue}");
                try
                {
                    var jsonMsgHandleConfig = new JsonMsgHandleConfig(module, socketControlBase, method, onExceptionTracking, argNotNull);
                    var result = handleModule.AddHandleConfigs(module,jsonMsgHandleConfig);
                    if (!result) 
                    {
                        throw new Exception("添加失败，已经添加过相同的配置");
                    }
                }
                catch (Exception ex)
                {

                    Console.WriteLine($"error in add method: {method.Name}{Environment.NewLine}{ex}");
                }
            }

        }

        /// <summary>
        /// 异步处理消息
        /// </summary>
        /// <param name="sendAsync"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public void HandleMsg(Func<string, Task> sendAsync, string message)
        {
            JObject json = JObject.Parse(message);
            foreach (var module in MyHandleModuleDict.Values)
            {
                module.HandleSocketMsg(sendAsync, json);
            }

        }



    }
}
