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
        public IFlowEnvironment? Env;
        public bool IsRuning;
        public async Task StartFlow(SereinProjectData flowProjectData, string fileDataPath)
        {
            IsRuning = true;
            SynchronizationContext? uiContext = SynchronizationContext.Current; // 在UI线程上获取UI线程上下文信息
            var uIContextOperation = new UIContextOperation(uiContext); // 封装一个调用UI线程的工具类

            //if (OperatingSystem.IsLinux())
            //{

            //}

            // if (uIContextOperation is null)
            //{
            //    throw new Exception("无法封装 UIContextOperation ");
            //}
            //else
            //{
            //    env = new FlowEnvironmentDecorator(uIContextOperation);
            //    this.window = window;
            //}

            Env = new FlowEnvironmentDecorator();
            Env.SetUIContextOperation(uIContextOperation);
            Env.LoadProject(new FlowEnvInfo { Project = flowProjectData }, fileDataPath); // 加载项目

            if(Env is IFlowEnvironmentEvent @event)
            {
                // 获取环境输出
                @event.OnEnvOut += (infoType, value) =>
                {
                    Console.WriteLine($"{DateTime.Now} [{infoType}] : {value}{Environment.NewLine}");
                };
            }

           

            await Env.StartRemoteServerAsync(7525); // 启动 web socket 监听远程请求

            //await Env.StartAsync();

            IsRuning = false;
        }

    }
}
