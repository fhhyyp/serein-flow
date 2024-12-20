using Dm.parser;
using NetTaste;
using Newtonsoft.Json;
using Serein.Library;
using Serein.Library.Utils;
using Serein.Library.Utils.SereinExpression;
using Serein.NodeFlow.Model;
using Serein.Script;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Serein.Workbench
{
#if DEBUG

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
                string content = System.IO.File.ReadAllText(filePath); // 读取整个文件内容
                App.FlowProjectData = JsonConvert.DeserializeObject<SereinProjectData>(content);
                App.FileDataPath = System.IO.Path.GetDirectoryName(filePath)!;   //  filePath;//
                var dir = Path.GetDirectoryName(filePath);
                //System.IO.Directory.SetCurrentDirectory(dir);
            }
#endif
        }
        
        public static SereinProjectData? FlowProjectData { get; set; }
        public static string FileDataPath { get; set; } = "";

        public App()
        {

        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            // 强制关闭所有窗口
            foreach (Window window in Windows)
            {
                window.Close();
            }
        }
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() => { });


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

