using CommunityToolkit.Mvvm.ComponentModel;
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



        public FlowCanvasViewModel()
        {
            
        }
    }
}
