using Serein.Library.Entity;
using Serein.NodeFlow.Base;

namespace Serein.NodeFlow.Model
{
    /// <summary>
    /// 单动作节点（用于动作控件)
    /// </summary>
    public class SingleActionNode : NodeModelBase
    {
        //public override void Execute(DynamicContext context)
        //{
        //    try
        //    {
        //        Execute(context, base.MethodDetails);
        //        CurrentState = true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.Write(ex.Message);
        //        CurrentState = false;
        //    }
        //}

        //public void Execute(DynamicContext context, MethodDetails md)
        //{
        //    if (DelegateCache.GlobalDicDelegates.TryGetValue(md.MethodName, out Delegate del))
        //    {

        //        object? result = null;

        //        if (md.ExplicitDatas.Length == 0)
        //        {
        //            if (md.ReturnType == typeof(void))
        //            {
        //                ((Action<object>)del).Invoke(md.ActingInstance);
        //            }
        //            else
        //            {
        //                result = ((Func<object, object>)del).Invoke(md.ActingInstance);
        //            }
        //        }
        //        else
        //        {
        //            object?[]? parameters = GetParameters(context, MethodDetails);
        //            if (md.ReturnType == typeof(void))
        //            {
        //                ((Action<object, object[]>)del).Invoke(md.ActingInstance, parameters);
        //            }
        //            else
        //            {
        //                result = ((Func<object, object[], object>)del).Invoke(md.ActingInstance, parameters);
        //            }
        //        }

        //        // 根据 ExplicitDatas.Length 判断委托类型
        //        //var action = (Action<object, object[]>)del;

        //        // 调用委托并获取结果
        //        // action.Invoke(MethodDetails.ActingInstance, parameters);

        //        //parameters = [md.ActingInstance, "", 123, ""];

        //        context.SetFlowData(result);
        //    }
        //}
        public override Parameterdata[] GetParameterdatas()
        {
            if (base.MethodDetails.ExplicitDatas.Length > 0)
            {
                return MethodDetails.ExplicitDatas
                                     .Select(it => new Parameterdata
                                     {
                                         state = it.IsExplicitData,
                                         value = it.DataValue,
                                     })
                                     .ToArray();
            }
            else
            {
                return [];
            }
        }
    }


}
