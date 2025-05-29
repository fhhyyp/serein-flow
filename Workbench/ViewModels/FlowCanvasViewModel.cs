using CommunityToolkit.Mvvm.ComponentModel;
using Serein.Library;
using Serein.Library.Api;
using Serein.Workbench.Api;
using Serein.Workbench.Node.View;
using Serein.Workbench.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;

namespace Serein.Workbench.ViewModels
{
    public partial class FlowCanvasViewModel : ObservableObject
    {
        /// <summary>
        /// 画布当前的节点
        /// </summary>
        public Dictionary<string, NodeControlBase> NodeControls { get; set; } = [];

        /// <summary>
        /// 正在创建节点方法调用关系
        /// </summary>
        [ObservableProperty]
        private bool _isConnectionInvokeNode;

        /// <summary>
        /// 正在创建节点参数连接关系
        /// </summary>
        [ObservableProperty]
        private bool _isConnectionArgSourceNode;

        /// <summary>
        /// 画布数据实体
        /// </summary>
        [ObservableProperty]
        private FlowCanvasDetails _model;



    }
}
