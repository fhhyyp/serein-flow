
using Serein.Library;
using Serein.Library.Entity;
using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.Enums;
using Serein.Library.Network.WebSocketCommunication;
using System.Security.Cryptography.X509Certificates;

namespace SereinFlowRemoteManagement
{
    [DynamicFlow]
    [AutoRegister]
    public class FlowRemoteManagement
    {
        private readonly IFlowEnvironment environment;
        public FlowRemoteManagement(IFlowEnvironment environment) 
        {
            this.environment = environment;
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
                await socketServer.StartAsync("http://*:7525/");
            });
            SereinProjectData projectData = environment.SaveProject();
        }

        


    }
}
