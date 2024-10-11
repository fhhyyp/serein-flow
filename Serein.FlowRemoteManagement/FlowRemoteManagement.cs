
using Serein.Library;
using Serein.Library.Entity;
using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.Enums;
using Serein.Library.Network.WebSocketCommunication;
using System.Security.Cryptography.X509Certificates;
using Serein.NodeFlow;
using Serein.Library.Core.NodeFlow;
using Serein.Library.NodeFlow.Tool;
using Serein.Library.Utils;
using Serein.FlowRemoteManagement.Model;

namespace SereinFlowRemoteManagement
{
    public enum FlowEnvCommand
    {
        A,
        B,
        C,
        D
    }


    /// <summary>
    /// SereinFlow 远程管理模块
    /// </summary>
    [DynamicFlow]
    [AutoRegister]
    [AutoSocketModule(ThemeKey ="theme",DataKey ="data")]
    public class FlowRemoteManagement : FlowTrigger<FlowEnvCommand>, ISocketHandleModule
    {
        #region 初始化
        public Guid HandleGuid { get; } = new Guid();

        private readonly  FlowEnvironment environment;
        public FlowRemoteManagement(IFlowEnvironment environment) 
        {
            if(environment is FlowEnvironment env)
            {
                this.environment = env;
            }
            else
            {
                throw new Exception();
            }
        }

        [NodeAction(NodeType.Init)]
        public void Init(IDynamicContext context)
        {
            environment.IOC.Register<WebSocketServer>();
        }

        [NodeAction(NodeType.Loading)]
        public void Loading(IDynamicContext context)
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
                await socketServer.StartAsync("http://*:7525/");
            });
            SereinProjectData projectData = environment.SaveProject();
        } 
        #endregion

        #region 对外接口

        /// <summary>
        /// 更改两个节点的连接关系
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
                environment.ConnectNode(nodeInfo.FromNodeGuid, nodeInfo.ToNodeGuid, connectionType);
            }
            else
            {
                environment.RemoteConnect(nodeInfo.FromNodeGuid, nodeInfo.ToNodeGuid, connectionType);
            }
        }

         /// <summary>
        /// 远程调用某个节点
        /// </summary>
        [AutoSocketHandle(ThemeValue = "InvokeNode")]
        public async Task InvokeNode(bool isBranchEx, string nodeGuid, Func<object, Task> Send)
        {
            if (string.IsNullOrEmpty(nodeGuid))
            {
                throw new InvalidOperationException("Guid错误");
            }
            if(!environment.Nodes.TryGetValue(nodeGuid, out var nodeModel) )
            {
                throw new InvalidOperationException("不存在这样的节点");
            }
            IDynamicContext dynamicContext = new DynamicContext(environment);
            object? result = null;
            if(isBranchEx)
            {
                await nodeModel.StartFlowAsync(dynamicContext);
            }
            else
            {
                result = await nodeModel.ExecutingAsync(dynamicContext);
            }
           
            if(result is not Task)
            {
                await Send(new
                {
                    state = 200,
                    tips = "执行完成",
                    data = result
                }) ;
            }
        }

        /// <summary>
        /// 获取项目配置文件信息
        /// </summary>
        [AutoSocketHandle(ThemeValue = "GetProjectInfo")]
        public async Task<SereinProjectData> GetProjectInfo()
        {
            await Task.Delay(0);
            return environment.SaveProject();
        }


        #endregion

        #region 测试节点

        [NodeAction(NodeType.Flipflop, "触发器等待")]
        public async Task<IFlipflopContext<object>> WaitFlipflop(FlowEnvCommand flowEnvCommand)
        {
            var result = await this.CreateTaskAsync<object>(flowEnvCommand);
            return new FlipflopContext<object>(FlipflopStateType.Succeed, result);
        }

        [NodeAction(NodeType.Action, "测试")]
        public void Test()
        {
            Console.WriteLine("Hello World");
        }

        [NodeAction(NodeType.Action, "等待")]
        public async Task Wait(int wait = 5)
        {
            await Task.Delay(1000 * wait);
        }

        [NodeAction(NodeType.Action, "输出")]
        public void Console2(string value)
        {
            Console.WriteLine(value);
        } 
        #endregion



    }
}
