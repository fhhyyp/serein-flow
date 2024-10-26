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
        public WebSocketHandleModule(string themeJsonKey, string dataJsonKey, string msgIdJsonKey)
        {
            this.ThemeJsonKey = themeJsonKey;
            this.DataJsonKey = dataJsonKey;
            this.MsgIdJsonKey = msgIdJsonKey;
        }

        /// <summary>
        /// 指示处理模块该使用 Json 中的哪个 Key 作为业务区别字段
        /// </summary>
        public string ThemeJsonKey { get; }

        /// <summary>
        /// 指示处理模块该使用 Json 中的哪个 Key 作为业务数据字段
        /// </summary>
        public string DataJsonKey { get; }
        
        /// <summary>
        /// 指示处理模块该使用 Json 中的哪个 Key 作为业务消息ID字段
        /// </summary>
        public string MsgIdJsonKey { get; }

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

        private HashSet<string> _myMsgIdHash = new HashSet<string>();

        /// <summary>
        /// 处理JSON数据
        /// </summary>
        /// <param name="sendAsync"></param>
        /// <param name="jsonObject"></param>
        public void HandleSocketMsg(Func<string, Task> sendAsync, JObject jsonObject)
        {
            // 获取到消息
            string theme = jsonObject.GetValue(ThemeJsonKey)?.ToString();
            if (!MyHandleConfigs.TryGetValue(theme, out var handldConfig))
            {
                // 没有主题
                return;
            }
            string msgId = jsonObject.GetValue(MsgIdJsonKey)?.ToString();
            if (_myMsgIdHash.Contains(msgId))
            {
                Console.WriteLine($"[{msgId}]{theme} 消息重复");
                return;
            }
            _myMsgIdHash.Add(msgId);

            try
            {
                JObject dataObj = jsonObject.GetValue(DataJsonKey)?.ToObject<JObject>();
                handldConfig.Handle(async (data) =>
                {
                    await this.SendAsync(sendAsync, msgId, theme, data);
                }, msgId, dataObj);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"error in ws : {ex.Message}{Environment.NewLine}json value:{jsonObject}");
                return;
            }
        }


        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="sendAsync"></param>
        /// <param name="msgId"></param>
        /// <param name="theme"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task SendAsync(Func<string, Task> sendAsync,string msgId, string theme, object data)
        {
            JObject jsonData;

            if (data is null)
            {
                jsonData = new JObject()
                {
                    [MsgIdJsonKey] = msgId,
                    [ThemeJsonKey] = theme,
                };
            }
            else
            {
                
                    JToken dataToken;
                    if ((data is System.Collections.IEnumerable || data is Array))
                    {
                        dataToken = JArray.FromObject(data);
                    }
                    else
                    {
                        dataToken = JObject.FromObject(data);
                    }

                    jsonData = new JObject()
                    {
                        [MsgIdJsonKey] = msgId,
                        [ThemeJsonKey] = theme,
                        [DataJsonKey] = dataToken
                    };
                
               
            }

            var msg = jsonData.ToString();
            
            
            await sendAsync.Invoke(msg);
        }


    }





}
