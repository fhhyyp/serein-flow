using Newtonsoft.Json;
using Serein.Library.Api;
using Serein.Library.Entity;
using Serein.NodeFlow;
using System.Diagnostics;
using System.Reflection;

namespace Serein.FlowStartTool
{
    public class Program
    {
        public static void Main(string[] args)
        {
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
            StartFlow(flowProjectData, fileDataPath).GetAwaiter().GetResult();
            while (IsRuning)
            {

            }
        }


        public static IFlowEnvironment? Env;
        public static bool IsRuning;
        public static async Task StartFlow(SereinProjectData flowProjectData, string fileDataPath)
        {
            Env = new FlowEnvironment();
            Env.LoadProject(flowProjectData, fileDataPath); // 加载项目
            await Env.StartAsync();
            IsRuning = false;
        }

    }
}
