using Serein.NodeFlow.Model;
using Serein.WorkBench.Node.View;

namespace Serein.WorkBench.Node.ViewModel
{
    public class ConditionNodeControlViewModel : NodeControlViewModelBase
    {
        private readonly SingleConditionNode singleConditionNode;

        /// <summary>
        /// 是否为自定义参数
        /// </summary>
        public bool IsCustomData
        {
            get => singleConditionNode.IsCustomData;
            set { singleConditionNode.IsCustomData= value; OnPropertyChanged(); }
        }
        /// <summary>
         /// 自定义参数值
         /// </summary>
        public object? CustomData
        {
            get => singleConditionNode.CustomData;
            set { singleConditionNode.CustomData = value ; OnPropertyChanged(); }
        }
        /// <summary>
        /// 表达式
        /// </summary>
        public string Expression
        {
            get => singleConditionNode.Expression;
            set { singleConditionNode.Expression = value; OnPropertyChanged(); }
        }

        public ConditionNodeControlViewModel(SingleConditionNode node) : base(node)
        {
            this.singleConditionNode = node;
            //IsCustomData = false;
            //CustomData = "";
            //Expression = "PASS";
        }

    }
}
