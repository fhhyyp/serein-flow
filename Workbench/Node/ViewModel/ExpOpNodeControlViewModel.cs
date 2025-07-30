using Serein.NodeFlow.Model;
using Serein.NodeFlow.Model.Nodes;
using Serein.Workbench.Node.View;

namespace Serein.Workbench.Node.ViewModel
{
    /// <summary>
    /// 表达式操作节点控制视图模型
    /// </summary>
    public class ExpOpNodeControlViewModel: NodeControlViewModelBase
    {
        /// <summary>
        /// 对应的表达式操作节点模型
        /// </summary>
        public new SingleExpOpNode NodeModel { get; }

        //public string Expression
        //{
        //    get => node.Expression;
        //    set
        //    {
        //        node.Expression = value;
        //        OnPropertyChanged();
        //    }
        //}

        /// <summary>
        /// 表达式操作节点控制视图模型构造函数
        /// </summary>
        /// <param name="nodeModel"></param>

        public ExpOpNodeControlViewModel(SingleExpOpNode nodeModel) : base(nodeModel)
        { 
            this.NodeModel = nodeModel;
        }
    }
}
