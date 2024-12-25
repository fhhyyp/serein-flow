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
            SereinEnv.WriteLine(InfoType.INFO, $"[{msgId}] => {theme}");
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
            _ = SendCommandAsync(msgId, theme, data); // 客户端发送消息
            var result = await remoteFlowEnvironment.WaitTriggerAsync<TResult>(msgId);
            if (result.Type == TriggerDescription.Overtime)
            {
                throw new Exception($"主题【{theme}】异常，服务端未响应");
            }
            else if (result.Type == TriggerDescription.TypeInconsistency)
            {
                throw new Exception($"主题【{theme}】异常，服务端返回数据类型与预期不一致{result.Value?.GetType()}");
            }
            return result.Value;
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
            _ = remoteFlowEnvironment.InvokeTriggerAsync(msgId, flowEnvInfo);
        }


        /// <summary>
        /// 远程环境发来项目信息
        /// </summary>
        /// <param name="msgId"></param>
        /// <param name="sereinProjectData"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.GetProjectInfo, IsReturnValue = false)]
        public void GetProjectInfo([UseMsgId] string msgId, [UseData] SereinProjectData sereinProjectData)
        {
            _ = remoteFlowEnvironment.InvokeTriggerAsync(msgId, sereinProjectData);
        }

        /// <summary>
        /// 开始流程
        /// </summary>
        /// <param name="msgId"></param>
        /// <param name="state"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.StartFlow, IsReturnValue = false)]
        public void StartFlow([UseMsgId] string msgId, bool state)
        {
            _ = remoteFlowEnvironment.InvokeTriggerAsync(msgId, state);
        }
        /// <summary>
        /// 结束流程
        /// </summary>
        /// <param name="msgId"></param>
        /// <param name="state"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.ExitFlow, IsReturnValue = false)]
        public void ExitFlow([UseMsgId] string msgId, bool state)
        {
            _ = remoteFlowEnvironment.InvokeTriggerAsync(msgId, state);
        }

        /// <summary>
        /// 设置了某个节点为起始节点
        /// </summary>
        /// <param name="msgId"></param>
        /// <param name="nodeGuid">节点Guid</param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.SetStartNode, IsReturnValue = false)]
        public void SetStartNode([UseMsgId] string msgId, string nodeGuid)
        {
            _ = remoteFlowEnvironment.InvokeTriggerAsync(msgId, nodeGuid);
        }




        /// <summary>
        /// 从某个节点开始运行
        /// </summary>
        /// <param name="msgId"></param>
        /// <param name="state"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.StartFlowInSelectNode, IsReturnValue = false)]
        public void StartFlowInSelectNode([UseMsgId] string msgId, bool state)
        {
            _ = remoteFlowEnvironment.InvokeTriggerAsync(msgId, state);
        }

        /// <summary>
        /// 设置节点的中断
        /// </summary>
        /// <param name="msgId"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.SetNodeInterrupt, IsReturnValue = false)]
        public void SetNodeInterrupt([UseMsgId] string msgId)
        {
            _ = remoteFlowEnvironment.InvokeTriggerAsync<object>(msgId, null);
        }

        /// <summary>
        /// 添加中断监视表达式
        /// </summary>
        /// <param name="msgId"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.AddInterruptExpression, IsReturnValue = false)]
        public void AddInterruptExpression([UseMsgId] string msgId)
        {
            _ = remoteFlowEnvironment.InvokeTriggerAsync<object>(msgId, null);
        }


        /// <summary>
        /// 创建节点
        /// </summary>
        /// <param name="msgId"></param>
        /// <param name="nodeInfo"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.CreateNode, IsReturnValue = false)]
        public void CreateNode([UseMsgId] string msgId, [UseData] NodeInfo nodeInfo)
        {
            _ = remoteFlowEnvironment.InvokeTriggerAsync(msgId, nodeInfo);
        }

        /// <summary>
        /// 移除节点
        /// </summary>
        /// <param name="msgId"></param>
        /// <param name="state"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.RemoveNode, IsReturnValue = false)]
        public void RemoveNode([UseMsgId] string msgId, bool state)
        {
            _ = remoteFlowEnvironment.InvokeTriggerAsync(msgId, state);
        }

        /// <summary>
        /// 创建节点之间的调用关系
        /// </summary>
        /// <param name="msgId"></param>
        /// <param name="state"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.ConnectInvokeNode, IsReturnValue = false)]
        public void ConnectInvokeNode([UseMsgId] string msgId, bool state)
        {
            _ = remoteFlowEnvironment.InvokeTriggerAsync(msgId, state);
        }

        /// <summary>
        /// 移除节点之间的调用关系
        /// </summary>
        /// <param name="msgId"></param>
        /// <param name="state"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.RemoveInvokeConnect, IsReturnValue = false)]
        public void RemoveInvokeConnect([UseMsgId] string msgId, bool state)
        {
            _ = remoteFlowEnvironment.InvokeTriggerAsync(msgId, state);
        }
        
        /// <summary>
        /// 创建节点之间参数获取关系
        /// </summary>
        /// <param name="msgId"></param>
        /// <param name="state"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.ConnectArgSourceNode, IsReturnValue = false)]
        public void ConnectArgSourceNode([UseMsgId] string msgId, bool state)
        {
            _ = remoteFlowEnvironment.InvokeTriggerAsync(msgId, state);
        }

        /// <summary>
        /// 移除节点之间参数获取关系
        /// </summary>
        /// <param name="msgId"></param>
        /// <param name="state"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.RemoveArgSourceConnect, IsReturnValue = false)]
        public void RemoveArgSourceConnect([UseMsgId] string msgId, bool state)
        {
            _ = remoteFlowEnvironment.InvokeTriggerAsync(msgId, state);
        }

        /// <summary>
        /// 改变参数
        /// </summary>
        /// <param name="msgId"></param>
        /// <param name="state"></param>
         [AutoSocketHandle(ThemeValue = EnvMsgTheme.ChangeParameter, IsReturnValue = false)]
        public void ChangeParameter([UseMsgId] string msgId, bool state)
        {
            _ = remoteFlowEnvironment.InvokeTriggerAsync(msgId, state);
        }
        
       
        #endregion

    }

}
