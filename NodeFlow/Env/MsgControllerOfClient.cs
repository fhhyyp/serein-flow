using Serein.Library;
using Serein.Library.Network.WebSocketCommunication;
using Serein.Library.Network.WebSocketCommunication.Handle;
using Serein.Library.Utils;

namespace Serein.NodeFlow.Env
{



    /// <summary>
    /// 客户端的消息管理（用于处理服务端的响应）
    /// </summary>

    [AutoSocketModule(ThemeKey = FlowEnvironment.ThemeKey, 
                      DataKey = FlowEnvironment.DataKey,
                      MsgIdKey = FlowEnvironment.MsgIdKey)]
    public class MsgControllerOfClient : ISocketHandleModule
    {
        public Guid HandleGuid => new Guid();

        // 消息主题，data - task等待
        private readonly Func<string, string, object?, Task> SendCommandFunc;
        private readonly RemoteFlowEnvironment remoteFlowEnvironment;

        public MsgControllerOfClient(RemoteFlowEnvironment remoteFlowEnvironment, Func<string, string, object?, Task> func)
        {
            this.remoteFlowEnvironment = remoteFlowEnvironment;
            SendCommandFunc = func;
        }
        private async Task SendCommandAsync(string msgId, string theme, object? data)
        {
            await SendCommandFunc.Invoke(msgId, theme, data);
        }



        /// <summary>
        /// 发送请求
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException">超时触发</exception>
        public async Task SendAsync(string signal, object? data = null, int overtimeInMs = 100)
        {
            //Console.WriteLine($"指令[{signal}]，value：{JsonConvert.SerializeObject(sendData)}");
            if (!DebounceHelper.CanExecute(signal, overtimeInMs))
            {
                return;
            }
            var msgId = MsgIdHelper.GenerateId().ToString();
            await SendCommandAsync(msgId, signal, data);
        }

        /// <summary>
        /// 发送请求并等待远程环境响应
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException">超时触发</exception>
        public async Task<TResult> SendAndWaitDataAsync<TResult>(string theme, object? data = null, int overtimeInMs = 50)
        {
            //Console.WriteLine($"指令[{signal}]，value：{JsonConvert.SerializeObject(sendData)}");

            var msgId = MsgIdHelper.GenerateId().ToString();
            _ = SendCommandAsync(msgId, theme, data);
            return await remoteFlowEnvironment.WaitData<TResult>(msgId);

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
        /// <param name="msgId"></param>
        /// <param name="flowEnvInfo"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.GetEnvInfo)]
        public void GetEnvInfo([UseMsgId] string msgId, [UseData] FlowEnvInfo flowEnvInfo)
        {
            remoteFlowEnvironment.TriggerSignal(msgId, flowEnvInfo);
        }


        /// <summary>
        /// 远程环境发来项目信息
        /// </summary>
        /// <param name="msgId"></param>
        /// <param name="sereinProjectData"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.GetProjectInfo)]
        public void GetProjectInfo([UseMsgId] string msgId, [UseData] SereinProjectData sereinProjectData)
        {
            remoteFlowEnvironment.TriggerSignal(msgId, sereinProjectData);
        }

        [AutoSocketHandle(ThemeValue = EnvMsgTheme.SetNodeInterrupt)]
        public void SetNodeInterrupt([UseMsgId] string msgId)
        {
            remoteFlowEnvironment.TriggerSignal(msgId, null);
        }

        [AutoSocketHandle(ThemeValue = EnvMsgTheme.AddInterruptExpression)]
        public void AddInterruptExpression([UseMsgId] string msgId)
        {
            remoteFlowEnvironment.TriggerSignal(msgId, null);
        }



        [AutoSocketHandle(ThemeValue = EnvMsgTheme.CreateNode)]
        public void CreateNode([UseMsgId] string msgId, [UseData] NodeInfo nodeInfo)
        {
            remoteFlowEnvironment.TriggerSignal(msgId, nodeInfo);
        }

        [AutoSocketHandle(ThemeValue = EnvMsgTheme.RemoveNode)]
        public void RemoveNode([UseMsgId] string msgId, bool state)
        {
            remoteFlowEnvironment.TriggerSignal(msgId, state);
        }


        [AutoSocketHandle(ThemeValue = EnvMsgTheme.ConnectNode)]
        public void ConnectNode([UseMsgId] string msgId, bool state)
        {
            remoteFlowEnvironment.TriggerSignal(msgId, state);
        }

        [AutoSocketHandle(ThemeValue = EnvMsgTheme.RemoveConnect)]
        public void RemoveConnect([UseMsgId] string msgId, bool state)
        {
            remoteFlowEnvironment.TriggerSignal(msgId, state);
        }
        
       


        #endregion

    }

}
