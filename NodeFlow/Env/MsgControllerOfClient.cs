using Newtonsoft.Json;
using Serein.Library;
using Serein.Library.Network.WebSocketCommunication;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Env
{



    /// <summary>
    /// 客户端的消息管理（用于处理服务端的响应）
    /// </summary>

    [AutoSocketModule(ThemeKey = FlowEnvironment.ThemeKey, DataKey = FlowEnvironment.DataKey)]
    public class MsgControllerOfClient : ISocketHandleModule
    {
        public Guid HandleGuid => new Guid();
        private readonly Func<string, object?, Task> SendCommandAsync;
        private readonly RemoteFlowEnvironment remoteFlowEnvironment;

        public MsgControllerOfClient(RemoteFlowEnvironment remoteFlowEnvironment, Func<string, object?, Task> func)
        {
            this.remoteFlowEnvironment = remoteFlowEnvironment;
            SendCommandAsync = func;
        }


        /// <summary>
        /// 发送请求并等待远程环境响应
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException">超时触发</exception>
        public async Task SendAsync(string signal, object? sendData = null, int overtimeInMs = 100)
        {
            //Console.WriteLine($"指令[{signal}]，value：{JsonConvert.SerializeObject(sendData)}");
            if (!DebounceHelper.CanExecute(signal, overtimeInMs))
            {
                return;
            }
            await SendCommandAsync.Invoke(signal, sendData);
        }

        /// <summary>
        /// 发送请求并等待远程环境响应
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException">超时触发</exception>
        public async Task<TResult> SendAndWaitDataAsync<TResult>(string signal, object? sendData = null, int overtimeInMs = 50)
        {
            //Console.WriteLine($"指令[{signal}]，value：{JsonConvert.SerializeObject(sendData)}");
            _ = SendCommandAsync.Invoke(signal, sendData);
            return await remoteFlowEnvironment.WaitData<TResult>(signal);

            //if (DebounceHelper.CanExecute(signal, overtimeInMs))
            //{
            //    _ = SendCommandAsync.Invoke(signal, sendData);
            //    return await remoteFlowEnvironment.WaitData<TResult>(signal);

            //    //(var type, var result) = await remoteFlowEnvironment.WaitDataWithTimeoutAsync<TResult>(signal, TimeSpan.FromSeconds(150));
            //    //if (type == TriggerType.Overtime)
            //    //{
            //    //    throw new NotImplementedException("超时触发");
            //    //}
            //    //else
            //    //{
            //    //    return result;
            //    //}
            //}
            //else
            //{
            //    return default;
            //} 


        }


        #region 消息接收

        /// <summary>
        /// 远程环境发来项目信息
        /// </summary>
        /// <param name="flowEnvInfo"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.GetEnvInfo)]
        public void GetEnvInfo([UseMsgData] FlowEnvInfo flowEnvInfo)
        {
            remoteFlowEnvironment.TriggerSignal(EnvMsgTheme.GetEnvInfo, flowEnvInfo);
        }




        /// <summary>
        /// 远程环境发来项目信息
        /// </summary>
        /// <param name="sereinProjectData"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.GetProjectInfo)]
        public void GetProjectInfo([UseMsgData] SereinProjectData sereinProjectData)
        {
            remoteFlowEnvironment.TriggerSignal(EnvMsgTheme.GetProjectInfo, sereinProjectData);
        }

        [AutoSocketHandle(ThemeValue = EnvMsgTheme.SetNodeInterrupt)]
        public void SetNodeInterrupt()
        {
            remoteFlowEnvironment.TriggerSignal(EnvMsgTheme.GetProjectInfo, null);
        }

        [AutoSocketHandle(ThemeValue = EnvMsgTheme.AddInterruptExpression)]
        public void AddInterruptExpression()
        {
            remoteFlowEnvironment.TriggerSignal(EnvMsgTheme.AddInterruptExpression, null);
        }



        [AutoSocketHandle(ThemeValue = EnvMsgTheme.CreateNode)]
        public void CreateNode([UseMsgData] NodeInfo nodeInfo)
        {
            remoteFlowEnvironment.TriggerSignal(EnvMsgTheme.CreateNode, nodeInfo);
        }

        [AutoSocketHandle(ThemeValue = EnvMsgTheme.RemoveNode)]
        public void RemoveNode(bool state)
        {
            remoteFlowEnvironment.TriggerSignal(EnvMsgTheme.RemoveNode, state);
        }


        [AutoSocketHandle(ThemeValue = EnvMsgTheme.ConnectNode)]
        public void ConnectNode(bool state)
        {
            remoteFlowEnvironment.TriggerSignal(EnvMsgTheme.ConnectNode, state);
        }

        [AutoSocketHandle(ThemeValue = EnvMsgTheme.RemoveConnect)]
        public void RemoveConnect(bool state)
        {
            remoteFlowEnvironment.TriggerSignal(EnvMsgTheme.RemoveConnect, state);
        }
        
       


        #endregion

    }

}
