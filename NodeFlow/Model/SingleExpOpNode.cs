using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils.SereinExpression;

namespace Serein.NodeFlow.Model
{
    /// <summary>
    /// Expression Operation - 表达式操作
    /// </summary>
    public class SingleExpOpNode : NodeModelBase
    {
        public SingleExpOpNode(IFlowEnvironment environment) : base(environment)
        {
            
        }
        /// <summary>
        /// 表达式
        /// </summary>
        public string Expression { get; set; }


        //public override async Task<object?> Executing(IDynamicContext context)
        public override Task<object?> ExecutingAsync(IDynamicContext context)
        {
            var data = PreviousNode?.GetFlowData(); // 表达式节点使用上一节点数据

            try
            {
                var newData = SerinExpressionEvaluator.Evaluate(Expression, data, out bool isChange);
                Console.WriteLine(newData);
                object? result = null;
                if (isChange)
                {
                    result =  newData;
                }
                else
                {
                    result = data;
                }

                NextOrientation = ConnectionType.IsSucceed;
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                NextOrientation = ConnectionType.IsError;
                RuningException = ex;
                return Task.FromResult(data);
            }

        }

        public override Parameterdata[] GetParameterdatas()
        {
            return [new Parameterdata{ Expression = Expression}];
        }



        public override NodeModelBase LoadInfo(NodeInfo nodeInfo)
        {
            var node = this;
            if (node != null)
            {
                node.Guid = nodeInfo.Guid;
                for (int i = 0; i < nodeInfo.ParameterData.Length; i++)
                {
                    node.Expression = nodeInfo.ParameterData[i].Expression;
                }
            }
            return this;
        }
    }
}
