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
        /// <summary>
        /// 运行环境
        /// </summary>
        private static readonly FlowEnv flowEnv = new FlowEnv();
        public static void Main(string[] args)
        {


            #region 获取文件路径
#if debug
            args = [@"F:\临时\project\linux\project.dnf"];
#endif
            Console.WriteLine("Hello :) ");
            Console.WriteLine($"args : {string.Join(" , ", args)}");

            string filePath;
            string fileDataPath;
            SereinProjectData? flowProjectData;
            string? assembly = Assembly.GetExecutingAssembly()?.Location;
            string exeAssemblyDictPath = Path.GetDirectoryName(assembly)!;

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
            #endregion

            #region 读取项目文件内容
            try
            {
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
            #endregion

            #region 加载项目
            _ = Task.Run(async () => await flowEnv.StartFlow(flowProjectData, fileDataPath));
            while (flowEnv.IsRuning)
            {
                Console.ReadKey();
            } 
            #endregion

        }



        
    }
}
