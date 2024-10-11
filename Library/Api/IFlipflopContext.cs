using Serein.Library.Enums;
using Serein.Library.NodeFlow.Tool;

namespace Serein.Library.Api
{
    /// <summary>
    /// 触发器必须使用该接口作为返回值，同时必须用Task泛型表示，否则将不会进行等待触发。
    /// </summary>
    public interface IFlipflopContext<out TResult>
    {
        /// <summary>
        /// 触发器完成的状态（根据业务场景手动设置）
        /// </summary>
        FlipflopStateType State { get; set; }
        /// <summary>
        /// 触发传递的数据
        /// </summary>
        //TriggerData TriggerData { get; set; }

        TriggerType Type { get; set; }
        /// <summary>
        /// 触发传递的数据
        /// </summary>
        TResult Value { get; }
    }
}
