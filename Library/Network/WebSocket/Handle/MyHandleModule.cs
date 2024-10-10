using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Network.WebSocketCommunication.Handle
{
    public class MyHandleModule
    {
        public MyHandleModule(string ThemeJsonKey, string DataJsonKey)
        {
            this.ThemeJsonKey = ThemeJsonKey;
            this.DataJsonKey = DataJsonKey;
        }
        public string ThemeJsonKey { get; }
        public string DataJsonKey { get; }

        public ConcurrentDictionary<string, MyHandleConfig> MyHandleConfigs = new ConcurrentDictionary<string, MyHandleConfig>();
        public void AddHandleConfigs(string themeValue, ISocketControlBase instance, MethodInfo methodInfo)
        {
            if (!MyHandleConfigs.ContainsKey(themeValue))
            {
                var myHandleConfig = new MyHandleConfig(instance, methodInfo);
                MyHandleConfigs[themeValue] = myHandleConfig;
            }
        }
        public void ResetConfig(ISocketControlBase socketControlBase)
        {
            foreach (var kv in MyHandleConfigs.ToArray())
            {
                var config = kv.Value;
                if (config.Instance.HandleGuid.Equals(socketControlBase.HandleGuid))
                {
                    MyHandleConfigs.TryRemove(kv.Key, out _);
                }
            }
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
