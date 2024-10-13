
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
using System.Reflection;

namespace SereinFlowRemoteManagement
{


    /// <summary>
    /// SereinFlow 远程管理模块
    /// </summary>
    [DynamicFlow]
    [AutoRegister]
    [AutoSocketModule(ThemeKey ="theme",DataKey ="data")]
    public class FlowRemoteManagement :  ISocketHandleModule
    {
        #region 初始化
        public Guid HandleGuid { get; } = new Guid();

        private readonly FlowEnvironment environment;
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
                await Console.Out.WriteLineAsync("启动远程管理模块");
                await socketServer.StartAsync("http://*:7525/");
            });
            SereinProjectData projectData = environment.SaveProject();
        } 
        #endregion

        #region 对外接口

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
        public async Task InvokeNode(string nodeGuid, Func<object, Task> Send)
        {
            if (string.IsNullOrEmpty(nodeGuid))
            {
                throw new InvalidOperationException("Guid错误");
            }

            await environment.StartFlowInSelectNodeAsync(nodeGuid);

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
            return environment.SaveProject();
        }


        /// <summary>
        /// 连接到运行环境，获取当前的节点信息
        /// </summary>
        /// <param name="Send"></param>
        /// <returns></returns>
        [AutoSocketHandle]
        public async Task<object?> ConnectWorkBench(Func<string, Task> Send)
        {
            await Send("尝试获取");

            Dictionary<NodeLibrary, List<MethodDetailsInfo>> LibraryMds = [];

            foreach (var mdskv in environment.MethodDetailss)
            {
                var library = mdskv.Key;
                var mds = mdskv.Value;
                foreach (var md in mds)
                {
                    if(!LibraryMds.TryGetValue(library, out var t_mds))
                    {
                        t_mds = new List<MethodDetailsInfo>();
                        LibraryMds[library] = t_mds;
                    }
                    var mdInfo = md.ToInfo();
                    mdInfo.LibraryName = library.Assembly.GetName().FullName;
                    t_mds.Add(mdInfo);
                }
            }
            try
            {
                var project = await GetProjectInfo();
                return new
                {
                    project = project,
                    envNode = LibraryMds.Values,
                };
            }
            catch (Exception ex)
            {
                await Send(ex.Message);
                return null;
            }
        }
        #endregion
    }
}
