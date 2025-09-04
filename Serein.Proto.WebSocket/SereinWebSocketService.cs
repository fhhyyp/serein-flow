using Serein.Library;
using Serein.Library.Utils;
using Serein.Proto.WebSocket.Attributes;
using Serein.Proto.WebSocket.Handle;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using NetWebSocket = System.Net.WebSockets.WebSocket;

namespace Serein.Proto.WebSocket
{
    /// <summary>
    /// WebSocket 服务类，负责管理所有 WebSocket 连接和处理模块
    /// </summary>
    public partial class SereinWebSocketService : ISereinWebSocketService
    {
        /// <summary>
        /// (Theme Name ,Data Name) - HandleModule
        /// </summary>
        private readonly ConcurrentDictionary<(string, string), WebSocketHandleModule> _socketModules;
        /// <summary>
        /// 追踪未处理的异常
        /// </summary>

        private Action<Exception>? _onExceptionTracking;
        private Action<string>? _onReply;
        private Func<WebSocketHandleContext, object, object> _onReplyMakeData;


        /// <summary>
        /// 维护所有 WebSocket 连接
        /// </summary>
        private readonly List<NetWebSocket> _sockets;

        /// <summary>
        /// 用于增加、移除 WebSocket 连接时，保证线程安全操作
        /// </summary>
        private readonly object _lock = new object();

        public int ConcetionCount => _sockets.Count;

        /// <summary>
        /// SereinWebSocketService 构造函数，初始化 WebSocket 模块字典和连接列表
        /// </summary>
        public SereinWebSocketService()
        {
            _socketModules = new ConcurrentDictionary<(string, string), WebSocketHandleModule>();
            _sockets = new List<NetWebSocket>();
            _lock = new object();
        }


        #region 添加处理模块

        /// <summary>
        /// 添加处理模块，使用指定的实例工厂和异常追踪回调
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instanceFactory"></param>
        /// <param name="onExceptionTracking"></param>
        public ISereinWebSocketService AddHandleModule<T>() where T : ISocketHandleModule, new()
        {
            var type = typeof(T);
            Func<ISocketHandleModule> instanceFactory = () => (T)Activator.CreateInstance(type);
            return AddHandleModule(type, instanceFactory);
        } 
        
        /// <summary>
        /// 添加处理模块，使用指定的实例工厂和异常追踪回调
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instanceFactory"></param>
        /// <param name="onExceptionTracking"></param>
        public ISereinWebSocketService AddHandleModule(ISocketHandleModule socketHandleModule) 
        {
            var type = socketHandleModule.GetType();
            Func<ISocketHandleModule> instanceFactory = () => socketHandleModule;
            return AddHandleModule(type, instanceFactory);
        }

        /// <summary>
        /// 添加处理模块，使用指定的实例工厂和异常追踪回调
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instanceFactory"></param>
        /// <param name="onExceptionTracking"></param>
        public ISereinWebSocketService AddHandleModule<T>(Func<ISocketHandleModule> instanceFactory) where T : ISocketHandleModule
        {
            var type = typeof(T);
            return AddHandleModule(type, instanceFactory);
        }

        /// <summary>
        /// 添加处理模块，使用指定的类型、实例工厂和异常追踪回调
        /// </summary>
        /// <param name="type"></param>
        /// <param name="instanceFactory"></param>
        /// <param name="onExceptionTracking"></param>
        private ISereinWebSocketService AddHandleModule(Type type,  Func<ISocketHandleModule> instanceFactory)
        {
            if(!CheckAttribute<WebSocketModuleAttribute>(type, out var attribute))
            {
                throw new Exception($"类型 {type} 需要标记 WebSocketModuleAttribute 特性");
            }
            var modbuleConfig = GetConfig(attribute);
            var module = GetOrAddModule(modbuleConfig);
            var methodInfos = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).ToArray();
            var methodConfigs = CreateMethodConfig(methodInfos, instanceFactory);

            SereinEnv.WriteLine(InfoType.INFO, $"add websocket handle model :");
            SereinEnv.WriteLine(InfoType.INFO, $"theme key, data key : {modbuleConfig.ThemeJsonKey}, {modbuleConfig.DataJsonKey}");
            foreach (var methodConfig in methodConfigs)
            {
                SereinEnv.WriteLine(InfoType.INFO, $"theme value  : {methodConfig!.ThemeValue} ");
                var result = module.AddHandleConfigs(methodConfig);
            }
            return this;
        }

