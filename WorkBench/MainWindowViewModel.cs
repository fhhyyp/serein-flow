using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.Entity;
using Serein.Library.Utils;
using Serein.NodeFlow;
using Serein.NodeFlow.Tool;
using Serein.Workbench.Node.View;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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
            FlowEnvironment = new FlowEnvironment();
            this.window = window;
        }


    }
}
