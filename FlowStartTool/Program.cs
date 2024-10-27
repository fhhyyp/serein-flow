using Newtonsoft.Json;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Env;
using System.Diagnostics;
using System.Reflection;
using static System.Net.Mime.MediaTypeNames;

namespace Serein.FlowStartTool
{
    public class Program
    {
        public static void Main(string[] args)
        {
#if true
            args = [@"F:\临时\project\linux\project.dnf"];
#endif


            Console.WriteLine("Hello :) ");
            Console.WriteLine($"args : {string.Join(" , ", args)}");
            string filePath;
            string fileDataPath;
            SereinProjectData? flowProjectData;

            string exeAssemblyDictPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
           
            if (args.Length == 1) 
            {
                filePath = args[0];
                fileDataPath = Path.GetDirectoryName(filePath) ?? "";
            }
            else if (args.Length == 0)
            {
                Console.WriteLine("loading project file data...");
                filePath = Process.GetCurrentProcess().ProcessName + ".dnf";
                fileDataPath = exeAssemblyDictPath;

            }
            else
            {
                return;
            }

            Console.WriteLine($"Current Name : {filePath}");
            Console.WriteLine($"Dict Path : {fileDataPath}");
            try
            {
                // 读取文件内容
                string content = File.ReadAllText(filePath); // 读取整个文件内容
                flowProjectData = JsonConvert.DeserializeObject<SereinProjectData>(content);
                if (flowProjectData is null || string.IsNullOrEmpty(fileDataPath))
                {
                    throw new Exception("项目文件读取异常");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取文件时发生错误：{ex.Message}");
                return;
            }

            IsRuning = true;
            _ = Task.Run(async () => await StartFlow(flowProjectData, fileDataPath));
            while (IsRuning)
            {
                Console.ReadKey();
            }
        }


        public static IFlowEnvironment? Env;
        public static bool IsRuning;
        public static async Task StartFlow(SereinProjectData flowProjectData, string fileDataPath)
        {
            
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

            Env = new FlowEnvironmentDecorator(uIContextOperation); 
            Env.LoadProject(new FlowEnvInfo { Project = flowProjectData }, fileDataPath); // 加载项目
            await Env.StartRemoteServerAsync(7525); // 启动 web socket 监听远程请求

            //await Env.StartAsync();

            IsRuning = false;
        }

    }
}
