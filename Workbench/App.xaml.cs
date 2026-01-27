using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Env;
using Serein.NodeFlow.Services;
using Serein.Script;
using Serein.Workbench.Api;
using Serein.Workbench.Services;
using Serein.Workbench.ViewModels;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using static System.Net.Mime.MediaTypeNames;

namespace Serein.Workbench
{

 
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private static IServiceProvider? ServiceProvider;

        /// <summary>
        /// UI线程
        /// </summary>
        public static UIContextOperation UIContextOperation => App.GetService<UIContextOperation>() ?? throw new NullReferenceException();
    
        internal static T GetService<T>() where T : class
        {
            return ServiceProvider?.GetService<T>() ?? throw new NullReferenceException();
        }

        internal App()
        {
            var collection = new ServiceCollection();
            collection.AddWorkbenchServices();
            collection.AddFlowServices();
            collection.AddViewModelServices();
            var services = collection.BuildServiceProvider(); // 绑定并返回获取实例的服务接口
            App.ServiceProvider = services;
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
#if DEBUG && false

            try
            {
                var t = JsonHelper.Parse(TestJson.json);
                var iss = t["PreviousNodes"]["IsSucceed"][0];
            }
            catch (Exception ex)
            {

            }
#endif

            var projectService = App.GetService<FlowProjectService>();
           
            if (e.Args.Length == 1)
            {
                string filePath = e.Args[0];
                if (!System.IO.File.Exists(filePath))  // 检查文件是否存在
                {
                    MessageBox.Show($"文件未找到：{filePath}");
                    Shutdown(); // 关闭应用程序
                    return;
                }
                try
                {
                    // 读取文件内容
                    projectService.LoadLocalProject(filePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"读取文件时发生错误：{ex.Message}");
                    Shutdown(); // 关闭应用程序
                }
                
            }
           


        }
    }

}

