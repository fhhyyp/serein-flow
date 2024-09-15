using Serein.Library.Enums;

namespace Serein.Library.Api
{
    public interface IFlipflopContext
    {
        FlipflopStateType State { get; set; }
        object Data { get; set; }
    }
}
