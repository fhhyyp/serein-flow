using Serein.NodeFlow.Model;
using Serein.NodeFlow.Model.Nodes;
using System.Windows;
using System.Windows.Input;

namespace Serein.Workbench.Node.ViewModel
{
    /// <summary>
    /// 全局数据节点控制视图模型
    /// </summary>
    public class GlobalDataNodeControlViewModel : NodeControlViewModelBase
    {
        private new SingleGlobalDataNode NodeModel => (SingleGlobalDataNode)base.NodeModel;

        /// <summary>
        /// 复制全局数据表达式
        /// </summary>
        public ICommand CommandCopyDataExp { get; }

        /// <summary>
        /// 刷新数据
        /// </summary>
        public ICommand CommandRefreshData { get; }

        /// <summary>
        /// 全局数据节点控制视图模型构造函数
        /// </summary>
        /// <param name="node"></param>
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

 
      
    }
}
