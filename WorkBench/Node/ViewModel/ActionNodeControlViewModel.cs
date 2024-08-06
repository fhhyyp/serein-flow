using Serein.NodeFlow.Model;
using Serein.WorkBench.Node.View;

namespace Serein.WorkBench.Node.ViewModel
{
    public class ActionNodeControlViewModel : NodeControlViewModel
    {
        private readonly SingleActionNode node;

        public ActionNodeControlViewModel(SingleActionNode node)
        {
            this.node = node;
            MethodDetails = node.MethodDetails;
            //if (node.MethodDetails.ExplicitDatas.Length == 0)
            //{
            //    // 没有显式项
            //    IsExistExplicitData = false;
            //    ExplicitDatas = [];
            //}
            //else
            //{
            //    explicitDatas = node.MethodDetails.ExplicitDatas;
            //    //ExplicitDatas = node.MethodDetails.ExplicitDatas;
            //    IsExistExplicitData = true;
            //}
        }
    }
}
