using Serein.Library.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Serein.Library.Network.WebSocketCommunication
{
    /// <summary>
    /// 消息处理工具
    /// </summary>
    public class MsgHandleUtil
    {
        private readonly Channel<string> _msgChannel;

        /// <summary>
        /// 初始化优先容器
        /// </summary>
        /// <param name="capacity"></param>
        public MsgHandleUtil(int capacity = 100)
        {
            _msgChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            });
        }
    

        /// <summary>
        /// 等待消息
        /// </summary>
        /// <returns></returns>
        public async Task<string> WaitMsgAsync()
        {
            // 检查是否可以读取消息
            if (await _msgChannel.Reader.WaitToReadAsync())
            {
                return await _msgChannel.Reader.ReadAsync();
            }
            return null; // 若通道关闭，则返回null
        }

        /// <summary>
        /// 写入消息
        /// </summary>
        /// <param name="msg">消息内容</param>
        /// <returns>是否写入成功</returns>
        public async Task<bool> WriteMsgAsync(string msg)
        {
            try
            {
                await _msgChannel.Writer.WriteAsync(msg);
                return true;
            }
            catch (ChannelClosedException)
            {
                // Channel 已关闭
                return false;
            }
        }

        /// <summary>
        /// 尝试关闭通道，停止写入消息
        /// </summary>
        public void CloseChannel()
        {
            _msgChannel.Writer.Complete();
        }
    }




    public class SocketExtension
    {
        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="webSocket"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static async Task SendAsync(WebSocket webSocket, string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
