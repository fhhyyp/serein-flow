using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow;
using Serein.NodeFlow.Env;
using Serein.Workbench.Avalonia.Api;
using Serein.Workbench.Avalonia.Custom.Node.ViewModels;
using Serein.Workbench.Avalonia.Custom.Node.Views;
using Serein.Workbench.Avalonia.Custom.ViewModels;
using Serein.Workbench.Avalonia.Services;
using Serein.Workbench.Avalonia.ViewModels;
using Serein.Workbench.Avalonia.Views;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Serein.Workbench.Avalonia;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册ViewModel
    /// </summary>
    /// <param name="collection"></param>
    public static void AddViewModelServices(this IServiceCollection collection)
    {
        collection.AddTransient<MainViewModel>(); // 主窗体
        collection.AddTransient<MainMenuBarViewModel>(); // 主窗体菜单
        collection.AddTransient<FlowLibrarysViewModel>(); // 依赖集合
        collection.AddTransient<FlowLibraryMethodInfoViewModel>(); // 预览的方法信息
        //collection.AddTransient<ParameterDetailsViewModel>(); // 节点参数信息
        collection.AddTransient<NodeContainerViewModel>(); // 节点容器（画布）


        collection.AddTransient<ActionNodeViewModel>(); // 节点容器（画布）


        //collection.AddTransient<FlowLibraryInfoViewModel>(); // 依赖信息
    }

    public static void AddWorkbenchServices(this IServiceCollection collection)
    {
        collection.AddSingleton<IFlowEEForwardingService, FlowEEForwardingService>(); // 流程事件管理
        collection.AddSingleton<IWorkbenchEventService, WorkbenchEventService>(); // 流程事件管理
        collection.AddSingleton<INodeOperationService, NodeOperationService>(); // 节点操作管理
        collection.AddSingleton<IKeyEventService, KeyEventService>(); // 按键事件管理
        //collection.AddSingleton<FlowNodeControlService>(); // 流程节点控件管理
    }


    /// <summary>
    /// 注册流程接口相关实例
    /// </summary>
    /// <param name="collection"></param>
    public static void AddFlowServices(this IServiceCollection collection)
    {
        
        #region 创建实例
        Func<SynchronizationContext> getSyncContext = null;
        Dispatcher.UIThread.Invoke(() =>
        {
            var uiContext = SynchronizationContext.Current; // 在UI线程上获取UI线程上下文信息
            if(uiContext is not null)
            {
                getSyncContext = () => uiContext;
            }
        });

        UIContextOperation? uIContextOperation = null;
        uIContextOperation = new UIContextOperation(getSyncContext); // 封装一个调用UI线程的工具类
        FlowEnvironmentDecorator flowEnvironmentDecorator = new FlowEnvironmentDecorator(uIContextOperation);
        collection.AddSingleton<UIContextOperation>(uIContextOperation); // 注册UI线程操作上下文
        collection.AddSingleton<IFlowEnvironment>(flowEnvironmentDecorator); // 注册运行环境
        collection.AddSingleton<IFlowEnvironmentEvent>(flowEnvironmentDecorator); // 注册运行环境事件
        //var strte =  tcs.Task.ConfigureAwait(false).GetAwaiter().GetResult();
        //if (strte) // 等待实例生成完成
        //{
        //} 
        #endregion

    }
}


public partial class App : Application
{
    private static IServiceProvider? ServiceProvider;
    public static T GetService<T>() where T : class
    {
        return ServiceProvider?.GetService<T>() ?? throw new NullReferenceException();
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }


    public override async void OnFrameworkInitializationCompleted()
    {
        // 如果使用 CommunityToolkit，则需要用下面一行移除 Avalonia 数据验证。
        // 如果没有这一行，数据验证将会在 Avalonia 和 CommunityToolkit 中重复。
        BindingPlugins.DataValidators.RemoveAt(0);

        // 注册应用程序运行所需的所有服务
        var collection = new ServiceCollection();
        collection.AddWorkbenchServices();
        collection.AddFlowServices();
        collection.AddViewModelServices();
        var services = collection.BuildServiceProvider(); // 绑定并返回获取实例的服务接口
        App.ServiceProvider = services;
        

        var vm = App.ServiceProvider?.GetRequiredService<MainViewModel>(); 
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = vm
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = vm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

}
