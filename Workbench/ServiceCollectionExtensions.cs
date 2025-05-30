using Microsoft.Extensions.DependencyInjection;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Env;
using Serein.Workbench.Api;
using Serein.Workbench.Services;
using Serein.Workbench.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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



}
