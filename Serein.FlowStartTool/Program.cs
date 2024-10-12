using Newtonsoft.Json;
using Serein.Library.Api;
using Serein.Library.Entity;
using Serein.NodeFlow;

namespace Serein.FlowStartTool
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello~");
            // 检查是否传入了参数
            if (args.Length == 1)
            {
                // 获取文件路径
                string filePath = args[0];
                // 检查文件是否存在
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"文件未找到：{filePath}");
                    return;
                }
                Console.WriteLine(filePath);
                return;
                SereinProjectData? flowProjectData;
                string fileDataPath;
                try
                {
                    // 读取文件内容
                    string content = System.IO.File.ReadAllText(filePath); // 读取整个文件内容
                    flowProjectData = JsonConvert.DeserializeObject<SereinProjectData>(content);
                    fileDataPath = System.IO.Path.GetDirectoryName(filePath) ?? "";
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
                
               _ = StartFlow(flowProjectData, fileDataPath);
            }
        }


        public static IFlowEnvironment? Env;
        public static async Task StartFlow(SereinProjectData flowProjectData, string fileDataPath)
        {
            Env = new FlowEnvironment();
            Env.LoadProject(flowProjectData, fileDataPath); // 加载项目
            await Env.StartAsync();
        }
    }
}
