using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Env;
using Serein.Workbench.Api;
using Serein.Workbench.Services;
using Serein.Workbench.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Serein.Workbench
{

    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 注册ViewModel
        /// </summary>
        /// <param name="collection"></param>
        public static void AddViewModelServices(this IServiceCollection collection)
        {
            collection.AddSingleton<Locator>(); // 主窗体

            collection.AddSingleton<MainViewModel>();
            collection.AddSingleton<MainMenuBarViewModel>();
            collection.AddSingleton<FlowWorkbenchViewModel>();
            collection.AddSingleton<BaseNodesViewModel>();
            collection.AddSingleton<FlowLibrarysViewModel>();
            collection.AddSingleton<FlowEditViewModel>();
            collection.AddSingleton<ViewNodeInfoViewModel>();
            collection.AddSingleton<ViewNodeMethodInfoViewModel>();

            collection.AddTransient<FlowCanvasViewModel>(); // 画布
            collection.AddTransient<ViewCanvasInfoViewModel>(); // 画布节点树视图
        }

        public static void AddWorkbenchServices(this IServiceCollection collection)
        {
            collection.AddSingleton<IFlowEEForwardingService, FlowEEForwardingService>(); // 流程事件管理
            collection.AddSingleton<IKeyEventService, KeyEventService>();// 按键事件管理
            collection.AddSingleton<IWorkbenchEventService, WorkbenchEventService>(); // 流程事件管理
            collection.AddSingleton<FlowProjectService>(); // 项目管理
            collection.AddSingleton<FlowNodeService>(); // 节点操作管理
        }


        /// <summary>
        /// 注册流程接口相关实例
        /// </summary>
        /// <param name="collection"></param>
        public static void AddFlowServices(this IServiceCollection collection)
        {
            #region 创建实例
            Func<SynchronizationContext> getSyncContext = null;
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                var uiContext = SynchronizationContext.Current; // 在UI线程上获取UI线程上下文信息
                if (uiContext is not null)
                {
                    getSyncContext = () => uiContext;
                }
                
            });

            UIContextOperation? uIContextOperation = null;
            uIContextOperation = new UIContextOperation(getSyncContext); // 封装一个调用UI线程的工具类
            var flowEnvironmentDecorator = new FlowEnvironmentDecorator();
            flowEnvironmentDecorator.SetUIContextOperation(uIContextOperation);
            collection.AddSingleton<UIContextOperation>(uIContextOperation); // 注册UI线程操作上下文
            collection.AddSingleton<IFlowEnvironment>(flowEnvironmentDecorator); // 注册运行环境
            collection.AddSingleton<IFlowEnvironmentEvent>(flowEnvironmentDecorator); // 注册运行环境事件
                    
            #endregion

        }
    }



    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static IServiceProvider? ServiceProvider;

        /// <summary>
        /// UI线程
        /// </summary>
        public static UIContextOperation UIContextOperation => App.GetService<UIContextOperation>() ?? throw new NullReferenceException();

        public static T GetService<T>() where T : class
        {
            return ServiceProvider?.GetService<T>() ?? throw new NullReferenceException();
        }
       
        public App()
        {
            var collection = new ServiceCollection();
            collection.AddWorkbenchServices();
            collection.AddFlowServices();
            collection.AddViewModelServices();
            var services = collection.BuildServiceProvider(); // 绑定并返回获取实例的服务接口
            App.ServiceProvider = services;
#if DEBUG
            _ = this.LoadLocalProjectAsync();
#endif
        }

#if DEBUG

        // 这里是测试代码，可以删除
        private async Task LoadLocalProjectAsync()
        {
            var projectService = App.GetService<FlowProjectService>();
            await Task.Delay(500);
            if (1 == 1)
            {
                string filePath;
                filePath = @"F:\TempFile\flow\temp2\project.dnf";
                projectService.LoadLocalProject(filePath);
            }

        }
#endif


        private async void Application_Startup(object sender, StartupEventArgs e)
        {
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

