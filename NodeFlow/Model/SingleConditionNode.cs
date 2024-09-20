
using Serein.Library.Api;
using Serein.Library.Entity;
using Serein.Library.Enums;
using Serein.NodeFlow.Base;
using Serein.NodeFlow.Tool.SereinExpression;

namespace Serein.NodeFlow.Model
{
    /// <summary>
    /// 条件节点（用于条件控件）
    /// </summary>
    public class SingleConditionNode : NodeModelBase
    {

        /// <summary>
        /// 是否为自定义参数
        /// </summary>
        public bool IsCustomData { get; set; }
        /// <summary>
        /// 自定义参数值
        /// </summary>
        public object? CustomData { get; set; }
        /// <summary>
        /// 条件表达式
        /// </summary>

        public string Expression { get; set; }


        //public override object? Executing(IDynamicContext context)
        public override Task<object?> ExecutingAsync(IDynamicContext context)
        {
            // 接收上一节点参数or自定义参数内容
            object? result;
            if (IsCustomData)
            {
                result = CustomData;
            }
            else
            {
                result = PreviousNode?.FlowData;
            }
            try
            {
                var isPass = SereinConditionParser.To(result, Expression);
                NextOrientation = isPass ? ConnectionType.IsSucceed : ConnectionType.IsFail;
            }
            catch (Exception ex)
            {
                NextOrientation = ConnectionType.IsError;
                RuningException = ex;
            }
            
            Console.WriteLine($"{result} {Expression}  -> " + NextOrientation);
            return Task.FromResult(result);
        }

        internal override Parameterdata[] GetParameterdatas()
        {
            var value = CustomData switch
            {
                Type when CustomData.GetType() == typeof(int)
                           && CustomData.GetType() == typeof(double)
                           && CustomData.GetType() == typeof(float)
                                => ((double)CustomData).ToString(),
                Type when CustomData.GetType() == typeof(bool) => ((bool)CustomData).ToString(),
                _ => CustomData?.ToString()!,
            };
            return [new Parameterdata
            {
                State = IsCustomData,
                Expression = Expression,
                Value = value,
            }];
        }



        internal override NodeModelBase LoadInfo(NodeInfo nodeInfo)
        {
            var node = this;
            if (node != null)
            {
                node.Guid = nodeInfo.Guid;
                for (int i = 0; i < nodeInfo.ParameterData.Length; i++)
                {
                    Parameterdata? pd = nodeInfo.ParameterData[i];
                    node.IsCustomData = pd.State;
                    node.CustomData = pd.Value;
                    node.Expression = pd.Expression;

                }
            }
            return this;
        }

        //public override void Execute(DynamicContext context)
        //{
        //    CurrentState = Judge(context, base.MethodDetails);
        //}

        //private bool Judge(DynamicContext context, MethodDetails md)
        //{
        //    try
        //    {
        //        if (DelegateCache.GlobalDicDelegates.TryGetValue(md.MethodName, out Delegate del))
        //        {
        //            object[] parameters = GetParameters(context, md);
        //            var temp = del.DynamicInvoke(parameters);
        //            //context.GetData(GetDyPreviousKey());
        //            return (bool)temp;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.Write(ex.Message);
        //    }
        //    return false;
        //}


    }


}
