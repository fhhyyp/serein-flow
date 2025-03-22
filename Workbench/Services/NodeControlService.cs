
using Newtonsoft.Json;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow;
using Serein.NodeFlow.Env;
using Serein.Workbench.Api;
using Serein.Workbench.Avalonia.Api;
using Serein.Workbench.Node;
using Serein.Workbench.Node.View;
using Serein.Workbench.Node.ViewModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;





namespace Serein.Workbench.Api
{

}

namespace Serein.Workbench.Services
{
    /// <summary>
    /// 节点操作相关服务
    /// </summary>
    internal class NodeControlService 
    {

        public NodeControlService(IFlowEnvironment flowEnvironment,
                                    IFlowEEForwardingService feefService)
        {
          /*  this.flowEnvironment = flowEnvironment;
            this.feefService = feefService;
            feefService.OnNodeCreate += FeefService_OnNodeCreate; // 订阅运行环境创建节点事件
            feefService.OnNodeConnectChange += FeefService_OnNodeConnectChange; // 订阅运行环境连接了节点事件
            // 手动加载项目
            _ = Task.Run(async delegate
            {
                await Task.Delay(1000);
                var flowEnvironment = new FlowEnvironment();// App.GetService<IFlowEnvironment>();
                var filePath = @"C:\Users\Az\source\repos\CLBanyunqiState\CLBanyunqiState\bin\debug\net8.0\project.dnf";
                string content = System.IO.File.ReadAllText(filePath); // 读取整个文件内容
                var projectData = JsonConvert.DeserializeObject<SereinProjectData>(content);
                var projectDfilePath = System.IO.Path.GetDirectoryName(filePath)!;
                flowEnvironment.LoadProject(new FlowEnvInfo { Project = projectData }, projectDfilePath);
            }, CancellationToken.None);*/
        }





    }

}
