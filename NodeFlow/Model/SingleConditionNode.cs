
using Serein.Library.Api;
using Serein.Library.Entity;
using Serein.Library.Enums;
using Serein.NodeFlow.Base;
using Serein.NodeFlow.Tool.SerinExpression;

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


        public override object? Execute(IDynamicContext context)
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
                NextOrientation = isPass ? ConnectionType.IsSucceed : ConnectionType.IsFail;
            }
            catch (Exception ex)
            {
                NextOrientation = ConnectionType.IsError;
                RuningException = ex;
            }
            
            Console.WriteLine($"{result} {Expression}  -> " + NextOrientation);
            return result;
        }

        public override Parameterdata[] GetParameterdatas()
        {
            if (base.MethodDetails.ExplicitDatas.Length > 0)
            {
                return MethodDetails.ExplicitDatas
                                     .Select(it => new Parameterdata
                                     {
                                         state = IsCustomData,
                                         expression = Expression,
                                         value = CustomData switch
                                         {
                                             Type when CustomData.GetType() == typeof(int)
                                                        && CustomData.GetType() == typeof(double)
                                                        && CustomData.GetType() == typeof(float)
                                                             => ((double)CustomData).ToString(),
                                             Type when CustomData.GetType() == typeof(bool) => ((bool)CustomData).ToString(),
                                             _ => CustomData?.ToString()!,
                                         }
                                     })
                                     .ToArray();
            }
            else
            {
                return [];
            }
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
