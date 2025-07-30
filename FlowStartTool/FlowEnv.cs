using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Env;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.FlowStartTool
{
    internal class FlowEnv
    {
        public readonly IFlowEnvironment flowEnvironment = new FlowEnvironment();
        public bool IsRuning;
        public  void StartFlow(SereinProjectData flowProjectData, string fileDataPath)
        {
            IsRuning = true;
            SynchronizationContext? uiContext = SynchronizationContext.Current; // 在UI线程上获取UI线程上下文信息
            var uIContextOperation = new UIContextOperation(uiContext); // 封装一个调用UI线程的工具类
            flowEnvironment.SetUIContextOperation(uIContextOperation);
            flowEnvironment.LoadProject(fileDataPath); // 加载项目

            flowEnvironment.Event.EnvOutput += (infoType, value) =>
            {
                Console.WriteLine($"{DateTime.Now} [{infoType}] : {value}{Environment.NewLine}");
            };

            //await flowEnvironment.StartRemoteServerAsync(7525); // 启动 web socket 监听远程请求
            IsRuning = false;
        }

    }
}
