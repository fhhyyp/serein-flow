using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Network.WebSocketCommunication.Handle
{
    public class WebSocketHandleModule
    {
        public WebSocketHandleModule(string ThemeJsonKey, string DataJsonKey)
        {
            this.ThemeJsonKey = ThemeJsonKey;
            this.DataJsonKey = DataJsonKey;
        }
        public string ThemeJsonKey { get; }
        public string DataJsonKey { get; }




        public ConcurrentDictionary<string, WebSocketHandleConfig> MyHandleConfigs = new ConcurrentDictionary<string, WebSocketHandleConfig>();
        internal void AddHandleConfigs(SocketHandleModel model, ISocketHandleModule instance, MethodInfo methodInfo
            , Action<Exception, Action<object>> onExceptionTracking)
        {
            if (!MyHandleConfigs.ContainsKey(model.ThemeValue))
            {
                var myHandleConfig = new WebSocketHandleConfig(model,instance, methodInfo, onExceptionTracking);
                MyHandleConfigs[model.ThemeValue] = myHandleConfig;
            }
        }
        public bool ResetConfig(ISocketHandleModule socketControlBase)
        {
            foreach (var kv in MyHandleConfigs.ToArray())
            {
                var config = kv.Value;
                if (config.HandleGuid.Equals(socketControlBase.HandleGuid))
                {
                    MyHandleConfigs.TryRemove(kv.Key, out _);
                }
            }
            return MyHandleConfigs.Count == 0;
        }

        public void ResetConfig()
        {
            var temp = MyHandleConfigs.Values;
            MyHandleConfigs.Clear();
            foreach (var config in temp)
            {
                config.Clear();
            }
        }


        public void HandleSocketMsg(Func<string, Task> RecoverAsync, JObject jsonObject)
        {
            // 获取到消息
            string themeKeyName = jsonObject.GetValue(ThemeJsonKey)?.ToString();
            if (!MyHandleConfigs.TryGetValue(themeKeyName, out var handldConfig))
            {
                // 没有主题
                return;
            }
            if (jsonObject[DataJsonKey] is JObject dataJsonObject)
            {
                handldConfig.Handle(RecoverAsync, dataJsonObject);
            }
        }
    }

}
