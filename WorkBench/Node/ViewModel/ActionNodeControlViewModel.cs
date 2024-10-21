using Serein.NodeFlow.Model;
using Serein.Workbench.Node.View;

namespace Serein.Workbench.Node.ViewModel
{
    public class ActionNodeControlViewModel : NodeControlViewModelBase
    {
        //public SingleActionNode NodelModel
        //{
        //    get => (SingleActionNode)base.NodeModel; set
        //    {
        //        if (base.NodeModel == null)
        //        {
        //            base.NodeModel = value;
        //        }
        //    }
        //}

        public ActionNodeControlViewModel(SingleActionNode node):base(node) 
        {
            // this.NodelModel = node;
        }
    }
}
