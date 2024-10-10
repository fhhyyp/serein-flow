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

    public class SocketMsgHandleHelper
    {
        /// <summary>
        /// (Theme Name ,Data Name) - HandleModule
        /// </summary>
        public ConcurrentDictionary<(string, string), MyHandleModule> MyHandleModuleDict
            = new ConcurrentDictionary<(string, string), MyHandleModule>();

        private Action<Exception, Action<object>> _onExceptionTracking;
        /// <summary>
        /// 异常跟踪
        /// </summary>
        public event Action<Exception, Action<object>> OnExceptionTracking;

        private MyHandleModule AddMyHandleModule(string themeKeyName, string dataKeyName)
        {
            var key = (themeKeyName, dataKeyName);
            if (!MyHandleModuleDict.TryGetValue(key, out var myHandleModule))
            {
                myHandleModule = new MyHandleModule(themeKeyName, dataKeyName);
                MyHandleModuleDict[key] = myHandleModule;
            }
            return myHandleModule;
        }

        public void RemoteModule(ISocketControlBase socketControlBase)
        {
            var type = socketControlBase.GetType();
            var moduleAttribute = type.GetCustomAttribute<AutoSocketModuleAttribute>();
            if (moduleAttribute is null)
            {
                return;
            }
            var themeKeyName = moduleAttribute.JsonThemeField;
            var dataKeyName = moduleAttribute.JsonDataField;
            var key = (themeKeyName, dataKeyName);
            if (MyHandleModuleDict.TryGetValue(key, out var myHandleModules))
            {
                var isRemote = myHandleModules.ResetConfig(socketControlBase);
                if (isRemote) MyHandleModuleDict.TryGetValue(key, out _);
            }

        }
        public void AddModule(ISocketControlBase socketControlBase, Action<Exception, Action<object>> onExceptionTracking)
        {
            var type = socketControlBase.GetType();
            var moduleAttribute = type.GetCustomAttribute<AutoSocketModuleAttribute>();
            if (moduleAttribute is null)
            {
                return;
            }

            var themeKey = moduleAttribute.JsonThemeField;
            var dataKey = moduleAttribute.JsonDataField;
          
            var handlemodule = AddMyHandleModule(themeKey, dataKey);
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(method =>
                {
                    var methodsAttribute = method.GetCustomAttribute<AutoSocketHandleAttribute>();
                    if (methodsAttribute is null)
                    {
                        return (null, null);
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(methodsAttribute.ThemeValue))
                        {
                            methodsAttribute.ThemeValue = method.Name;
                        }
                        var model = new SocketHandleModel
                        {
                            IsReturnValue = methodsAttribute.IsReturnValue,
                            ThemeValue = methodsAttribute.ThemeValue,
                        };
                        var value = methodsAttribute.ThemeValue;
                        return (model, method);
                    }
                })
                .Where(x => !(x.model is null)).ToList();
            if (methods.Count == 0)
            {
                return;
            }

            Console.WriteLine($"add websocket handle model :");
            Console.WriteLine($"theme key, data key : {themeKey}, {dataKey}");
            foreach ((var model, var method) in methods)
            {
                Console.WriteLine($"theme value  : {model.ThemeValue}");
                handlemodule.AddHandleConfigs(model, socketControlBase, method, onExceptionTracking);
            }

        }

        public async Task HandleMsgAsync(Func<string, Task> RecoverAsync, string message)
        {
            JObject json = JObject.Parse(message);
            await Task.Run(() =>
            {
                foreach (var module in MyHandleModuleDict.Values)
                {

                    module.HandleSocketMsg(RecoverAsync, json);
                    
                }
            });

        }



    }
}
