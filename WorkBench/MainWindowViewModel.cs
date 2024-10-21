using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Env;
using System.Windows;

namespace Serein.Workbench
{
    /// <summary>
    /// 工作台数据视图
    /// </summary>
    /// <param name="window"></param>
    public class MainWindowViewModel
    {
        private readonly MainWindow window ;
        /// <summary>
        /// 运行环境
        /// </summary>
        public IFlowEnvironment FlowEnvironment { get; set; }

        /// <summary>
        /// 工作台数据视图
        /// </summary>
        /// <param name="window"></param>
        public MainWindowViewModel(MainWindow window)
        {
            UIContextOperation? uIContextOperation = null;
            Application.Current.Dispatcher.Invoke(() =>
            {
                SynchronizationContext? uiContext = SynchronizationContext.Current; // 在UI线程上获取UI线程上下文信息
                if (uiContext != null)
                {
                    uIContextOperation = new UIContextOperation(uiContext); // 封装一个调用UI线程的工具类
                }
            });

            if (uIContextOperation is null) 
            {
                throw new Exception("无法封装 UIContextOperation ");
            }
            else
            {
                FlowEnvironment = new FlowEnvironmentDecorator(uIContextOperation);
                //_ = FlowEnvironment.StartRemoteServerAsync();
                this.window = window;
            }
        }


    }
}
