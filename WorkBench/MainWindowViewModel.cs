using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.Entity;
using Serein.Library.Utils;
using Serein.NodeFlow;
using Serein.NodeFlow.Tool;
using Serein.WorkBench.Node.View;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Serein.WorkBench
{
    public class MainWindowViewModel
    {
        private readonly MainWindow window ;
        public IFlowEnvironment FlowEnvironment { get; set; }
        public MainWindowViewModel(MainWindow window)
        {
            FlowEnvironment = new FlowEnvironment();
            this.window = window;
        }


    }
}
