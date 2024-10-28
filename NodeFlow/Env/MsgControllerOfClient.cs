using Newtonsoft.Json;
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

        /// <summary>
        /// 处理需要返回的消息
        /// </summary>
        /// <param name="msgId"></param>
        /// <param name="theme"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private async Task SendCommandAsync(string msgId, string theme, object? data)
        {
            await SendCommandFunc.Invoke(msgId, theme, data);
        }



        /// <summary>
        /// 发送请求
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException">超时触发</exception>
        public async Task SendAsync(string theme, object? data = null, int overtimeInMs = 100)
        {

            var msgId = MsgIdHelper.GenerateId().ToString();
            Console.WriteLine($"[{msgId}] => {theme}");
            await SendCommandAsync(msgId, theme, data); // 客户端发送消息
        }

        /// <summary>
        /// 发送请求并等待远程环境响应
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException">超时触发</exception>
        public async Task<TResult> SendAndWaitDataAsync<TResult>(string theme, object? data = null, int overtimeInMs = 50)
        {
            var msgId = MsgIdHelper.GenerateId().ToString();
            //_ = Task.Run(async () =>
            //{
            //    await Task.Delay(500);
            //});
             await SendCommandAsync(msgId, theme, data); // 客户端发送消息
            return await remoteFlowEnvironment.WaitData<TResult>(msgId);
        }


        #region 消息接收

        /// <summary>
        /// 远程环境发来项目信息
        /// </summary>
        /// <param name="msgId"></param>
        /// <param name="flowEnvInfo"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.GetEnvInfo, IsReturnValue = false)]
        public void GetEnvInfo([UseMsgId] string msgId, [UseData] FlowEnvInfo flowEnvInfo)
        {
            remoteFlowEnvironment.TriggerSignal(msgId, flowEnvInfo);
        }


        /// <summary>
        /// 远程环境发来项目信息
        /// </summary>
        /// <param name="msgId"></param>
        /// <param name="sereinProjectData"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.GetProjectInfo, IsReturnValue = false)]
        public void GetProjectInfo([UseMsgId] string msgId, [UseData] SereinProjectData sereinProjectData)
        {
            remoteFlowEnvironment.TriggerSignal(msgId, sereinProjectData);
        }

        [AutoSocketHandle(ThemeValue = EnvMsgTheme.SetNodeInterrupt, IsReturnValue = false)]
        public void SetNodeInterrupt([UseMsgId] string msgId)
        {
            remoteFlowEnvironment.TriggerSignal(msgId, null);
        }

        [AutoSocketHandle(ThemeValue = EnvMsgTheme.AddInterruptExpression, IsReturnValue = false)]
        public void AddInterruptExpression([UseMsgId] string msgId)
        {
            remoteFlowEnvironment.TriggerSignal(msgId, null);
        }



        [AutoSocketHandle(ThemeValue = EnvMsgTheme.CreateNode, IsReturnValue = false)]
        public void CreateNode([UseMsgId] string msgId, [UseData] NodeInfo nodeInfo)
        {
            remoteFlowEnvironment.TriggerSignal(msgId, nodeInfo);
        }

        [AutoSocketHandle(ThemeValue = EnvMsgTheme.RemoveNode, IsReturnValue = false)]
        public void RemoveNode([UseMsgId] string msgId, bool state)
        {
            remoteFlowEnvironment.TriggerSignal(msgId, state);
        }

        [AutoSocketHandle(ThemeValue = EnvMsgTheme.ConnectInvokeNode, IsReturnValue = false)]
        public void ConnectInvokeNode([UseMsgId] string msgId, bool state)
        {
            remoteFlowEnvironment.TriggerSignal(msgId, state);
        }

        [AutoSocketHandle(ThemeValue = EnvMsgTheme.RemoveInvokeConnect, IsReturnValue = false)]
        public void RemoveInvokeConnect([UseMsgId] string msgId, bool state)
        {
            remoteFlowEnvironment.TriggerSignal(msgId, state);
        }
        
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.ConnectArgSourceNode, IsReturnValue = false)]
        public void ConnectArgSourceNode([UseMsgId] string msgId, bool state)
        {
            remoteFlowEnvironment.TriggerSignal(msgId, state);
        }

        [AutoSocketHandle(ThemeValue = EnvMsgTheme.RemoveArgSourceConnect, IsReturnValue = false)]
        public void RemoveArgSourceConnect([UseMsgId] string msgId, bool state)
        {
            remoteFlowEnvironment.TriggerSignal(msgId, state);
        }
        
       


        #endregion

    }

}
