using Serein.Proto.WebSocket.Handle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetWebSocket = System.Net.WebSockets.WebSocket;
namespace Serein.Proto.WebSocket
{
    /// <summary>
    /// WebSocket服务
    /// </summary>
    public interface ISereinWebSocketService
    {
        /// <summary>
        /// 目前有多少个连接
        /// </summary>
        int ConcetionCount { get; }

        /// <summary>
        /// 添加处理模块
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        ISereinWebSocketService AddHandleModule<T>() where T : ISocketHandleModule, new();

        /// <summary>
        /// 添加处理模块
        /// </summary>
        /// <param name="socketHandleModule">接口实例</param>
        /// <returns></returns>
        ISereinWebSocketService AddHandleModule(ISocketHandleModule socketHandleModule);

        /// <summary>
        /// 添加处理模块
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instanceFactory">使用指定的实例</param>
        /// <returns></returns>
        ISereinWebSocketService AddHandleModule<T>(Func<ISocketHandleModule> instanceFactory) where T : ISocketHandleModule;

        /// <summary>
        /// 跟踪未处理的异常
        /// </summary>
        /// <param name="onExceptionTracking"></param>
        /// <returns></returns>
        ISereinWebSocketService TrackUnhandledExceptions(Action<Exception> onExceptionTracking);

        /// <summary>
        /// 添加新的 WebSocket 连接进行处理消息
        /// </summary>
        /// <param name="webSocket"></param>
        Task AddWebSocketHandleAsync(NetWebSocket webSocket);

        /// <summary>
        /// 推送消息
        /// </summary>
        /// <param name="latestData"></param>
        /// <returns></returns>
        Task PushDataAsync(object latestData);

        /// <summary>
        /// 设置回调函数，用于处理外部请求时的回复消息
        /// </summary>
        /// <param name="func"></param>
        void OnReplyMakeData(Func<WebSocketHandleContext, object, object> func);

        /// <summary>
        /// 设置回调函数，回复外部请求时，记录消息内容
        /// </summary>
        /// <param name="onReply"></param>
        void OnReply(Action<string> onReply);
    }
}
