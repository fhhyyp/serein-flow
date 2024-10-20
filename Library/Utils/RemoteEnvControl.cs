using Newtonsoft.Json;
using Serein.Library.Network.WebSocketCommunication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{
    /// <summary>
    /// 管理远程环境，具备连接、发送消息、停止的功能
    /// </summary>
    public class RemoteEnvControl
    {
        /// <summary>
        /// 配置远程连接IP端口
        /// </summary>
        public RemoteEnvControl(string addres, int port, object token)
        {
            this.Addres = addres;
            this.Port = port;
            this.Token = token;
        }

        /// <summary>
        /// 远程环境的网络地址
        /// </summary>
        public string Addres { get; }

        /// <summary>
        /// 远程环境的对外端口
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// 登录远程环境必须携带的token(可以为可序列化的JSON对象)
        /// </summary>
        public object Token { get; }



        /// <summary>
        /// 连接到远程的客户端
        /// </summary>
        public WebSocketClient EnvClient { get; } = new WebSocketClient();

        /// <summary>
        /// 是否连接到了远程环境
        /// </summary>
        public bool IsConnectdRemoteEnv { get => isConnectdRemoteEnv; }
        private bool isConnectdRemoteEnv = false;

        /// <summary>
        /// 尝试连接到远程环境
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ConnectAsync()
        {
            // 第2种，WebSocket连接到远程环境，实时接收远程环境的响应？
            Console.WriteLine($"准备连接：{Addres}:{Port},{Token}");
            bool success = false;
            try
            {
                var tcpClient = new TcpClient();
                var result = tcpClient.BeginConnect(Addres, Port, null, null);
                success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3));
            }
            finally
            {

            }
            if (!success)
            {
                Console.WriteLine($"无法连通远程端口 {Addres}:{Port}");
                return false;
            }
            else
            {
                var url = $"ws://{Addres}:{Port}/";
                var result = await EnvClient.ConnectAsync(url); // 尝试连接远程环境
                this.isConnectdRemoteEnv = result;
                return result;
            }
        }



        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="theme"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task SendAsync(string theme, object data)
        {
            var sendMsg = new
            {
                theme = theme,
                token = this.Token,
                data = data,
            };
            var msg = JsonConvert.SerializeObject(sendMsg);
            await EnvClient.SendAsync(msg);
        }











    }


}
