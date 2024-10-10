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
            if (MyHandleModuleDict.TryRemove(key, out var myHandleModules))
            {
                myHandleModules.ResetConfig(socketControlBase);
            }

        }
        public void AddModule(ISocketControlBase socketControlBase)
        {
            var type = socketControlBase.GetType();
            var moduleAttribute = type.GetCustomAttribute<AutoSocketModuleAttribute>();
            if (moduleAttribute is null)
            {
                return;
            }

            // 添加处理模块
            var themeKey = moduleAttribute.JsonThemeField;
            var dataKey = moduleAttribute.JsonDataField;

            var handlemodule = AddMyHandleModule(themeKey, dataKey);
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(method =>
                {
                    var methodsAttribute = method.GetCustomAttribute<AutoSocketHandleAttribute>();
                    if (methodsAttribute is null)
                    {
                        return (string.Empty, null);
                    }
                    else
                    {
                        var value = methodsAttribute.ThemeValue;
                        return (value, method);
                    }
                })
                .Where(x => !string.IsNullOrEmpty(x.value)).ToList();
            if (methods.Count == 0)
            {
                return;
            }

            foreach ((var value, var method) in methods)
            {
                handlemodule.AddHandleConfigs(value, socketControlBase, method);
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
