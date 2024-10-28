using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils.SereinExpression;
using System.Reactive;

namespace Serein.NodeFlow.Model
{
    /// <summary>
    /// Expression Operation - 表达式操作
    /// </summary>
    [NodeProperty(ValuePath = NodeValuePath.Node)]
    public partial class SingleExpOpNode : NodeModelBase
    {
        /// <summary>
        /// 表达式
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private string _expression;

    }



    public partial class SingleExpOpNode : NodeModelBase
    {
        public SingleExpOpNode(IFlowEnvironment environment) : base(environment)
        {

        }

        /// <summary>
        /// 加载完成后调用的方法
        /// </summary>
        public override void OnLoading()
        {
            Console.WriteLine("SingleExpOpNode 暂未实现 OnLoading");
        }


        public override Task<object?> ExecutingAsync(IDynamicContext context)
        {
            var data = context.TransmissionData(this); // 表达式节点使用上一节点数据

            try
            {
                var newData = SerinExpressionEvaluator.Evaluate(Expression, data, out bool isChange);
                Console.WriteLine(newData);
                object? result = null;
                if (isChange)
                {
                    result = newData;
                }
                else
                {
                    result = data;
                }

                context.NextOrientation = ConnectionInvokeType.IsSucceed;
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                context.NextOrientation = ConnectionInvokeType.IsError;
                RuningException = ex;
                return Task.FromResult(data);
            }

        }

        public override ParameterData[] GetParameterdatas()
        {
            return [new ParameterData { Expression = Expression }];
        }



        public override NodeModelBase LoadInfo(NodeInfo nodeInfo)
        {
            var node = this;
            this.Position = nodeInfo.Position;// 加载位置信息
            node.Guid = nodeInfo.Guid;
            for (int i = 0; i < nodeInfo.ParameterData.Length; i++)
            {
                node.Expression = nodeInfo.ParameterData[i].Expression;
            }
            return this;
        }
    }
}
