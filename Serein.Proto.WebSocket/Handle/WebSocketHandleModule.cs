using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Serein.Proto.WebSocket.Handle
{

    /// <summary>
    /// Json消息处理模块
    /// </summary>
    public class WebSocketHandleModule
    {
        /// <summary>
        /// Json消息处理模块
        /// </summary>
        public WebSocketHandleModule(WebSocketModuleConfig config)
        {
           _moduleConfig = config;
           _methodInvokeConfigs = new ConcurrentDictionary<string, MethodInvokeConfiguration>();
            _myMsgIdHash = new HashSet<string>();
        }

        /// <summary>
        /// 模块的处理配置
        /// </summary>
        private readonly WebSocketModuleConfig _moduleConfig;

        /// <summary>
        /// 用来判断消息是否重复
        /// </summary>
        private readonly HashSet<string> _myMsgIdHash;

        /// <summary>
        /// 存储处理数据的配置
        /// </summary>
        private readonly ConcurrentDictionary<string, MethodInvokeConfiguration> _methodInvokeConfigs ;


        /// <summary>
        /// 添加处理配置
        /// </summary>
        /// <param name="config">处理模块</param>
        internal bool AddHandleConfigs(WebSocketMethodConfig config)
        {
            if (!_methodInvokeConfigs.ContainsKey(config.ThemeValue))
            {
                _methodInvokeConfigs[config.ThemeValue] = config;
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
            foreach (var kv in _methodInvokeConfigs.ToArray())
            {
                var config = kv.Value;
                _methodInvokeConfigs.TryRemove(kv.Key, out _);
                
            }
            return _methodInvokeConfigs.Count == 0;
        }

        /// <summary>
        /// 卸载当前模块的所有配置
        /// </summary>
        public void UnloadConfig()
        {
            var temp = _methodInvokeConfigs.Values;
            _methodInvokeConfigs.Clear();
        }

       

        /// <summary>
        /// 处理JSON数据
        /// </summary>
        public async Task HandleAsync(WebSocketHandleContext context)
        {
            var jsonObject = context.MsgRequest; // 获取到消息

            if (jsonObject is null)
            {
                context.TriggerExceptionTracking($"请求没有获取到消息");
                return; // 没有获取到消息
            }
            if(!jsonObject.TryGetValue(_moduleConfig.ThemeJsonKey, out var themeToken) || themeToken.IsNull)
            {
                context.TriggerExceptionTracking($"请求没有获取到主题\"{_moduleConfig.ThemeJsonKey}\"");
                return; // 没有获取到消息
            }
            if(themeToken.Type != IJsonToken.TokenType.Value)
            {
                context.TriggerExceptionTracking($"请求主题需要值类型 \"{_moduleConfig.ThemeJsonKey}\"");
                return; // 没有获取到消息
            }

            var theme = themeToken.ToString(); // 获取主题
            // 验证主题
            if (!_methodInvokeConfigs.TryGetValue(theme, out var handldConfig))
            {
                context.TriggerExceptionTracking($"{_moduleConfig.ThemeJsonKey} 主题不存在");
                return; 
            }


            if (!jsonObject.TryGetValue(_moduleConfig.MsgIdJsonKey, out var msgIdToken) || themeToken.IsNull)
            {
                context.TriggerExceptionTracking($"主题 {theme} 没有消息Id");
                return; // 没有获取到消息
            }
            if (themeToken.Type != IJsonToken.TokenType.Value)
            {
                context.TriggerExceptionTracking($"请求消息Id需要值类型 \"{_moduleConfig.ThemeJsonKey}\"");
                return; // 没有获取到消息
            }

            var msgId = msgIdToken.ToString(); // 获取主题
            // 验证消息ID是否重复
            if (!_myMsgIdHash.Add(msgId))
            {
                context.TriggerExceptionTracking($"主题 {theme} 消息Id {msgId} 消息重复");
                return; // 消息重复
            }

            // 验证数据
            if (!jsonObject.TryGetValue(_moduleConfig.DataJsonKey, out var dataToken))
            {
                context.TriggerExceptionTracking($"主题 {theme} 消息Id {msgId} 数据提取失败，当前指定键\"{_moduleConfig.DataJsonKey}\"");
                return; // 没有主题
            }
            if(dataToken.Type != IJsonToken.TokenType.Object)
            {
                context.TriggerExceptionTracking($"主题 {theme} 消息Id {msgId} 数据需要 JSON Object");
            }

            context.MsgTheme = theme; // 添加主题
            context.MsgId = msgId; // 添加 ID
            context.MsgData = dataToken; // 添加消息
            context.MsgRequest = jsonObject; // 添加原始消息
            try
            {
                if (TryGetParameters(handldConfig, context, out var args))
                {
                    var result = await HandleAsync(handldConfig, args);
                    if (handldConfig.IsReturnValue)
                    {
                        await RepliedAsync(_moduleConfig, context, result);
                    }
                }
                else
                {
                    context.TriggerExceptionTracking($"主题 {theme} 消息Id {msgId} 参数获取失败");
                }
            }
            catch (Exception ex)
            {
                context.TriggerExceptionTracking(ex);
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
        public static async Task<object> HandleAsync(MethodInvokeConfiguration config, object?[] args)
        {
            if (config.DelegateDetails is null)
            {
                throw new InvalidOperationException("DelegateDetails 为 null, 无法进行调用.");
            }
            var instance = config.InstanceFactory?.Invoke();
            var result = await config.DelegateDetails.InvokeAsync(instance, args);
            return result;
        }


        /// <summary>
        /// 获取入参参数
        /// </summary>
        /// <param name="config">处理配置</param>
        /// <param name="context">处理上下文</param>
        /// <param name="args">返回的入参参数</param>
        /// <returns></returns>
        internal static bool TryGetParameters(MethodInvokeConfiguration config, WebSocketHandleContext context, out object?[] args)
        {
            args = new object[config.ParameterType.Length];
            var theme = context.MsgTheme;
            var msgId = context.MsgId;
            List<string> exTips = [$"主题 {theme} 消息Id {msgId}"];
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
                else if (config.UseRequest[i])
                {
                    args[i] = context.MsgRequest?.ToObject(type);
                }
                #endregion
                #region DATA JSON数据
                else if (config.UseData[i])
                {
                    args[i] = context.MsgData?.ToObject(type);
                }
                #endregion
                #region 入参参数
                else if (!config.IsNeedSendDelegate[i])
                {
                    var jsonValue = context.MsgData?.GetValue(argName);
                    if(jsonValue is null)
                    {
                        isCanInvoke = false;
                        exTips.Add($"参数 {argName}({i}) 不存在，请检查参数名称是否正确");
                        continue;
                    }
                    var data = jsonValue.ToObject(type);
                    if (data is null)
                    {
                        isCanInvoke = false;
                        exTips.Add($"参数 {argName}({i}) 解析失败，类型：{type.FullName}，值：{jsonValue}，请检查参数类型是否正确");
                        continue;
                    }
                    args[i] = data;
                }
                #endregion
                #region 传递消息委托
                else if (config.IsNeedSendDelegate[i]) // 传递SendAsync委托
                {
                    if (config.CachedSendDelegates != null && config.CachedSendDelegates[i] != null)
                    {
                        args[i] = config.CachedSendDelegates[i];
                        continue;
                    }

                    Delegate? del = null;
                    var st = config.SendDelegateType[i];
                    switch (st)
                    {
                        case SereinWebSocketService.SendType.ObjectAsync:
                            del = new Func<object, Task>(async data =>
                            {
                                var jsonText = JsonHelper.Serialize(data);
                                await context.SendAsync(jsonText);
                            });
                            break;
                        case SereinWebSocketService.SendType.StringAsync:
                            del = new Func<string, Task>(async data =>
                            {
                                await context.SendAsync(data);
                            });
                            break;
                        case SereinWebSocketService.SendType.Object:
                            del = new Action<object>(data =>
                            {
                                var jsonText = JsonHelper.Serialize(data);
                                _ = context.SendAsync(jsonText);
                            });
                            break;
                        case SereinWebSocketService.SendType.String:
                            del = new Action<string>(data =>
                            {
                                _ = context.SendAsync(data);
                            });
                            break;
                    }

                    if (del is not null)
                    {
                        config.CachedSendDelegates![i] = del;
                        args[i] = del;
                    }
                    else
                    {
                        isCanInvoke = false; // 方法要求参数不能为空，终止调用
                        exTips.Add($"参数 {argName}({i}) 发送委托类型错误");
                        break;
                    }
                }
                #endregion
            }
            if (!isCanInvoke)
            {
                string ex = string.Join(Environment.NewLine, exTips);
                context.TriggerExceptionTracking(ex);
            }
            return isCanInvoke;
        }

        


        /// <summary>
        /// 返回消息
        /// </summary>
        /// <param name="moduleConfig"></param>
        /// <param name="context"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task RepliedAsync(WebSocketModuleConfig moduleConfig,
                                       WebSocketHandleContext context,
                                       object data)
        {
            if (moduleConfig.IsResponseUseReturn)
            {
                var responseContent = JsonHelper.Serialize(data);
                await context.SendAsync(responseContent);
            }
            else
            {

                IJsonToken jsonData;
                jsonData = JsonHelper.Object(obj =>
                {
                    obj[moduleConfig.MsgIdJsonKey] = context.MsgId;
                    obj[moduleConfig.ThemeJsonKey] = context.MsgTheme;
                    obj[moduleConfig.DataJsonKey] = data is null ? null : JsonHelper.FromObject(data);
                });

                var msg = jsonData.ToString();
                await context.SendAsync(msg);
            }
        }
    }





}
