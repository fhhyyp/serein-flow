using Serein.NodeFlow.Model;
using Serein.Workbench.Node.View;

namespace Serein.Workbench.Node.ViewModel
{
    public class ExpOpNodeViewModel: NodeControlViewModelBase
    {
        public  new SingleExpOpNode NodeModel { get; }

        //public string Expression
        //{
        //    get => node.Expression;
        //    set
        //    {
        //        node.Expression = value;
        //        OnPropertyChanged();
        //    }
        //}


        public ExpOpNodeViewModel(SingleExpOpNode nodeModel) : base(nodeModel)
        { 
            this.NodeModel = nodeModel;
        }
    }
}
