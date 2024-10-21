using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        /// 远程环境配置
        /// </summary>
        public class ControlConfiguration
        {
            /// <summary>
            /// 远程环境的网络地址
            /// </summary>
            public string Addres { get; set; }

            /// <summary>
            /// 远程环境的对外端口
            /// </summary>
            public int Port { get; set; }

            /// <summary>
            /// 登录远程环境必须携带的token(可以为可序列化的JSON对象)
            /// </summary>
            public object Token { get; set; }

            /// <summary>
            /// 有关消息ID的 Json Key
            /// </summary>
            public string MsgIdJsonKey { get; set; }
            /// <summary>
            /// 有关消息主题的 Json Key
            /// </summary>
            public string ThemeJsonKey { get; set; }
            /// <summary>
            /// 有关数据的 Json Key
            /// </summary>
            public string DataJsonKey { get; set; }
        }

        /// <summary>
        /// 配置远程连接IP端口
        /// </summary>
        public RemoteEnvControl(ControlConfiguration controlConfiguration)
        {
            Config = controlConfiguration;
        }

        /// <summary>
        /// 配置信息
        /// </summary>
        public ControlConfiguration Config { get; }




        /// <summary>
        /// 连接到远程的客户端
        /// </summary>
        public WebSocketClient EnvClient { get; } = new WebSocketClient();

        /// <summary>
        /// 是否连接到了远程环境
        /// </summary>
        //public bool IsConnectdRemoteEnv { get => isConnectdRemoteEnv; }
        //private bool isConnectdRemoteEnv = false;

        /// <summary>
        /// 尝试连接到远程环境
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ConnectAsync()
        {
            // 第2种，WebSocket连接到远程环境，实时接收远程环境的响应？
            Console.WriteLine($"准备连接：{Config.Addres}:{Config.Port},{Config.Token}");
            bool success = false;
            try
            {
                var tcpClient = new TcpClient();
                var result = tcpClient.BeginConnect(Config.Addres, Config.Port, null, null);
                success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3));
            }
            finally
            {

            }
            if (!success)
            {
                Console.WriteLine($"无法连通远程端口 {Config.Addres}:{Config.Port}");
                return false;
            }
            else
            {
                var url = $"ws://{Config.Addres}:{Config.Port}/";
                var result = await EnvClient.ConnectAsync(url); // 尝试连接远程环境
                //this.isConnectdRemoteEnv = result;
                return result;
            }
        }



        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="msgId"></param>
        /// <param name="theme"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task SendAsync(string msgId , string theme, object data)
        {
            //var sendMsg = new
            //{
            //    theme = theme,
            //    token = this.Token,
            //    data = data,
            //};
            //var msg = JsonConvert.SerializeObject(sendMsg);
            JObject jsonData;

            if (data is null)
            {
                jsonData = new JObject()
                {
                    [Config.MsgIdJsonKey] = msgId,
                    [Config.ThemeJsonKey] = theme,
                };
            }
            else
            {
                JToken dataToken;
                if (data is System.Collections.IEnumerable || data is Array)
                {
                    dataToken = JArray.FromObject(data);
                }
                else
                {
                    dataToken = JObject.FromObject(data);
                }

                jsonData = new JObject()
                {
                    [Config.MsgIdJsonKey] = msgId,
                    [Config.ThemeJsonKey] = theme,
                    [Config.DataJsonKey] = dataToken
                };
            }
           
            var msg = jsonData.ToString();
            //Console.WriteLine(msg);
            //Console.WriteLine();

            await EnvClient.SendAsync(msg);
        }









    }


}
