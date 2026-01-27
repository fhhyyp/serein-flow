using Microsoft.Extensions.DependencyInjection;
using Serein.Extend.NewtonsoftJson;
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
            collection.AddSingleton<Locator>(); // 视图模型路由

            collection.AddSingleton<MainViewModel>(); 
            collection.AddSingleton<MainMenuBarViewModel>(); // 菜单栏视图模型
            collection.AddSingleton<FlowWorkbenchViewModel>(); // 工作台视图模型
            collection.AddSingleton<BaseNodesViewModel>(); // 基础节点视图模型
            collection.AddSingleton<FlowLibrarysViewModel>(); // 流程已加载依赖视图模型
            collection.AddSingleton<FlowEditViewModel>(); // 流程画布编辑器视图模型
            collection.AddSingleton<ViewNodeInfoViewModel>(); // 节点信息视图模型
            collection.AddSingleton<ViewNodeMethodInfoViewModel>(); // 方法信息视图模型
            collection.AddSingleton<ViewCanvasInfoViewModel>(); // 画布视图模型

            collection.AddTransient<FlowCanvasViewModel>(); // 画布
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
            Func<SynchronizationContext>? getSyncContext = null;
            UIContextOperation? uIContextOperation = new(getSyncContext); // 封装一个调用UI线程的工具类
            IFlowEnvironment flowEnvironment = new FlowEnvironment(uIContextOperation);
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                if (SynchronizationContext.Current is { } uiContext)
                {
                    // 在UI线程上获取UI线程上下文信息
                    getSyncContext = () => uiContext;

                    flowEnvironment.UIContextOperation.GetUiContext = () => uiContext; 
                }
            });


            collection.AddSingleton<UIContextOperation>(uIContextOperation); // 注册UI线程操作上下文
            collection.AddSingleton<IFlowEnvironment>(flowEnvironment); // 注册运行环境
            collection.AddSingleton<IFlowEnvironmentEvent>(flowEnvironment.Event); // 注册运行环境事件
            collection.AddSingleton<IFlowEEForwardingService, FlowEEForwardingService>(); // 注册工作台环境事件

            //#region 创建实例
           
            //Func<SynchronizationContext>? getSyncContext = null;
            //Dispatcher.CurrentDispatcher.Invoke(() =>
            //{
            //    var uiContext = SynchronizationContext.Current; // 在UI线程上获取UI线程上下文信息
            //    if (uiContext is not null)
            //    {
            //        getSyncContext = () => uiContext;
            //    }
            //});
            //UIContextOperation? uIContextOperation = new (getSyncContext); // 封装一个调用UI线程的工具类
            //IFlowEnvironment flowEnvironment = new FlowEnvironment();
            //flowEnvironment.SetUIContextOperation(uIContextOperation);



            //collection.AddSingleton<UIContextOperation>(uIContextOperation); // 注册UI线程操作上下文
            //collection.AddSingleton<IFlowEnvironment>(flowEnvironment); // 注册运行环境
            //collection.AddSingleton<IFlowEnvironmentEvent>(flowEnvironment.Event); // 注册运行环境事件

            //#endregion
        }
    }



}
