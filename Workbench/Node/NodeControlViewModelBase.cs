using System.ComponentModel;
using Serein.Library;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Data;
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Serein.Library.Api;

namespace Serein.Workbench.Node.ViewModel
{
    public abstract partial class NodeControlViewModelBase : ObservableObject
    {
        
        ///// <summary>
        ///// 对应的节点实体类
        ///// </summary>
        public IFlowNode NodeModel { get; }

        /// <summary>
        /// 节点控制器的基类
        /// </summary>
        /// <param name="nodeModel"></param>
        public NodeControlViewModelBase(IFlowNode nodeModel)
        {
            NodeModel = nodeModel;

        }

        /// <summary>
        /// 工作台预览基本节点时，避免其中的文本框响应拖拽事件导致卡死
        /// </summary>
        [ObservableProperty]
        private bool isEnabledOnView = true;


    }
}
