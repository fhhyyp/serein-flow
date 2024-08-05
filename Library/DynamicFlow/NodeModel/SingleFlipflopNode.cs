using Serein.DynamicFlow.Tool;

namespace Serein.DynamicFlow.NodeModel
{

    public class SingleFlipflopNode : NodeBase
    {
        //public override void Execute(DynamicContext context)
        //{
        //    throw new NotImplementedException("无法以非await/async的形式调用触发器");
        //}

        //public virtual async Task ExecuteAsync(DynamicContext context, Action NextTask = null)
        //{
        //    if (DelegateCache.GlobalDicDelegates.TryGetValue(MethodDetails.MethodName, out Delegate? del))
        //    {
        //        object?[]? parameters = GetParameters(context, MethodDetails);

        //        // 根据 ExplicitDatas.Length 判断委托类型
        //        var func = (Func<object, object[], Task<FlipflopContext>>)del;

        //        // 调用委托并获取结果
        //        FlipflopContext flipflopContext = await func.Invoke(MethodDetails.ActingInstance, parameters);

        //        if (flipflopContext != null)
        //        {
        //            if (flipflopContext.State == FfState.Cancel)
        //            {
        //                throw new Exception("取消此异步");
        //            }
        //            else
        //            {
        //                CurrentState = flipflopContext.State == FfState.Succeed;
        //                context.SetFlowData(flipflopContext.Data);
        //            }
        //        }
        //    }
        //}
    }
}
