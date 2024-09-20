using Serein.Library.Enums;
using Serein.Library.NodeFlow.Tool;

namespace Serein.Library.Api
{
    public interface IFlipflopContext
    {
        FlipflopStateType State { get; set; }
        TriggerData TriggerData { get; set; }
    }
}
