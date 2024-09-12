using Serein.Library.Enums;

namespace Serein.Library.Api
{
    public interface IFlipflopContext
    {
        FlowStateType State { get; set; }
        object Data { get; set; }
    }
}
