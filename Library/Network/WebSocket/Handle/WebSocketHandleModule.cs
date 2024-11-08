using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serein.Library.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        public WebSocketHandleModule(WebSocketHandleModuleConfig config)
        {
           this.moduleConfig = config;
        }

        /// <summary>
        /// 模块的处理配置
        /// </summary>
        private readonly WebSocketHandleModuleConfig moduleConfig;

        /// <summary>
        /// 用来判断消息是否重复
        /// </summary>
        private HashSet<string> _myMsgIdHash = new HashSet<string>();
        /// <summary>
        /// 存储处理数据的配置
        /// </summary>
        public ConcurrentDictionary<string, HandleConfiguration> MyHandleConfigs = new ConcurrentDictionary<string, HandleConfiguration>();


        /// <summary>
        /// 添加处理配置
        /// </summary>
        /// <param name="config">处理模块</param>
        internal bool AddHandleConfigs(WebSocketHandleConfiguration config)
        {
            if (!MyHandleConfigs.ContainsKey(config.ThemeValue))
            {
                MyHandleConfigs[config.ThemeValue] = config;
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
                if (config.Instance.HandleGuid.Equals(socketControlBase.HandleGuid))
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
        public async Task HandleAsync(WebSocketMsgContext context)
        {
            var jsonObject = context.JsonObject; // 获取到消息
            string theme = jsonObject.GetValue(moduleConfig.ThemeJsonKey)?.ToString();
            if (!MyHandleConfigs.TryGetValue(theme, out var handldConfig))
            {
                return; // 没有主题
            }
            context.MsgTheme = theme; // 添加主题
            string msgId = jsonObject.GetValue(moduleConfig.MsgIdJsonKey)?.ToString();
            if (_myMsgIdHash.Contains(msgId))
            {
                SereinEnv.WriteLine(InfoType.WARN, $"[{msgId}]{theme} 消息重复");
                return;
            }
            context.MsgId = msgId; // 添加 ID
            _myMsgIdHash.Add(msgId);

            try
            {
                var dataObj = jsonObject.GetValue(moduleConfig.DataJsonKey)?.ToObject<JObject>();
                context.MsgData = dataObj; // 添加消息
                if (WebSocketHandleModule.TryGetParameters(handldConfig, context, out var args))
                {
                    var result =  await WebSocketHandleModule.HandleAsync(handldConfig, args);
                    if (handldConfig.IsReturnValue)
                    {
                        await context.RepliedAsync(moduleConfig, context, result);
                    }
                }
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(InfoType.ERROR, $"error in ws : {ex.Message}{Environment.NewLine}json value:{jsonObject}");
            }
            finally
            {
                context.Handle = true;
            }
        }


        /// <summary>
        /// 调用
        /// </summary>
        /// <param name="config"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static async Task<object> HandleAsync(HandleConfiguration config, object[] args)
        {
            var result = await config.DelegateDetails.InvokeAsync(config.Instance, args);
            return result;
        }


        /// <summary>
        /// 获取入参参数
        /// </summary>
        /// <param name="config">处理配置</param>
        /// <param name="context">处理上下文</param>
        /// <param name="args">返回的入参参数</param>
        /// <returns></returns>
        internal static bool TryGetParameters(HandleConfiguration config, WebSocketMsgContext context, out object[] args)
        {
            args = new object[config.ParameterType.Length];
            bool isCanInvoke = true; ; // 表示是否可以调用方法

            for (int i = 0; i < config.ParameterType.Length; i++)
            {
                var type = config.ParameterType[i]; // 入参变量类型
                var argName = config.ParameterName[i]; // 入参参数名称
                #region 传递消息ID
                if (config.UseMsgId[i])
                {
                    args[i] = context.MsgId;
                }
                #endregion
                #region DATA JSON数据
                else if (config.UseData[i])
                {
                    args[i] = context.MsgData.ToObject(type);
                }
                #endregion
                #region 值类型参数
                else if (type.IsValueType)
                {
                    var jsonValue = context.MsgData.GetValue(argName);
                    if (!(jsonValue is null))
                    {
                        args[i] = jsonValue.ToObject(type);
                    }
                    else
                    {
                        if (config.ArgNotNull && !config.IsCheckArgNotNull[i]) // 检查不能为空
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
                    var jsonValue = context.MsgData.GetValue(argName);
                    if (!(jsonValue is null))
                    {
                        args[i] = jsonValue.ToObject(type);
                    }
                    else
                    {
                        if (config.ArgNotNull && !config.IsCheckArgNotNull[i])
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
                            await context.SendAsync(jsonText);
                        });
                    }
                    else if (type.IsAssignableFrom(typeof(Func<string, Task>)))
                    {
                        args[i] = new Func<string, Task>(async data =>
                        {
                            await context.SendAsync(data);
                        });
                    }
                    else if (type.IsAssignableFrom(typeof(Action<object>)))
                    {
                        args[i] = new Action<object>(async data =>
                        {
                            var jsonText = JsonConvert.SerializeObject(data);
                            await context.SendAsync(jsonText);
                        });
                    }
                    else if (type.IsAssignableFrom(typeof(Action<string>)))
                    {
                        args[i] = new Action<string>(async data =>
                        {
                            var jsonText = JsonConvert.SerializeObject(data);
                            await context.SendAsync(jsonText);
                        });
                    }
                }
                #endregion
            }
            return isCanInvoke;
        }


    }





}
