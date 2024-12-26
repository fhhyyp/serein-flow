using Newtonsoft.Json;
using Serein.Library;
using System.IO;
using System.Windows;

namespace Serein.Workbench
{
#if DEBUG
    public class People
    {
        public string Name { get; set; } 
        public int Id { get; set; } 
        public int Age { get; set; } 
    }
#endif



    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        void LoadLocalProject()
        {
#if DEBUG 
            if (1 == 1)
            {
                // 这里是我自己的测试代码，你可以删除
                string filePath;
                filePath = @"C:\Users\Az\source\repos\CLBanyunqiState\CLBanyunqiState\bin\Release\net8.0\PLCproject.dnf";
                filePath = @"C:\Users\Az\source\repos\CLBanyunqiState\CLBanyunqiState\bin\Release\banyunqi\project.dnf";
                filePath = @"C:\Users\Az\source\repos\CLBanyunqiState\CLBanyunqiState\bin\debug\net8.0\project.dnf";
                string content = System.IO.File.ReadAllText(filePath); // 读取整个文件内容
                App.FlowProjectData = JsonConvert.DeserializeObject<SereinProjectData>(content);
                App.FileDataPath = System.IO.Path.GetDirectoryName(filePath)!;   //  filePath;//
                var dir = Path.GetDirectoryName(filePath);
            }
#endif
        }
        
        public static SereinProjectData? FlowProjectData { get; set; }
        public static string FileDataPath { get; set; } = "";

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 检查是否传入了参数
            if (e.Args.Length == 1)
            {
                // 获取文件路径
                string filePath = e.Args[0];
                // 检查文件是否存在
                if (!System.IO.File.Exists(filePath))
                {
                    MessageBox.Show($"文件未找到：{filePath}");
                    Shutdown(); // 关闭应用程序
                    return;
                }

                try
                {
                    // 读取文件内容
                    string content = System.IO.File.ReadAllText(filePath); // 读取整个文件内容
                    FlowProjectData = JsonConvert.DeserializeObject<SereinProjectData>(content);
                    FileDataPath = System.IO.Path.GetDirectoryName(filePath) ?? "";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"读取文件时发生错误：{ex.Message}");
                    Shutdown(); // 关闭应用程序
                }
                
            }
            this.LoadLocalProject();


        }
    }

}

