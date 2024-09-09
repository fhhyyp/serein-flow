
using Serein.Library.SerinExpression;

namespace Serein.NodeFlow.Model
{
    /// <summary>
    /// 条件节点（用于条件控件）
    /// </summary>
    public class SingleConditionNode : NodeBase
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


        public override object? Execute(DynamicContext context)
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
                var isPass = SerinConditionParser.To(result, Expression);
                FlowState = isPass ? FlowStateType.Succeed : FlowStateType.Fail;
            }
            catch (Exception ex)
            {
                FlowState = FlowStateType.Error;
                Exception = ex;
            }
            
            Console.WriteLine($"{result} {Expression}  -> " + FlowState);
            return result;
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
