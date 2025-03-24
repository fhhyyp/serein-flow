using CommunityToolkit.Mvvm.ComponentModel;
using Serein.Library;
using Serein.Workbench.Node.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.ViewModels
{
    public partial class FlowCanvasViewModel : ObservableObject
    {
        /// <summary>
        /// 画布当前选中的节点
        /// </summary>
        public NodeControlBase CurrentSelectNodeControl {  get; set; }  


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
        /// 画布显示名称
        /// </summary>
        [ObservableProperty]
        private string _name;
        
        /// <summary>
        /// 画布ID
        /// </summary>
        [ObservableProperty]
        private string _canvasGuid;

        /// <summary>
        /// 画布数据实体
        /// </summary>
        [ObservableProperty]
        private FlowCanvasModel _model;



        public FlowCanvasViewModel()
        {
            
        }
    }
}
