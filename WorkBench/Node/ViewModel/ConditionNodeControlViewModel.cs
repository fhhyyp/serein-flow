using Serein.Flow;
using Serein.Flow.NodeModel;
using Serein.WorkBench.Node.View;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using static Dm.net.buffer.ByteArrayBuffer;

namespace Serein.WorkBench.Node.ViewModel
{
    public class ConditionNodeControlViewModel : NodeControlViewModel
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

        public ConditionNodeControlViewModel(SingleConditionNode node)
        {
            this.singleConditionNode = node;
            MethodDetails = node.MethodDetails;
            IsCustomData = false;
            CustomData = "";
            Expression = "PASS";
        }

    }
}
