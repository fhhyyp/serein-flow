using Serein.Library;
using Serein.NodeFlow.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Serein.Workbench.Node.ViewModel
{
    public class GlobalDataNodeControlViewModel : NodeControlViewModelBase
    {
        private SingleGlobalDataNode NodeModel => (SingleGlobalDataNode)base.NodeModel;

        /// <summary>
        /// 复制全局数据表达式
        /// </summary>
        public ICommand CommandCopyDataExp { get; }

        /// <summary>
        /// 刷新数据
        /// </summary>
        public ICommand CommandRefreshData { get; }


        public GlobalDataNodeControlViewModel(SingleGlobalDataNode node) : base(node)
        {
            CommandCopyDataExp = new RelayCommand( o =>
            {
                string exp = NodeModel.KeyName;
                string copyValue = $"@Get #{exp}#";
                Clipboard.SetDataObject(copyValue);
            });
        }

        /// <summary>
        /// 自定义参数值
        /// </summary>
        public string? KeyName
        {
            get => NodeModel?.KeyName;
            set { NodeModel.KeyName = value; OnPropertyChanged(); }
        }


        /// <summary>
        /// 设置数据节点
        /// </summary>
        /// <param name="dataNode"></param>
        public void SetDataNode(NodeModelBase dataNode)
        {
            NodeModel.SetDataNode(dataNode);
        }




    }
}
