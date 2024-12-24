using Serein.Library.Utils;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

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
        /// <param name="moduleConfig">模块配置</param>
        /// <returns></returns>
        private WebSocketHandleModule AddMyHandleModule(WebSocketHandleModuleConfig moduleConfig)
        {
            var key = (moduleConfig.ThemeJsonKey, moduleConfig.DataJsonKey);
            if (!MyHandleModuleDict.TryGetValue(key, out var myHandleModule))
            {
                myHandleModule = new WebSocketHandleModule(moduleConfig);
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
            var moduleConfig = new WebSocketHandleModuleConfig()
            {
                ThemeJsonKey = themeKey,
                DataJsonKey = dataKey,
                MsgIdJsonKey = msgIdKey,
            };

            var handleModule = AddMyHandleModule(moduleConfig);
            var configs = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select<MethodInfo, WebSocketHandleConfiguration>(methodInfo =>
                {
                    var methodsAttribute = methodInfo.GetCustomAttribute<AutoSocketHandleAttribute>();
                    if (methodsAttribute is null)
                    {
                        return null;
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(methodsAttribute.ThemeValue))
                        {
                            methodsAttribute.ThemeValue = methodInfo.Name;
                        }

                        #region 生成处理配置
                        var config = new WebSocketHandleConfiguration();

                        config.ThemeValue = methodsAttribute.ThemeValue;
                        config.ArgNotNull = methodsAttribute.ArgNotNull;
                        config.IsReturnValue = methodsAttribute.IsReturnValue;
                        //if (config.IsReturnValue)
                        //{
                        //    // 重新检查是否能够返回
                        //    if (methodInfo.ReturnType == typeof(void))
                        //    {
                        //        config.IsReturnValue = false;  // void 不返回
                        //    }
                        //    else if (methodInfo.ReturnType == typeof(Unit))
                        //    {
                        //        config.IsReturnValue = false; // Unit 不返回
                        //    }
                        //    else if (methodInfo.ReturnType == typeof(Task))
                        //    {
                        //        config.IsReturnValue = false; // void 不返回
                        //    }
                           
                        //}
                        var parameterInfos = methodInfo.GetParameters();
                        
                        config.DelegateDetails = new DelegateDetails(methodInfo); // 对应theme的emit构造委托调用工具类
                        config.Instance = socketControlBase; // 调用emit委托时的实例
                        config.OnExceptionTracking = onExceptionTracking; // 异常追踪
                        config.ParameterType = parameterInfos.Select(t => t.ParameterType).ToArray(); // 入参参数类型
                        config.ParameterName = parameterInfos.Select(t => t.Name).ToArray(); // 入参参数名称
                        config.UseData = parameterInfos.Select(p => p.GetCustomAttribute<UseDataAttribute>() != null).ToArray(); // 是否使用整体data数据
                        config.UseMsgId = parameterInfos.Select(p => p.GetCustomAttribute<UseMsgIdAttribute>() != null).ToArray(); // 是否使用消息ID
#if NET5_0_OR_GREATER
                        config.IsCheckArgNotNull = parameterInfos.Select(p => p.GetCustomAttribute<NotNullAttribute>() != null).ToArray(); // 是否检查null
#endif

                        if (config.IsCheckArgNotNull is null)
                        {
                            config.IsCheckArgNotNull = parameterInfos.Select(p => p.GetCustomAttribute<NeedfulAttribute>() != null).ToArray(); // 是否检查null
                        }
                        else
                        {
                            // 兼容两种非空特性的写法
                            var argNotNull = parameterInfos.Select(p => p.GetCustomAttribute<NeedfulAttribute>() != null).ToArray(); // 是否检查null
                            for (int i = 0; i < config.IsCheckArgNotNull.Length; i++)
                            {
                                if (!config.IsCheckArgNotNull[i] && argNotNull[i])
                                {
                                    config.IsCheckArgNotNull[i] = true;
                                }
                            }
                        } 
                        #endregion


                        var value = methodsAttribute.ThemeValue;

                        return config;
                    }
                })
                .Where(config => config != null).ToList();
            if (configs.Count == 0)
            {
                return;
            }



            SereinEnv.WriteLine(InfoType.INFO, $"add websocket handle model :");
            SereinEnv.WriteLine(InfoType.INFO, $"theme key, data key : {themeKey}, {dataKey}");
            foreach (var config in configs)
            {
                SereinEnv.WriteLine(InfoType.INFO, $"theme value  : {config.ThemeValue} ");
                var result = handleModule.AddHandleConfigs(config);
            }

        }

        /// <summary>
        /// 异步处理消息
        /// </summary>
        /// <param name="context">此次请求的上下文</param>
        /// <returns></returns>
        public void Handle(WebSocketMsgContext context)
        {
            foreach (var module in MyHandleModuleDict.Values)
            {
                if (context.Handle)
                {
                    return;
                }
               _ = module.HandleAsync(context);
            }


        }





    }
}
