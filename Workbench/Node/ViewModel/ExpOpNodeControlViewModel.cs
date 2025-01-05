using Serein.NodeFlow.Model;
using Serein.Workbench.Node.View;

namespace Serein.Workbench.Node.ViewModel
{
    public class ExpOpNodeControlViewModel: NodeControlViewModelBase
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


        public ExpOpNodeControlViewModel(SingleExpOpNode nodeModel) : base(nodeModel)
        { 
            this.NodeModel = nodeModel;
        }
    }
}
