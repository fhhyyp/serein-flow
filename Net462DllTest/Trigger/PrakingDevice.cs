using Net462DllTest.LogicControl;
using Serein.Library;
using Serein.Library.Utils;

namespace Net462DllTest.Trigger
{
    [AutoRegister]
    public class PrakingDevice : TaskFlowTrigger<ParkingCommand>
    {
    }

}
