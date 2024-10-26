
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Network.WebSocketCommunication;
using System.Security.Cryptography.X509Certificates;
using Serein.NodeFlow;
using Serein.Library.Core.NodeFlow;
using Serein.Library.Utils;
using Serein.FlowRemoteManagement.Model;
using System.Reflection;
using Serein.Library.FlowNode;

namespace SereinFlowRemoteManagement
{


    /// <summary>
    /// SereinFlow 远程控制模块
    /// </summary>
    [DynamicFlow]
    [AutoRegister]
    [AutoSocketModule(ThemeKey ="theme",DataKey ="data")]
    public class SereinFlowRemoteControl :  ISocketHandleModule
    {
        public int ServerPort { get; set; } = 7525;

        #region 初始化服务端
        public Guid HandleGuid { get; } = new Guid();

        private readonly IFlowEnvironment environment;
        public SereinFlowRemoteControl(IFlowEnvironment environment) 
        {
            this.environment = environment;
        }

        [NodeAction(NodeType.Init)]
        public void Init(IDynamicContext context)
        {
            environment.IOC.Register<WebSocketServer>();
        }

        [NodeAction(NodeType.Loading)]
        public async Task Loading(IDynamicContext context)
        {
            environment.IOC.Run<WebSocketServer>(async (socketServer) =>
            {
                socketServer.MsgHandleHelper.AddModule(this,
                (ex, send) =>
                {
                    send(new
                    {
                        code = 400,
                        ex = ex.Message
                    });
                });
                await Console.Out.WriteLineAsync("启动远程管理模块");
                await socketServer.StartAsync($"http://*:{ServerPort}/");
            });
            SereinProjectData projectData = await environment.GetProjectInfoAsync();
        }
        #endregion

        #region 流程运行接口

        /// <summary>
        /// 连接到运行环境，获取当前的节点信息
        /// </summary>
        /// <param name="Send"></param>
        /// <returns></returns>
        [AutoSocketHandle]
        public async Task<object?> ConnectWorkBench(Func<string, Task> Send)
        {
            await Send("尝试获取");

            try
            {
                var envInfo =  this.environment.GetEnvInfoAsync();
                return envInfo;
            }
            catch (Exception ex)
            {
                await Send(ex.Message);
                return null;
            }
        }

        public void AddNode(string nodeType,string methodName,int x, int y)
        {
            if(x <= 0 || y <= 0)
            {
                throw new InvalidOperationException("坐标错误");
            }
            if (!EnumHelper.TryConvertEnum<NodeControlType>(nodeType, out var connectionType))
            {
                throw new InvalidOperationException("类型错误");
            }

            if (this.environment.TryGetMethodDetailsInfo(methodName,out var mdInfo))
            {
                this.environment.CreateNode(connectionType, new PositionOfUI(x, y), mdInfo);  // 
            }


        }

        /// <summary>
        /// 远程更改两个节点的连接关系
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <param name="Send"></param>
        /// <exception cref="InvalidOperationException"></exception>
        [AutoSocketHandle(ThemeValue = "ConnectionChange")]
        public void ChangeNodeConnection(ConnectionInfoData nodeInfo, Func<object, Task> Send)
        {
            if (string.IsNullOrEmpty(nodeInfo.FromNodeGuid) || string.IsNullOrEmpty(nodeInfo.ToNodeGuid))
            {
                throw new InvalidOperationException("Guid错误");
            }
            if (!EnumHelper.TryConvertEnum<ConnectionType>(nodeInfo.Type, out var connectionType))
            {
                throw new InvalidOperationException("类型错误");
            }

            if (nodeInfo.Op)
            {
                environment.ConnectNodeAsync(nodeInfo.FromNodeGuid, nodeInfo.ToNodeGuid, connectionType);
            }
            else
            {
                environment.RemoveConnect(nodeInfo.FromNodeGuid, nodeInfo.ToNodeGuid, connectionType);
            }
        }

         /// <summary>
        /// 远程调用某个节点
        /// </summary>
        [AutoSocketHandle(ThemeValue = "InvokeNode")]
        public async Task InvokeNode(string nodeGuid, Func<object, Task> Send)
        {
            if (string.IsNullOrEmpty(nodeGuid))
            {
                throw new InvalidOperationException("Guid错误");
            }

            await environment.StartAsyncInSelectNode(nodeGuid);

            await Send(new
            {
                state = 200,
                tips = "执行完成",
            });
        }

        /// <summary>
        /// 获取项目配置文件信息
        /// </summary>
        [AutoSocketHandle(ThemeValue = "GetProjectInfo")]
        public async Task<SereinProjectData> GetProjectInfo()
        {
            await Task.Delay(0);
            return await environment.GetProjectInfoAsync();
        }

        
        #endregion
    }
}
