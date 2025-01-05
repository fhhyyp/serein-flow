using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Env;
using System.ComponentModel;
using System.Windows;

namespace Serein.Workbench
{
    /// <summary>
    /// 工作台数据视图
    /// </summary>
    /// <param name="window"></param>
    public class MainWindowViewModel: INotifyPropertyChanged
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


        private bool _isConnectionInvokeNode = false;
        /// <summary>
        /// 是否正在连接节点的方法调用关系
        /// </summary>
        public bool IsConnectionInvokeNode { get => _isConnectionInvokeNode; set
            {
                if (_isConnectionInvokeNode != value)
                {
                    SetProperty<bool>(ref _isConnectionInvokeNode, value);
                }
            }
        }

        private bool _isConnectionArgSouceNode = false;
        /// <summary>
        /// 是否正在连接节点的参数传递关系
        /// </summary>
        public bool IsConnectionArgSourceNode { get => _isConnectionArgSouceNode; set
            {
                if (_isConnectionArgSouceNode != value)
                {
                    SetProperty<bool>(ref _isConnectionArgSouceNode, value);
                }
            }
        }


        /// <summary>
        /// 略
        /// <para>此事件为自动生成</para>
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        /// <summary>
        /// 通知属性变更
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="storage">绑定的变量</param>
        /// <param name="value">新的数据</param>
        /// <param name="propertyName"></param>
        protected void SetProperty<T>(ref T storage, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return;
            }

            storage = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