        /// <summary>
        /// 检查特性
        /// </summary>
        /// <typeparam name="TAttribute"></typeparam>
        /// <param name="type"></param>
        /// <param name="attribute"></param>
        /// <returns></returns>
        private bool CheckAttribute<TAttribute>(Type type, out TAttribute attribute) where TAttribute : Attribute
        {
            attribute = type.GetCustomAttribute<TAttribute>();
            if (attribute is null)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 获取模块配置
        /// </summary>
        /// <param name="moduleAttribute"></param>
        /// <returns></returns>
        private WebSocketModuleConfig GetConfig(WebSocketModuleAttribute moduleAttribute)
        {
            var themeKey = moduleAttribute.ThemeKey;
            var dataKey = moduleAttribute.DataKey;
            var msgIdKey = moduleAttribute.MsgIdKey;
            var isResponseUseReturn = moduleAttribute.IsResponseUseReturn;
            var moduleConfig = new WebSocketModuleConfig()
            {
                ThemeJsonKey = themeKey,
                DataJsonKey = dataKey,
                MsgIdJsonKey = msgIdKey,
                IsResponseUseReturn = isResponseUseReturn,
            };
            return moduleConfig;
        }

        /// <summary>
        /// 获取或添加消息处理与异常处理
        /// </summary>
        /// <param name="moduleConfig">模块配置</param>
        /// <returns></returns>
        private WebSocketHandleModule GetOrAddModule(WebSocketModuleConfig moduleConfig)
        {
            var key = (moduleConfig.ThemeJsonKey, moduleConfig.DataJsonKey);
            if (_socketModules.TryGetValue(key, out var wsHandleModule))
            {
                return wsHandleModule;
            }
            wsHandleModule = new WebSocketHandleModule(moduleConfig);
            _socketModules[key] = wsHandleModule;
            return wsHandleModule;
        }

        /// <summary>
        /// 创建方法配置
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instanceFactory"></param>
        /// <param name="onExceptionTracking"></param>
        /// <returns></returns>
        private List<WebSocketMethodConfig> CreateMethodConfig(MethodInfo[] methodInfos,  Func<ISocketHandleModule> instanceFactory) 
        {
            List<WebSocketMethodConfig> configs = [];
            foreach (var methodInfo in methodInfos)
            {
                var wsMethodAttribute = methodInfo.GetCustomAttribute<WsMethodAttribute>();
                if (wsMethodAttribute is null)
                {
                    continue;
                }
                var parameterInfos = methodInfo.GetParameters();

               var temp_array = parameterInfos.Select(p =>
                {
                    var isSend = CheckSendType(p.ParameterType, out var sendType);
                    return new
                    {
                        IsNeedSend = isSend,
                        Type = sendType
                    };
                }).ToArray();

                var config = new WebSocketMethodConfig
                {
                    ThemeValue = string.IsNullOrEmpty(wsMethodAttribute.ThemeValue) ? methodInfo.Name : wsMethodAttribute.ThemeValue,
                    IsReturnValue = wsMethodAttribute.IsReturnValue,
                    DelegateDetails = new DelegateDetails(methodInfo), // 对应theme的emit构造委托调用工具类
                    InstanceFactory = instanceFactory, // 调用emit委托时的实例
                    //OnExceptionTracking = onExceptionTracking, // 异常追踪
                    ParameterType = parameterInfos.Select(t => t.ParameterType).ToArray(), // 入参参数类型
                    ParameterName = parameterInfos.Select(t => $"{t.Name}").ToArray(), // 入参参数名称
                    UseRequest = parameterInfos.Select(p => p.GetCustomAttribute<UseRequestAttribute>() is not null).ToArray(),// 是否使用整体data数据
                    UseData = parameterInfos.Select(p => p.GetCustomAttribute<UseDataAttribute>() is not null).ToArray(), // 是否使用整体data数据
                    UseMsgId = parameterInfos.Select(p => p.GetCustomAttribute<UseMsgIdAttribute>() is not null).ToArray(), // 是否使用消息ID
                    UseContent = parameterInfos.Select(p => p.ParameterType.IsAssignableFrom(typeof(WebSocketHandleContext))).ToArray(), // 是否使用上下文
                    IsNeedSendDelegate = temp_array.Select(p => p.IsNeedSend).ToArray(), // 是否需要发送消息的委托
                    SendDelegateType = temp_array.Select(p => p.Type).ToArray(), // 发送消息的委托类型
                    CachedSendDelegates = new Delegate[temp_array.Length], // 提前缓存发送委托数组
                };
                configs.Add(config);
            }
            return configs;
        }

        private bool CheckSendType(Type type , out SendType sendType)
        {
            if (type.IsAssignableFrom(typeof(Func<object, Task>)))
            {
                sendType = SendType.ObjectAsync;
            }
            else if (type.IsAssignableFrom(typeof(Func<string, Task>)))
            {
                sendType = SendType.StringAsync;
            }
            else if (type.IsAssignableFrom(typeof(Action<object>)))
            {
                sendType = SendType.StringAsync;
            }
            else if (type.IsAssignableFrom(typeof(Action<string>)))
            {
                sendType = SendType.StringAsync;
            }
            else
            {
                sendType = SendType.None;
                return false;
            }
            return true;
        }

        #endregion

        /// <summary>
        /// 跟踪未处理的异常
        /// </summary>
        /// <returns></returns>
        public ISereinWebSocketService TrackUnhandledExceptions(Action<Exception> onExceptionTracking)
        {
            _onExceptionTracking = onExceptionTracking;
            return this;
        }

        /// <summary>
        /// 传入新的 WebSocket 连接，开始进行处理
        /// </summary>
        /// <param name="webSocket"></param>
    

        /// <summary>
        /// 处理新的 WebSocket 连接
        /// </summary>
        /// <param name="socket"></param>
        /// <returns></returns>
        public async Task AddWebSocketHandleAsync(NetWebSocket socket)
        {
            lock (_lock) { _sockets.Add(socket); }
            var buffer = new byte[4096];

            var msgHandleUtil = new WebSocketMessageTransmissionTool(); // 消息队列

            _ = Task.Run(async () =>
            {
                await HandleMsgAsync(socket, msgHandleUtil);
            });

            try
            {
               
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(buffer: new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine($"收到客户端消息: {message}");
                        var echo = Encoding.UTF8.GetBytes(message);
                        await msgHandleUtil.WriteMsgAsync(message);  // 异步传递消息
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebSocket 异常: {ex.Message}");
            }
            finally
            {
                lock (_lock) { _sockets.Remove(socket); }
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "关闭连接", CancellationToken.None);
                socket.Dispose();
            }
        }

        /// <summary>
        /// 处理消息
        /// </summary>
        /// <param name="webSocket"></param>
        /// <param name="tranTool"></param>
        /// <returns></returns>
        private async Task HandleMsgAsync(NetWebSocket webSocket,WebSocketMessageTransmissionTool tranTool)
        {
            //var AuthorizedClients = new ConcurrentDictionary<string, WebSocketAuthorizedHelper>();
            async Task sendasync(string text)
            {
                await SocketExtension.SendAsync(webSocket, text); // 回复客户端，处理方法中入参如果需要发送消息委托，则将该回调方法作为委托参数传入
            }
            
            ObjectPool<WebSocketHandleContext> contextPool = new ObjectPool<WebSocketHandleContext>(() =>
            {
                var context = new WebSocketHandleContext(sendasync);
                context.OnExceptionTracking = _onExceptionTracking;
                context.OnReplyMakeData = _onReplyMakeData;
                context.OnReply = _onReply;
                return context;
            }, context =>
            {
                context.Reset();
            });

            while (webSocket.State == WebSocketState.Open)
            {
                var message = await tranTool.WaitMsgAsync();  // 有消息时通知
                if (!JsonHelper.TryParse(message, out var jsonReques))
                {
                    Console.WriteLine($"WebSocket 消息解析失败: {message}");
                    continue;
                }
                var context = contextPool.Allocate();
                context.MsgRequest = jsonReques;
                await HandleAsync(context); // 处理消息
                contextPool.Free(context);
            }

        }

        /// <summary>
        /// 异步处理消息
        /// </summary>
        /// <param name="context">此次请求的上下文</param>
        /// <returns></returns>
        private async Task HandleAsync(WebSocketHandleContext context)
        {
            foreach (var module in _socketModules.Values)
            {
                if (context.Handle)
                {
                    return;
                }
                await module.HandleAsync(context);
            }
            

        }

        /// <summary>
        /// 给所有客户端广播最新数据
        /// </summary>
        /// <param name="latestData"></param>
        /// <returns></returns>
        public async Task PushDataAsync(object latestData)
        {
            //var options = new JsonSerializerOptions
            //{
            //    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            //};
            var json = JsonHelper.Serialize(latestData);
            var buffer = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(buffer);

            List<NetWebSocket> socketsCopy;
            lock (_lock)
            {
                socketsCopy = _sockets.ToList();
            }

            foreach (var socket in socketsCopy)
            {
                if (socket.State == WebSocketState.Open)
                {
                    try
                    {
                        await socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch
                    {
                        // 忽略异常或移除失效连接
                    }
                }
            }
        }

        /// <summary>
        /// 设置回调函数，用于处理外部请求时的回复消息
        /// </summary>
        /// <param name="func"></param>
        public void OnReplyMakeData(Func<WebSocketHandleContext, object, object> func)
        {
            _onReplyMakeData = func;
        }
        /// <summary>
        /// 设置回调函数，回复外部请求时，记录消息内容
        /// </summary>
        /// <param name="onReply"></param>

        public void OnReply(Action<string> onReply)
        {
            _onReply = onReply;
        }
    }

}
