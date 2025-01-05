using Serein.Library;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Serein.Workbench.Themes
{
    /// <summary>
    /// WindowDialogInput.xaml 的交互逻辑
    /// </summary>
    public partial class WindowEnvRemoteLoginView : Window
    {
        private Action<string, int, string> ConnectRemoteFlowEnv;

        /// <summary>
        /// 弹窗输入
        /// </summary>
        /// <param name="connectRemoteFlowEnv"></param>
        public WindowEnvRemoteLoginView(Action<string, int, string> connectRemoteFlowEnv)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            InitializeComponent();
            ConnectRemoteFlowEnv = connectRemoteFlowEnv;
        }

        private void ButtonTestConnect_Client(object sender, RoutedEventArgs e)
        {
            var addres = this.TextBlockAddres.Text;
            _ = int.TryParse(this.TextBlockPort.Text, out var port);
            _ = Task.Run(() => {
                bool success = false;
                try
                {
                    TcpClient tcpClient = new TcpClient();
                    var result = tcpClient.BeginConnect(addres, port, null, null);
                    success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3));
                }
                catch 
                {
                    success = false;
                }
                if (!success)
                {
                    SereinEnv.WriteLine(InfoType.ERROR, $"无法连接远程:{addres}:{port}");
                }            
            });
            
        }

        private void ButtonTestLoginEnv_Client(object sender, RoutedEventArgs e)
        {
            var addres = this.TextBlockAddres.Text;
            _ = int.TryParse(this.TextBlockPort.Text, out var port);
            var token = this.TextBlockToken.Text;
            ConnectRemoteFlowEnv?.Invoke(addres, port, token);
        }
    }
}
