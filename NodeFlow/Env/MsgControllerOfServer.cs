using Newtonsoft.Json.Linq;
using Serein.Library.Api;
using Serein.Library;
using Serein.Library.Network.WebSocketCommunication;
using Serein.Library.Network.WebSocketCommunication.Handle;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Env
{
    /// <summary>
    /// 服务端的消息管理（用于处理客户端的请求）
    /// </summary>
    [AutoSocketModule(ThemeKey = FlowEnvironment.ThemeKey, DataKey = FlowEnvironment.DataKey)]
    public class MsgControllerOfServer : ISocketHandleModule
    {
        /// <summary>
        /// 受控环境
        /// </summary>
        public IFlowEnvironment environment;

        /// <summary>
        /// WebSocket处理
        /// </summary>
        public Guid HandleGuid { get; } = new Guid();

        /// <summary>
        /// <para>表示是否正在控制远程</para>
        /// <para>Local control remote env</para>
        /// </summary>
        public bool IsLcR { get; set; }
        /// <summary>
        /// <para>表示是否受到远程控制</para>
        /// <para>Remote control local env</para>
        /// </summary>
        public bool IsRcL { get; set; }


        /// <summary>
        /// 流程环境远程管理服务
        /// </summary>
        private WebSocketServer FlowEnvRemoteWebSocket;


        /// <summary>
        /// 启动不带Token验证的远程服务
        /// </summary>
        public MsgControllerOfServer(IFlowEnvironment environment)
        {
            this.environment = environment;
            FlowEnvRemoteWebSocket ??= new WebSocketServer();
        }

        /// <summary>
        /// 启动带token验证的远程服务
        /// </summary>
        /// <param name="token"></param>
        public MsgControllerOfServer(IFlowEnvironment environment, string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("当前没有设置token，但使用了token验证的服务端");

            }
            this.environment = environment;
            FlowEnvRemoteWebSocket ??= new WebSocketServer(token, OnInspectionAuthorized);

        }


        #region 基本方法
        /// <summary>
        /// 启动远程
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        public async Task StartRemoteServerAsync(int port = 7525)
        {

            FlowEnvRemoteWebSocket.MsgHandleHelper.AddModule(this,
            (ex, send) =>
            {
                send(new
                {
                    code = 400,
                    ex = ex.Message
                });
            });
            var url = $"http://*:{port}/";
            try
            {
                await FlowEnvRemoteWebSocket.StartAsync(url);
            }
            catch (Exception ex)
            {
                FlowEnvRemoteWebSocket.MsgHandleHelper.RemoveModule(this);
                Console.WriteLine("打开远程管理异常：" + ex);
            }
        }

        /// <summary>
        /// 结束远程管理
        /// </summary>
        [AutoSocketHandle]
        public void StopRemoteServer()
        {
            try
            {
                FlowEnvRemoteWebSocket.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine("结束远程管理异常：" + ex);
            }
        }

        /// <summary>
        /// 验证远程token
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task<bool> OnInspectionAuthorized(dynamic token)
        {
            if (IsLcR)
            {
                return false; // 正在远程控制远程环境时，禁止其它客户端远程控制 
            }
            if (IsRcL)
            {
                return false; // 正在受到远程控制时，禁止其它客户端远程控制 
            }
            await Task.Delay(0);
            var tokenValue = token.ToString();
            if ("123456".Equals(tokenValue))
            {
                // 同时切换远程环境
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 获取发送消息的委托
        /// </summary>
        /// <param name="SendAsync"></param>
        private void OnResultSendMsgFunc(Func<string, Task> SendAsync)
        {
            // 从受控环境向主控环境发送消息。
            Func<string, object, Task> func = async (theme, data) =>
            {
                JObject sendJson = new JObject
                {
                    [FlowEnvironment.ThemeKey] = theme,
                    [FlowEnvironment.DataKey] = JObject.FromObject(data),
                };
                var msg = sendJson.ToString();
                await SendAsync(msg);
            };

            // var remoteEnv = new RemoteFlowEnvironment(func); // 创建一个远程环境
            // OnSwitchedEnvironment.Invoke(remoteEnv); // 通知前台切换到了远程环境
        }
        #endregion

        /// <summary>
        /// 异步运行
        /// </summary>
        /// <returns></returns>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.StartFlow)]
        private async Task StartAsync()
        {
            var uiContextOperation = environment.IOC.Get<UIContextOperation>();
            await environment.StartAsync();
        }

        /// <summary>
        /// 从远程环境运行选定的节点
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <returns></returns>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.StartFlowInSelectNode)]
        private async Task StartAsyncInSelectNode(string nodeGuid)
        {
            await environment.StartAsyncInSelectNode(nodeGuid);
        }

        /// <summary>
        /// 结束流程
        /// </summary>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.ExitFlow)]
        private void ExitFlow()
        {
            environment.ExitFlow();

        }

        /// <summary>
        /// 激活全局触发器
        /// </summary>
        /// <param name="nodeGuid"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.ActivateFlipflopNode)]
        private void ActivateFlipflopNode(string nodeGuid)
        {
            environment.ActivateFlipflopNode(nodeGuid);
        }


        /// <summary>
        /// 关闭全局触发器
        /// </summary>
        /// <param name="nodeGuid"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.TerminateFlipflopNode)]
        private void TerminateFlipflopNode(string nodeGuid)
        {
            environment.TerminateFlipflopNode(nodeGuid);
        }


        /// <summary>
        /// 获取当前环境信息（远程连接）
        /// </summary>
        /// <returns></returns>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.GetEnvInfo)]
        private async Task<FlowEnvInfo> GetEnvInfoAsync()
        {
            return await environment.GetEnvInfoAsync();
        }

        /// <summary>
        /// 加载项目文件
        /// </summary>
        /// <param name="flowEnvInfo">环境信息</param>
        // [AutoSocketHandle(ThemeValue = EnvMsgTheme.GetProjectInfo)]
        private void LoadProject(FlowEnvInfo flowEnvInfo)
        {
            environment.LoadProject(flowEnvInfo, "");
        }


        /// <summary>
        /// 连接远程环境
        /// </summary>
        /// <param name="addres">远程环境地址</param>
        /// <param name="port">远程环境端口</param>
        /// <param name="token">密码</param>
        // [AutoSocketHandle]
        public async Task<(bool, RemoteEnvControl)> ConnectRemoteEnv(string addres, int port, string token)
        {
            return await environment.ConnectRemoteEnv(addres, port, token);
        }



        /// <summary>
        /// 退出远程环境
        /// </summary>
        // [AutoSocketHandle]
        public void ExitRemoteEnv()
        {
            Console.WriteLine("暂未实现远程退出远程环境");
            IsLcR = false;
        }


        /// <summary>
        /// 序列化当前项目的依赖信息、节点信息
        /// </summary>
        /// <returns></returns>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.GetProjectInfo)]
        public async Task<SereinProjectData> GetProjectInfoAsync()
        {
            return await environment.GetProjectInfoAsync();
        }

        /// <summary>
        /// 从文件路径中加载DLL
        /// </summary>
        /// <param name="dllPath"></param>
        /// <returns></returns> 
        // [AutoSocketHandle(ThemeValue = EnvMsgTheme)]
        public void LoadDll(string dllPath)
        {
        }
        /// <summary>
        /// 移除DLL
        /// </summary>
        /// <param name="assemblyFullName"></param>
        /// <returns></returns>
        // [AutoSocketHandle(ThemeValue = EnvMsgTheme)]
        public bool RemoteDll(string assemblyFullName)
        {
            return false;
        }

        /// <summary>
        /// 从远程环境创建节点
        /// </summary>
        /// <param name="nodeType"></param>
        /// <param name="position"></param>
        /// <param name="mdInfo">如果是表达式节点条件节点，该项为null</param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.CreateNode,ArgNotNull = false)]
        public async Task<NodeInfo> CreateNode([Needful] string nodeType, [Needful] PositionOfUI position, MethodDetailsInfo? mdInfo = null)
        {
            if (!EnumHelper.TryConvertEnum<NodeControlType>(nodeType, out var nodeControlType))
            {
                return null;
            }
           var nodeInfo =  await environment.CreateNodeAsync(nodeControlType, position, mdInfo); // 监听到客户端创建节点的请求
            return nodeInfo;
        }
        /// <summary>
        /// 从远程环境移除节点
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <exception cref="NotImplementedException"></exception>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.RemoveNode)]
        public async Task<object> RemoveNode(string nodeGuid)
        {
            //var result = environment.RemoveNodeAsync(nodeGuid).GetAwaiter().GetResult();
            var result = await environment.RemoveNodeAsync(nodeGuid);
            //return result;
            return new
            {
                state = result
            };
        }


        /// <summary>
        /// 连接节点
        /// </summary>
        /// <param name="fromNodeGuid">起始节点</param>
        /// <param name="toNodeGuid">目标节点</param>
        /// <param name="connectionType">连接关系</param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.ConnectNode)]
        public async Task<object> ConnectNode(string fromNodeGuid, string toNodeGuid, string connectionType)
        {
            if (!EnumHelper.TryConvertEnum<ConnectionType>(connectionType, out var tmpConnectionType))
            {
                return new
                {
                    state = false
                };
            }
            //environment.ConnectNodeAsync(fromNodeGuid, toNodeGuid, tmpConnectionType);
            var result = await environment.ConnectNodeAsync(fromNodeGuid, toNodeGuid, tmpConnectionType);
            return new
            {
                state = result
            };
        }

        /// <summary>
        /// 移除连接关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点Guid</param>
        /// <param name="toNodeGuid">目标节点Guid</param>
        /// <param name="connectionType">连接关系</param>
        /// <exception cref="NotImplementedException"></exception>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.RemoveConnect)]
        public async Task<object> RemoveConnect(string fromNodeGuid, string toNodeGuid, string connectionType)
        {
            if (!EnumHelper.TryConvertEnum<ConnectionType>(connectionType, out var tmpConnectionType))
            {
                return new
                {
                    state = false
                };
            }

            var result = await environment.RemoveConnectAsync(fromNodeGuid, toNodeGuid, tmpConnectionType);
            return new
            {
                state = result
            };
        }

        /// <summary>
        /// 移动了某个节点(远程插件使用）
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.MoveNode)]
        public void MoveNode(string nodeGuid, double x, double y)
        {
            environment.MoveNode(nodeGuid, x, y);
        }

        /// <summary>
        /// 设置起点控件
        /// </summary>
        /// <param name="nodeGuid"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.SetStartNode)]
        public void SetStartNode(string nodeGuid)
        {
            environment.SetStartNode(nodeGuid);
        }



        /// <summary>
        /// 中断指定节点，并指定中断等级。
        /// </summary>
        /// <param name="nodeGuid">被中断的目标节点Guid</param>
        /// <param name="interruptClass">中断级别</param>
        /// <returns>操作是否成功</returns>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.SetNodeInterrupt)]
        public async Task<bool> SetNodeInterruptAsync(string nodeGuid, string interruptClass)
        {
            
            if (!EnumHelper.TryConvertEnum<InterruptClass>(interruptClass, out var @class))
            {
                return false;
            }
            
            return await this.environment.SetNodeInterruptAsync(nodeGuid, @class);
            
        }



        /// <summary>
        /// 添加表达式中断
        /// </summary>
        /// <param name="key">如果是节点，传入Guid；如果是对象，传入类型FullName</param>
        /// <param name="expression">合法的条件表达式</param>
        /// <returns></returns>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.AddInterruptExpression)]
        public async Task<bool> AddInterruptExpression(string key, string expression)
        {
            return await environment.AddInterruptExpressionAsync(key, expression);
        }
        /// <summary>
        /// 设置对象的监视状态
        /// </summary>
        /// <param name="key">如果是节点，传入Guid；如果是对象，传入类型FullName</param>
        /// <param name="isMonitor">ture监视对象；false取消对象监视</param>
        /// <returns></returns>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.SetMonitor)]
        public void SetMonitorObjState(string key, bool isMonitor)
        {
            environment.SetMonitorObjState(key, isMonitor);
        }

        /// <summary>
        /// 节点数据更改
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="path"></param>
        /// <param name="value"></param>
        [AutoSocketHandle(ThemeValue = EnvMsgTheme.ValueNotification)]
        public async Task ValueNotification(string nodeGuid, string path, string value)
        {
           await environment.NotificationNodeValueChangeAsync(nodeGuid, path, value);
        }






    }


}
