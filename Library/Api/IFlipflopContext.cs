using Serein.Library.Enums;
using Serein.Library.NodeFlow.Tool;

namespace Serein.Library.Api
{
    /// <summary>
    /// <para>触发器必须使用该接口作为返回值，同时必须用Task泛型表示，否则将不会进行等待触发。</para>
    /// <para>即使大多数时候，触发器传出的数据可能是任何一种数据类型，导致其泛型参数可能是无意义的 object / dynamic 。</para>
    /// <para>但在确定传出类型的场景下，至少可以保证数据一定为某个类型。</para>
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
