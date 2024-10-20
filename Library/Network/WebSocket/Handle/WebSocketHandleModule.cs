using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serein.Library.Network.WebSocketCommunication.Handle
{
    /// <summary>
    /// Json消息处理模块
    /// </summary>
    public class WebSocketHandleModule
    {
        /// <summary>
        /// Json消息处理模块
        /// </summary>
        public WebSocketHandleModule(string ThemeJsonKey, string DataJsonKey)
        {
            this.ThemeJsonKey = ThemeJsonKey;
            this.DataJsonKey = DataJsonKey;
        }

        /// <summary>
        /// 指示处理模块该使用json中的哪个key作为业务区别字段
        /// </summary>
        public string ThemeJsonKey { get; }

        /// <summary>
        /// 指示处理模块该使用json中的哪个key作为业务数据字段
        /// </summary>
        public string DataJsonKey { get; }

        /// <summary>
        /// 存储处理数据的配置
        /// </summary>
        public ConcurrentDictionary<string, JsonMsgHandleConfig> MyHandleConfigs = new ConcurrentDictionary<string, JsonMsgHandleConfig>();

        /// <summary>
        /// 添加处理配置
        /// </summary>
        /// <param name="module">处理模块</param>
        /// <param name="jsonMsgHandleConfig">处理配置</param>
        internal bool AddHandleConfigs(SocketHandleModule module,JsonMsgHandleConfig jsonMsgHandleConfig)
        {
            if (!MyHandleConfigs.ContainsKey(module.ThemeValue))
            {
                MyHandleConfigs[module.ThemeValue] = jsonMsgHandleConfig;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 移除某个处理模块
        /// </summary>
        /// <param name="socketControlBase"></param>
        /// <returns></returns>
        public bool RemoveConfig(ISocketHandleModule socketControlBase)
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

        /// <summary>
        /// 卸载当前模块的所有配置
        /// </summary>
        public void UnloadConfig()
        {
            var temp = MyHandleConfigs.Values;
            MyHandleConfigs.Clear();
        }

        /// <summary>
        /// 处理JSON数据
        /// </summary>
        /// <param name="tSendAsync"></param>
        /// <param name="jsonObject"></param>
        public void HandleSocketMsg(Func<string, Task> tSendAsync, JObject jsonObject)
        {
            // 获取到消息
            string themeKeyName = jsonObject.GetValue(ThemeJsonKey)?.ToString();
            if (!MyHandleConfigs.TryGetValue(themeKeyName, out var handldConfig))
            {
                // 没有主题
                return;
            }

            Func<object, Task> SendAsync = async (data) =>
            {
                var sendMsg = new
                {
                    theme = themeKeyName,
                    token = "",
                    data = data,
                };
                var msg = JsonConvert.SerializeObject(sendMsg);
                await tSendAsync(msg);
            };
            try
            {

                JObject dataObj = jsonObject.GetValue(DataJsonKey).ToObject<JObject>();
                handldConfig.Handle(SendAsync, dataObj);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"error in ws : {ex.Message}{Environment.NewLine}json value:{jsonObject}");
                return;
            }

   

           
        }

    }

}
