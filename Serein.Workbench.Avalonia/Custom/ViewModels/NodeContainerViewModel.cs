using Avalonia.Controls;
using Serein.Library.Api;
using Serein.NodeFlow.Env;
using Serein.Workbench.Avalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Avalonia.Custom.ViewModels
{
    internal class NodeContainerViewModel : ViewModelBase
    {
        /// <summary>
        /// 正在创建方法调用关系的连接线
        /// </summary>
        public bool IsConnectionInvokeNode { get; set; } = false;
        /// <summary>
        /// 正在创建参数传递关系的连接线
        /// </summary>
        public bool IsConnectionArgSourceNode { get; set; } = false;
        public NodeContainerViewModel()
        {
            
        }

    }
}
