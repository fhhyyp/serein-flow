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
    public class MsgQueueUtil 
    {
        public ConcurrentQueue<string> Msgs = new ConcurrentQueue<string>();

        private readonly Channel<string> _msgChannel;
        public MsgQueueUtil()
        {
            _msgChannel = CreateChannel();
        }

        private Channel<string> CreateChannel()
        {
            return Channel.CreateBounded<string>(new BoundedChannelOptions(100)
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
           var state = await _msgChannel.Reader.ReadAsync();
           return state;
        }
       
        public void WriteMsg(string msg)
        {
            //Msgs.Enqueue(msg);
            Console.WriteLine($"{DateTime.Now}{msg}{Environment.NewLine}");
            _ = _msgChannel.Writer.WriteAsync(msg);
        }

        public bool TryGetMsg(out string msg)
        {
            return Msgs.TryDequeue(out msg);
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
